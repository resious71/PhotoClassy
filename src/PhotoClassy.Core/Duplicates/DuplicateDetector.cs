using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using PhotoClassy.Core.Engine;
using PhotoClassy.Core.Logging;

namespace PhotoClassy.Core.Duplicates
{
    /// <summary>대상 폴더에 이미 있는 파일.</summary>
    public class ExistingFile
    {
        public ExistingFile(string path, long size)
        {
            Path = path;
            Size = size;
        }

        public string Path { get; }

        public long Size { get; }

        public string Hash { get; set; }
    }

    /// <summary>
    /// 중복 판정.
    /// - 비교 대상: 이번 실행의 소스 파일 + 대상 폴더에 이미 있는 파일
    /// - 크기가 같은 후보만 SHA256 을 계산하고, 크기가 유일하면 해시 없이 비중복으로 판정
    /// - 해시가 같으면 파일명이 달라도 중복
    /// </summary>
    public class DuplicateDetector
    {
        private readonly Func<string, CancellationToken, string> _hasher;

        public DuplicateDetector(Func<string, CancellationToken, string> hasher = null)
        {
            _hasher = hasher ?? FileHasher.ComputeSha256;
        }

        /// <summary>해시 계산이 필요한 파일 수 (진행률 전체 값).</summary>
        public static int CountHashCandidates(IList<FileItem> sources, IList<ExistingFile> existing)
        {
            GetCandidates(sources, existing, out var sourceCandidates, out var existingCandidates);
            return sourceCandidates.Count + existingCandidates.Count;
        }

        /// <param name="sources">소스 파일 (처리 순서대로). 실패 상태인 항목은 무시된다.</param>
        /// <param name="existing">대상 폴더의 기존 파일 (_Duplicates 제외).</param>
        /// <param name="onHashed">파일 하나의 해시 계산이 끝날 때마다 호출 (진행 표시용, 여러 스레드에서 호출됨).</param>
        /// <param name="log">로그 (여러 스레드에서 호출됨).</param>
        /// <returns>중복으로 판정된 소스 파일 수.</returns>
        public int Detect(IList<FileItem> sources, IList<ExistingFile> existing, Action<string> onHashed,
            Action<LogEntry> log, CancellationToken cancellationToken, int maxDegreeOfParallelism = 4)
        {
            GetCandidates(sources, existing, out var sourceCandidates, out var existingCandidates);

            var options = new ParallelOptions
            {
                CancellationToken = cancellationToken,
                MaxDegreeOfParallelism = Math.Max(1, maxDegreeOfParallelism)
            };

            var failed = new ConcurrentBag<FileItem>();
            Parallel.ForEach(sourceCandidates, options, item =>
            {
                try
                {
                    if (item.Hash == null)
                        item.Hash = _hasher(item.SourcePath, cancellationToken);
                }
                catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
                {
                    item.Status = FileItemStatus.Failed;
                    item.Error = $"해시 계산 실패 (파일 읽기 오류): {ex.Message}";
                    failed.Add(item);
                    log?.Invoke(new LogEntry(LogLevel.Error, item.Error, item.SourcePath));
                }
                onHashed?.Invoke(item.SourcePath);
            });

            Parallel.ForEach(existingCandidates, options, file =>
            {
                try
                {
                    if (file.Hash == null)
                        file.Hash = _hasher(file.Path, cancellationToken);
                }
                catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
                {
                    log?.Invoke(new LogEntry(LogLevel.Warning,
                        $"대상 폴더의 기존 파일을 읽을 수 없어 중복 비교에서 제외합니다: {ex.Message}", file.Path));
                }
                onHashed?.Invoke(file.Path);
            });

            cancellationToken.ThrowIfCancellationRequested();

            var existingByHash = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var file in existingCandidates)
                if (file.Hash != null && !existingByHash.ContainsKey(file.Hash))
                    existingByHash[file.Hash] = file.Path;

            var firstSourceByHash = new Dictionary<string, FileItem>(StringComparer.Ordinal);
            var duplicates = 0;

            // 원래 순서를 유지해야 "먼저 나온 파일"이 원본이 된다.
            foreach (var item in sources)
            {
                if (item.Status == FileItemStatus.Failed || item.Hash == null)
                    continue;

                if (existingByHash.TryGetValue(item.Hash, out var existingPath))
                {
                    item.DuplicateOf = existingPath;
                    duplicates++;
                }
                else if (firstSourceByHash.TryGetValue(item.Hash, out var original))
                {
                    item.DuplicateOf = original.SourcePath;
                    item.DuplicateOfItem = original;
                    duplicates++;
                }
                else
                {
                    firstSourceByHash[item.Hash] = item;
                }
            }

            return duplicates;
        }

        private static void GetCandidates(IList<FileItem> sources, IList<ExistingFile> existing,
            out List<FileItem> sourceCandidates, out List<ExistingFile> existingCandidates)
        {
            var activeSources = sources.Where(s => s.Status != FileItemStatus.Failed).ToList();

            var sizeCounts = new Dictionary<long, int>();
            foreach (var item in activeSources)
                sizeCounts[item.Size] = sizeCounts.TryGetValue(item.Size, out var c) ? c + 1 : 1;

            var sourceSizes = new HashSet<long>(sizeCounts.Keys);
            existingCandidates = (existing ?? new List<ExistingFile>()).Where(e => sourceSizes.Contains(e.Size)).ToList();
            foreach (var file in existingCandidates)
                sizeCounts[file.Size]++;

            sourceCandidates = activeSources.Where(s => sizeCounts[s.Size] >= 2).ToList();
        }
    }
}
