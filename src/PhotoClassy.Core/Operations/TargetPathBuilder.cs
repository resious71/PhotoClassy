using System;
using System.Globalization;
using System.IO;
using PhotoClassy.Core.Duplicates;
using PhotoClassy.Core.Settings;

namespace PhotoClassy.Core.Operations
{
    public static class TargetPathBuilder
    {
        /// <summary>
        /// 날짜 폴더 경로. 예: 대상\2026\01 또는 대상\2026\01\15,
        /// 중복이면 대상\_Duplicates\2026\01[\15].
        /// </summary>
        public static string BuildFolder(string targetRoot, DateTime date, ClassificationFormat format, bool duplicate)
        {
            var root = duplicate ? DuplicateCleaner.GetFolder(targetRoot) : targetRoot;
            var path = Path.Combine(root,
                date.Year.ToString("0000", CultureInfo.InvariantCulture),
                date.Month.ToString("00", CultureInfo.InvariantCulture));

            if (format == ClassificationFormat.YearMonthDay)
                path = Path.Combine(path, date.Day.ToString("00", CultureInfo.InvariantCulture));

            return path;
        }

        /// <summary>
        /// 폴더에 같은 이름이 있으면 "이름_1.확장자", "이름_2.확장자" ... 로 사용 가능한 경로를 찾는다.
        /// </summary>
        public static string GetUniquePath(string folder, string fileName, out bool renamed)
        {
            var candidate = Path.Combine(folder, fileName);
            renamed = false;
            if (!File.Exists(candidate) && !Directory.Exists(candidate))
                return candidate;

            renamed = true;
            var name = Path.GetFileNameWithoutExtension(fileName);
            var extension = Path.GetExtension(fileName);
            for (var i = 1; ; i++)
            {
                candidate = Path.Combine(folder, $"{name}_{i}{extension}");
                if (!File.Exists(candidate) && !Directory.Exists(candidate))
                    return candidate;
            }
        }
    }
}
