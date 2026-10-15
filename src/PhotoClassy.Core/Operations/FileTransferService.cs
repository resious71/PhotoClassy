using System;
using System.IO;
using System.Threading;
using PhotoClassy.Core.Duplicates;
using PhotoClassy.Core.IO;
using PhotoClassy.Core.Settings;

namespace PhotoClassy.Core.Operations
{
    public class TransferOutcome
    {
        public bool Success { get; set; }

        public string DestinationPath { get; set; }

        /// <summary>같은 파일명이 있어 _1, _2 등을 붙였는지.</summary>
        public bool Renamed { get; set; }

        /// <summary>성공했지만 알릴 내용 (예: 원본 삭제 실패).</summary>
        public string Warning { get; set; }

        public string Error { get; set; }

        public bool PermissionDenied { get; set; }
    }

    /// <summary>
    /// 파일 이동/복사.
    /// - 같은 드라이브 이동: 파일 시스템 이동
    /// - 다른 드라이브 이동: 복사 → 크기/해시 검증 → 성공 시에만 원본 삭제 (실패 시 원본 유지)
    /// - 복사: 원본 유지
    /// </summary>
    public class FileTransferService
    {
        private const int ErrorHandleDiskFull = 0x27;
        private const int ErrorDiskFull = 0x70;

        private readonly Func<string, string, bool> _isSameVolume;
        private readonly Func<string, CancellationToken, string> _hasher;

        public FileTransferService(Func<string, string, bool> isSameVolume = null,
            Func<string, CancellationToken, string> hasher = null)
        {
            _isSameVolume = isSameVolume ?? PathUtil.IsSameVolume;
            _hasher = hasher ?? FileHasher.ComputeSha256;
        }

        public bool IsSameVolume(string a, string b) => _isSameVolume(a, b);

        /// <param name="sourcePath">원본 파일.</param>
        /// <param name="destinationFolder">대상 날짜 폴더 (없으면 생성).</param>
        /// <param name="targetRoot">대상 루트 폴더 (사라졌는지 확인용).</param>
        /// <param name="knownSourceHash">이미 계산된 원본 해시 (없으면 null).</param>
        /// <exception cref="CriticalOperationException">디스크 가득 참 등 작업을 계속할 수 없을 때.</exception>
        public TransferOutcome Transfer(string sourcePath, string destinationFolder, string targetRoot,
            FileOperation operation, string knownSourceHash, CancellationToken cancellationToken)
        {
            var outcome = new TransferOutcome();
            string destination = null;
            var copied = false;

            try
            {
                try
                {
                    Directory.CreateDirectory(destinationFolder);
                }
                catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
                {
                    ThrowIfCritical(ex, targetRoot);
                    outcome.PermissionDenied = ex is UnauthorizedAccessException;
                    outcome.Error = $"대상 폴더 생성 실패: {ex.Message}";
                    return outcome;
                }

                destination = TargetPathBuilder.GetUniquePath(destinationFolder, Path.GetFileName(sourcePath), out var renamed);
                outcome.Renamed = renamed;
                outcome.DestinationPath = destination;

                if (operation == FileOperation.Copy)
                {
                    File.Copy(sourcePath, destination, false);
                    outcome.Success = true;
                    return outcome;
                }

                if (_isSameVolume(sourcePath, destination))
                {
                    File.Move(sourcePath, destination);
                    outcome.Success = true;
                    return outcome;
                }

                // 다른 드라이브 이동: 복사 후 검증
                File.Copy(sourcePath, destination, false);
                copied = true;

                if (!Verify(sourcePath, destination, knownSourceHash, cancellationToken, out var reason))
                {
                    TryDelete(destination);
                    outcome.DestinationPath = null;
                    outcome.Error = $"복사 검증 실패 ({reason}) - 원본을 유지합니다.";
                    return outcome;
                }

                outcome.Success = true;
                try
                {
                    var attributes = File.GetAttributes(sourcePath);
                    if ((attributes & FileAttributes.ReadOnly) != 0)
                        File.SetAttributes(sourcePath, attributes & ~FileAttributes.ReadOnly);
                    File.Delete(sourcePath);
                }
                catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
                {
                    outcome.PermissionDenied = ex is UnauthorizedAccessException;
                    outcome.Warning = $"복사는 완료되었지만 원본을 삭제하지 못했습니다: {ex.Message}";
                }
                return outcome;
            }
            catch (OperationCanceledException)
            {
                if (copied && !outcome.Success)
                    TryDelete(destination);
                throw;
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                if (copied && !outcome.Success)
                    TryDelete(destination);
                ThrowIfCritical(ex, targetRoot);

                outcome.Success = false;
                outcome.DestinationPath = null;
                outcome.PermissionDenied = ex is UnauthorizedAccessException;
                outcome.Error = ex is UnauthorizedAccessException
                    ? $"권한이 없습니다: {ex.Message}"
                    : $"{(operation == FileOperation.Move ? "이동" : "복사")} 실패: {ex.Message}";
                return outcome;
            }
        }

        private bool Verify(string source, string destination, string knownSourceHash, CancellationToken token,
            out string reason)
        {
            var sourceLength = new FileInfo(source).Length;
            var destinationLength = new FileInfo(destination).Length;
            if (sourceLength != destinationLength)
            {
                reason = $"크기 불일치 {sourceLength} ≠ {destinationLength}";
                return false;
            }

            var sourceHash = knownSourceHash ?? _hasher(source, token);
            var destinationHash = _hasher(destination, token);
            if (!string.Equals(sourceHash, destinationHash, StringComparison.OrdinalIgnoreCase))
            {
                reason = "SHA256 해시 불일치";
                return false;
            }

            reason = null;
            return true;
        }

        private static void ThrowIfCritical(Exception ex, string targetRoot)
        {
            var code = ex.HResult & 0xFFFF;
            if (ex is IOException && (code == ErrorDiskFull || code == ErrorHandleDiskFull))
                throw new CriticalOperationException("대상 드라이브의 공간이 부족합니다. 작업을 중단합니다.", ex);

            if (!string.IsNullOrEmpty(targetRoot) && !Directory.Exists(targetRoot))
                throw new CriticalOperationException($"대상 폴더에 접근할 수 없습니다: {targetRoot}", ex);
        }

        private static void TryDelete(string path)
        {
            if (string.IsNullOrEmpty(path))
                return;
            try
            {
                if (File.Exists(path))
                    File.Delete(path);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
            }
        }
    }
}
