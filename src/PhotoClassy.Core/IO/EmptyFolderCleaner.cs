using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using PhotoClassy.Core.Logging;

namespace PhotoClassy.Core.IO
{
    public static class EmptyFolderCleaner
    {
        /// <summary>
        /// 파일을 옮겨 낸 폴더들 중 비게 된 폴더를 삭제하고, 상위 폴더도 비었으면 따라 올라가며 삭제한다.
        /// 소스 폴더(<paramref name="roots"/>) 자체와 원래부터 비어 있던 다른 폴더는 건드리지 않는다.
        /// </summary>
        /// <param name="folders">파일을 옮겨 낸 폴더 목록.</param>
        /// <param name="roots">소스 폴더 목록 (삭제하지 않는 경계).</param>
        /// <returns>삭제한 폴더 수.</returns>
        public static int RemoveEmptyFolders(IEnumerable<string> folders, IEnumerable<string> roots, Action<LogEntry> log)
        {
            var rootList = roots.Select(PathUtil.Normalize).ToList();
            var removed = 0;

            // 깊은 폴더부터 처리해야 상위 폴더가 비었는지 정확히 판단할 수 있다.
            foreach (var start in folders.Select(PathUtil.Normalize).Distinct(StringComparer.OrdinalIgnoreCase)
                         .OrderByDescending(f => f.Length))
            {
                var current = start;
                while (current != null && IsStrictlyUnderAny(current, rootList))
                {
                    try
                    {
                        if (!Directory.Exists(current))
                        {
                            current = Path.GetDirectoryName(current);
                            continue;
                        }
                        if (Directory.EnumerateFileSystemEntries(current).Any())
                            break;

                        // 비재귀 삭제: 그 사이에 무언가 생겼다면 실패하므로 안전하다.
                        Directory.Delete(current, false);
                        removed++;
                    }
                    catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
                    {
                        log?.Invoke(new LogEntry(LogLevel.Warning, $"빈 폴더를 삭제하지 못했습니다: {ex.Message}", current));
                        break;
                    }
                    current = Path.GetDirectoryName(current);
                }
            }

            return removed;
        }

        private static bool IsStrictlyUnderAny(string folder, List<string> roots)
        {
            foreach (var root in roots)
                if (!PathUtil.AreSame(folder, root) && PathUtil.IsSameOrUnder(folder, root))
                    return true;
            return false;
        }
    }
}
