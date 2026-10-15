using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using PhotoClassy.Core.IO;
using PhotoClassy.Core.Logging;

namespace PhotoClassy.Core.Scanning
{
    /// <summary>소스 폴더를 재귀적으로 탐색하여 파일 목록을 수집한다.</summary>
    public static class FileScanner
    {
        /// <param name="roots">탐색할 폴더 목록.</param>
        /// <param name="excludedFolders">탐색에서 제외할 폴더 (대상 폴더 등). 하위 폴더도 모두 제외된다.</param>
        /// <param name="log">접근할 수 없는 폴더 등을 기록.</param>
        /// <param name="onFileFound">파일을 하나 찾을 때마다 호출 (진행 표시용).</param>
        public static List<string> Scan(IEnumerable<string> roots, IEnumerable<string> excludedFolders,
            Action<LogEntry> log, Action<string> onFileFound, CancellationToken cancellationToken)
        {
            var excluded = new List<string>();
            if (excludedFolders != null)
            {
                foreach (var folder in excludedFolders)
                    if (!string.IsNullOrWhiteSpace(folder))
                        excluded.Add(PathUtil.Normalize(folder));
            }

            var files = new List<string>();
            foreach (var root in roots)
            {
                var stack = new Stack<string>();
                stack.Push(PathUtil.Normalize(root));

                while (stack.Count > 0)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var directory = stack.Pop();

                    if (IsExcluded(directory, excluded))
                        continue;

                    string[] entries;
                    try
                    {
                        entries = System.IO.Directory.GetFiles(directory);
                    }
                    catch (Exception ex) when (ex is UnauthorizedAccessException || ex is IOException
                                               || ex is System.Security.SecurityException)
                    {
                        log?.Invoke(new LogEntry(LogLevel.Error, $"폴더를 읽을 수 없습니다: {ex.Message}", directory));
                        continue;
                    }

                    Array.Sort(entries, StringComparer.OrdinalIgnoreCase);
                    foreach (var file in entries)
                    {
                        files.Add(file);
                        onFileFound?.Invoke(file);
                    }

                    string[] subdirectories;
                    try
                    {
                        subdirectories = System.IO.Directory.GetDirectories(directory);
                    }
                    catch (Exception ex) when (ex is UnauthorizedAccessException || ex is IOException
                                               || ex is System.Security.SecurityException)
                    {
                        log?.Invoke(new LogEntry(LogLevel.Error, $"하위 폴더를 읽을 수 없습니다: {ex.Message}", directory));
                        continue;
                    }

                    Array.Sort(subdirectories, StringComparer.OrdinalIgnoreCase);
                    // 스택이므로 역순으로 넣어 이름순으로 탐색
                    for (var i = subdirectories.Length - 1; i >= 0; i--)
                    {
                        var sub = subdirectories[i];
                        if (IsReparsePoint(sub))
                        {
                            log?.Invoke(new LogEntry(LogLevel.Info, "바로 가기/정션 폴더는 건너뜁니다.", sub));
                            continue;
                        }
                        stack.Push(sub);
                    }
                }
            }

            return files;
        }

        private static bool IsExcluded(string directory, List<string> excluded)
        {
            foreach (var folder in excluded)
                if (PathUtil.IsSameOrUnder(directory, folder))
                    return true;
            return false;
        }

        private static bool IsReparsePoint(string path)
        {
            try
            {
                return (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0;
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                return false;
            }
        }
    }
}
