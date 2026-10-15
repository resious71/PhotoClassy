using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using PhotoClassy.Core.Dates;
using PhotoClassy.Core.Duplicates;
using PhotoClassy.Core.Filtering;
using PhotoClassy.Core.IO;
using PhotoClassy.Core.Logging;
using PhotoClassy.Core.Operations;
using PhotoClassy.Core.Progress;
using PhotoClassy.Core.Scanning;
using PhotoClassy.Core.Settings;
using PhotoClassy.Core.Validation;

namespace PhotoClassy.Core.Engine
{
    /// <summary>
    /// 분류 작업 전체 흐름.
    /// 1. 소스 스캔 → 2. 확장자 필터 → 3. 날짜 추출 → 4. 중복 검사 → 5. 이동/복사
    /// </summary>
    public class ClassificationEngine
    {
        private static readonly TimeSpan ReportInterval = TimeSpan.FromMilliseconds(100);

        private readonly IDateExtractor _dateExtractor;
        private readonly DuplicateDetector _duplicateDetector;
        private readonly FileTransferService _transferService;
        private readonly Func<string, long?> _freeSpaceProvider;

        public ClassificationEngine(IDateExtractor dateExtractor = null, DuplicateDetector duplicateDetector = null,
            FileTransferService transferService = null, Func<string, long?> freeSpaceProvider = null)
        {
            _dateExtractor = dateExtractor ?? new ExifDateExtractor();
            _duplicateDetector = duplicateDetector ?? new DuplicateDetector();
            _transferService = transferService ?? new FileTransferService();
            _freeSpaceProvider = freeSpaceProvider ?? DiskSpace.GetAvailableFreeSpace;
        }

        public Task<ClassificationResult> RunAsync(AppSettings settings, IProgress<ProgressInfo> progress,
            Action<LogEntry> log, CancellationToken cancellationToken)
        {
            return Task.Run(() => Run(settings, progress, log, cancellationToken), CancellationToken.None);
        }

        public ClassificationResult Run(AppSettings settings, IProgress<ProgressInfo> progress, Action<LogEntry> log,
            CancellationToken cancellationToken)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            settings = settings.Clone();
            log = log ?? (_ => { });

            var result = new ClassificationResult { StartTime = DateTime.Now };
            var reporter = new Reporter(progress, result.StartTime);
            var sources = new List<string>();
            var movedFromFolders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            try
            {
                var validation = FolderValidator.Validate(settings);
                if (!validation.IsValid)
                    throw new CriticalOperationException(string.Join(Environment.NewLine, validation.Errors));

                var target = PathUtil.Normalize(settings.TargetFolder);
                result.TargetFolder = target;
                result.DuplicatesFolder = DuplicateCleaner.GetFolder(target);

                try
                {
                    Directory.CreateDirectory(target);
                }
                catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
                {
                    result.HadPermissionErrors = ex is UnauthorizedAccessException;
                    throw new CriticalOperationException($"대상 폴더 생성 실패: {ex.Message}", ex);
                }

                log(new LogEntry(LogLevel.Info,
                    $"작업 시작 - {(settings.Operation == FileOperation.Move ? "이동" : "복사")}, " +
                    $"{(settings.Format == ClassificationFormat.YearMonthDay ? AppSettings.YearMonthDayText : AppSettings.YearMonthText)}"));

                // 1. 스캔 (대상 폴더는 탐색에서 제외)
                reporter.BeginStage(ProcessingStage.Scanning, 0);
                sources = settings.SourceFolders.Select(PathUtil.Normalize).ToList();
                var scanned = 0;
                var allFiles = FileScanner.Scan(sources, new[] { target }, log,
                    path => reporter.Report(++scanned, path), cancellationToken);
                reporter.Report(scanned, null, force: true);
                log(new LogEntry(LogLevel.Info, $"소스 폴더 스캔 완료: {allFiles.Count}개 파일 발견"));

                // 2. 필터
                var filter = new ExtensionFilter(settings.IncludingExtensions, settings.BarringExtensions);
                reporter.BeginStage(ProcessingStage.Filtering, allFiles.Count);
                var items = new List<FileItem>();
                for (var i = 0; i < allFiles.Count; i++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var path = allFiles[i];
                    if (!filter.IsMatch(path))
                    {
                        result.Skipped++;
                    }
                    else if (IsHiddenOrSystem(path))
                    {
                        // Thumbs.db, desktop.ini 같은 Windows 숨김/시스템 파일은 대상에서 제외
                        result.Skipped++;
                        result.SkippedHiddenOrSystem++;
                    }
                    else
                    {
                        try
                        {
                            items.Add(new FileItem(path, new FileInfo(path).Length));
                        }
                        catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
                        {
                            var failedItem = new FileItem(path, 0) { Status = FileItemStatus.Failed };
                            items.Add(failedItem);
                            MarkFailed(result, failedItem, $"파일 정보를 읽을 수 없습니다: {ex.Message}", log,
                                ex is UnauthorizedAccessException);
                        }
                    }
                    reporter.Report(i + 1, path);
                }
                reporter.Report(allFiles.Count, null, force: true);
                result.TotalFiles = items.Count;
                log(new LogEntry(LogLevel.Info,
                    $"필터 적용: 대상 {items.Count}개, 건너뜀 {result.Skipped}개" +
                    (result.SkippedHiddenOrSystem > 0 ? $" (숨김/시스템 파일 {result.SkippedHiddenOrSystem}개 포함)" : string.Empty)));

                CheckFreeSpace(settings, items, target);

                // 3. 날짜 추출 (병렬)
                ExtractDates(items, result, reporter, log, cancellationToken);

                // 4. 중복 검사
                reporter.BeginStage(ProcessingStage.CheckingDuplicates, 0);
                var existing = CollectExistingFiles(target, log, cancellationToken);
                var hashTotal = DuplicateDetector.CountHashCandidates(items, existing);
                reporter.BeginStage(ProcessingStage.CheckingDuplicates, hashTotal);
                var hashed = 0;
                var hashFailures = 0;
                var duplicateCount = _duplicateDetector.Detect(items, existing,
                    path => reporter.Report(Interlocked.Increment(ref hashed), path),
                    entry =>
                    {
                        if (entry.Level == LogLevel.Error)
                            Interlocked.Increment(ref hashFailures);
                        log(entry);
                    },
                    cancellationToken);
                reporter.Report(hashTotal, null, force: true);
                result.Failed += hashFailures;
                log(new LogEntry(LogLevel.Info,
                    $"중복 검사 완료: 대상 폴더 기존 파일 {existing.Count}개와 비교, 중복 {duplicateCount}개"));

                // 5. 이동/복사
                Transfer(settings, target, items, result, reporter, log, movedFromFolders, cancellationToken);

                result.Status = RunStatus.Completed;
                reporter.BeginStage(ProcessingStage.Completed, items.Count, items.Count);
                log(new LogEntry(LogLevel.Info, result.CompletionMessage));
            }
            catch (OperationCanceledException)
            {
                result.Status = RunStatus.Cancelled;
                reporter.BeginStage(ProcessingStage.Cancelled, 0);
                log(new LogEntry(LogLevel.Warning, "사용자가 작업을 취소했습니다."));
            }
            catch (CriticalOperationException ex)
            {
                result.Status = RunStatus.Aborted;
                result.AbortReason = ex.Message;
                reporter.BeginStage(ProcessingStage.Failed, 0);
                log(new LogEntry(LogLevel.Error, $"작업 중단: {ex.Message}"));
            }
            catch (Exception ex)
            {
                result.Status = RunStatus.Aborted;
                result.AbortReason = $"예기치 않은 오류: {ex.Message}";
                reporter.BeginStage(ProcessingStage.Failed, 0);
                log(new LogEntry(LogLevel.Error, $"작업 중단 - {result.AbortReason}"));
            }

            // 이동 모드: 파일을 옮겨 낸 뒤 비게 된 소스 하위 폴더 삭제 (취소/중단된 경우에도 옮긴 만큼 정리)
            if (settings.Operation == FileOperation.Move && movedFromFolders.Count > 0)
            {
                result.RemovedEmptyFolders = EmptyFolderCleaner.RemoveEmptyFolders(movedFromFolders, sources, log);
                if (result.RemovedEmptyFolders > 0)
                    log(new LogEntry(LogLevel.Info, $"비어 있게 된 소스 폴더 {result.RemovedEmptyFolders}개를 삭제했습니다."));
            }

            result.EndTime = DateTime.Now;
            return result;
        }

        private void ExtractDates(List<FileItem> items, ClassificationResult result, Reporter reporter,
            Action<LogEntry> log, CancellationToken cancellationToken)
        {
            reporter.BeginStage(ProcessingStage.ExtractingDates, items.Count);
            var done = 0;
            var options = new ParallelOptions
            {
                CancellationToken = cancellationToken,
                MaxDegreeOfParallelism = Math.Max(1, Environment.ProcessorCount)
            };
            var sync = new object();

            Parallel.ForEach(items.Where(i => i.Status != FileItemStatus.Failed), options, item =>
            {
                var dateResult = _dateExtractor.Extract(item.SourcePath);
                lock (sync)
                {
                    if (dateResult.Success)
                    {
                        item.CaptureDate = dateResult.Date;
                        item.DateSource = dateResult.Source;
                        if (dateResult.IsFallback)
                            result.FallbackDates++;
                        if (!string.IsNullOrEmpty(dateResult.Warning))
                            log(new LogEntry(LogLevel.Warning, dateResult.Warning, item.SourcePath));
                    }
                    else
                    {
                        item.Status = FileItemStatus.Failed;
                        MarkFailed(result, item, dateResult.Error ?? "날짜 정보를 얻을 수 없습니다.", log,
                            dateResult.Error != null && dateResult.Error.Contains("권한"));
                    }
                }
                reporter.Report(Interlocked.Increment(ref done), item.SourcePath);
            });
            reporter.Report(items.Count, null, force: true);
        }

        private void Transfer(AppSettings settings, string target, List<FileItem> items, ClassificationResult result,
            Reporter reporter, Action<LogEntry> log, HashSet<string> movedFromFolders, CancellationToken cancellationToken)
        {
            var work = items.Where(i => i.Status != FileItemStatus.Failed).ToList();
            reporter.BeginStage(ProcessingStage.Transferring, work.Count);
            var verb = settings.Operation == FileOperation.Move ? "이동" : "복사";

            // 소스 내부 중복의 원본이 실패하면 그 중복 파일을 새 원본으로 승격한다 (데이터 손실 방지).
            var promoted = new Dictionary<FileItem, FileItem>();

            for (var i = 0; i < work.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var item = work[i];
                reporter.Report(i, item.SourcePath, force: true);

                if (item.DuplicateOfItem != null)
                {
                    var original = item.DuplicateOfItem;
                    while (promoted.TryGetValue(original, out var replacement))
                        original = replacement;

                    if (original.Status == FileItemStatus.Failed)
                    {
                        log(new LogEntry(LogLevel.Info,
                            "원본 파일 처리에 실패하여 이 파일을 중복이 아닌 원본으로 처리합니다.", item.SourcePath));
                        promoted[original] = item;
                        item.DuplicateOf = null;
                        item.DuplicateOfItem = null;
                    }
                    else
                    {
                        item.DuplicateOfItem = original;
                        item.DuplicateOf = original.DestinationPath ?? original.SourcePath;
                    }
                }

                var duplicate = item.IsDuplicate;
                var folder = TargetPathBuilder.BuildFolder(target, item.CaptureDate ?? DateTime.Now, settings.Format, duplicate);
                var outcome = _transferService.Transfer(item.SourcePath, folder, target, settings.Operation, item.Hash,
                    cancellationToken);

                if (outcome.PermissionDenied)
                    result.HadPermissionErrors = true;

                if (!outcome.Success)
                {
                    item.Status = FileItemStatus.Failed;
                    MarkFailed(result, item, outcome.Error, log, outcome.PermissionDenied);
                    continue;
                }

                item.DestinationPath = outcome.DestinationPath;
                if (settings.Operation == FileOperation.Move)
                    movedFromFolders.Add(Path.GetDirectoryName(item.SourcePath));
                if (outcome.Renamed)
                {
                    result.Renamed++;
                    log(new LogEntry(LogLevel.Warning,
                        $"같은 이름의 파일이 있어 '{Path.GetFileName(outcome.DestinationPath)}'(으)로 저장했습니다.",
                        item.SourcePath));
                }
                if (!string.IsNullOrEmpty(outcome.Warning))
                    log(new LogEntry(LogLevel.Warning, outcome.Warning, item.SourcePath));

                if (duplicate)
                {
                    item.Status = FileItemStatus.Duplicate;
                    result.Duplicates++;
                    log(new LogEntry(LogLevel.Duplicate,
                        $"중복 파일 ({item.DuplicateOf} 와 동일) → {outcome.DestinationPath}", item.SourcePath));
                }
                else
                {
                    item.Status = FileItemStatus.Transferred;
                    result.Transferred++;
                    log(new LogEntry(LogLevel.Success, $"{verb} 완료 → {outcome.DestinationPath}", item.SourcePath));
                }
            }

            reporter.Report(work.Count, null, force: true);
        }

        private void CheckFreeSpace(AppSettings settings, List<FileItem> items, string target)
        {
            long required = 0;
            foreach (var item in items)
            {
                if (item.Status == FileItemStatus.Failed)
                    continue;
                if (settings.Operation == FileOperation.Copy || !_transferService.IsSameVolume(item.SourcePath, target))
                    required += item.Size;
            }

            if (required == 0)
                return;

            var available = _freeSpaceProvider(target);
            if (available.HasValue && available.Value < required)
                throw new CriticalOperationException(
                    $"대상 드라이브의 여유 공간이 부족합니다. 필요: {DiskSpace.FormatBytes(required)}, " +
                    $"사용 가능: {DiskSpace.FormatBytes(available.Value)}");
        }

        /// <summary>대상 폴더(날짜 폴더 등)의 기존 파일 목록. _Duplicates 는 비교 대상에서 제외한다.</summary>
        private static List<ExistingFile> CollectExistingFiles(string target, Action<LogEntry> log,
            CancellationToken cancellationToken)
        {
            var paths = FileScanner.Scan(new[] { target }, new[] { DuplicateCleaner.GetFolder(target) }, log, null,
                cancellationToken);
            var existing = new List<ExistingFile>(paths.Count);
            foreach (var path in paths)
            {
                try
                {
                    existing.Add(new ExistingFile(path, new FileInfo(path).Length));
                }
                catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
                {
                    log(new LogEntry(LogLevel.Warning, $"대상 폴더의 기존 파일 정보를 읽을 수 없습니다: {ex.Message}", path));
                }
            }
            return existing;
        }

        private static bool IsHiddenOrSystem(string path)
        {
            try
            {
                return (File.GetAttributes(path) & (FileAttributes.Hidden | FileAttributes.System)) != 0;
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                // 속성을 읽지 못하면 대상에 포함하고, 이후 단계에서 오류로 처리되게 한다.
                return false;
            }
        }

        private static void MarkFailed(ClassificationResult result, FileItem item, string error, Action<LogEntry> log,
            bool permissionDenied)
        {
            item.Status = FileItemStatus.Failed;
            item.Error = error;
            result.Failed++;
            if (permissionDenied)
                result.HadPermissionErrors = true;
            log(new LogEntry(LogLevel.Error, error, item.SourcePath));
        }

        /// <summary>진행 상황 보고 (UI 과부하를 막기 위해 일정 간격으로만 보낸다).</summary>
        private sealed class Reporter
        {
            private readonly IProgress<ProgressInfo> _progress;
            private readonly DateTime _startTime;
            private readonly Stopwatch _sinceLastReport = Stopwatch.StartNew();
            private readonly object _sync = new object();
            private ProcessingStage _stage;
            private int _total;
            private DateTime _stageStart;

            public Reporter(IProgress<ProgressInfo> progress, DateTime startTime)
            {
                _progress = progress;
                _startTime = startTime;
            }

            public void BeginStage(ProcessingStage stage, int total, int processed = 0)
            {
                lock (_sync)
                {
                    _stage = stage;
                    _total = total;
                    _stageStart = DateTime.Now;
                }
                Report(processed, null, force: true);
            }

            public void Report(int processed, string currentPath, bool force = false)
            {
                if (_progress == null)
                    return;

                ProgressInfo info;
                lock (_sync)
                {
                    if (!force && _sinceLastReport.Elapsed < ReportInterval)
                        return;
                    _sinceLastReport.Restart();

                    info = new ProgressInfo
                    {
                        Stage = _stage,
                        Total = _total,
                        Processed = processed,
                        CurrentFilePath = currentPath,
                        CurrentFileName = currentPath == null ? null : Path.GetFileName(currentPath),
                        StartTime = _startTime,
                        StageStartTime = _stageStart
                    };
                }
                _progress.Report(info);
            }
        }
    }
}
