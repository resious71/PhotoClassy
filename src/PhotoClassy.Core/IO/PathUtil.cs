using System;
using System.IO;

namespace PhotoClassy.Core.IO
{
    public static class PathUtil
    {
        /// <summary>전체 경로로 바꾸고 끝의 구분자를 제거한다 (루트는 "C:\" 형태 유지).</summary>
        public static string Normalize(string path)
        {
            var full = Path.GetFullPath(path.Trim());
            var trimmed = full.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (trimmed.Length == 0 || trimmed.EndsWith(":", StringComparison.Ordinal))
                return trimmed + Path.DirectorySeparatorChar;
            return trimmed;
        }

        public static bool AreSame(string a, string b)
        {
            return string.Equals(Normalize(a), Normalize(b), StringComparison.OrdinalIgnoreCase);
        }

        /// <summary><paramref name="path"/> 가 <paramref name="parent"/> 와 같거나 그 하위 경로이면 true.</summary>
        public static bool IsSameOrUnder(string path, string parent)
        {
            var p = WithTrailingSeparator(Normalize(path));
            var root = WithTrailingSeparator(Normalize(parent));
            return p.StartsWith(root, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>두 경로가 같은 볼륨(드라이브)에 있는지 판단한다.</summary>
        public static bool IsSameVolume(string a, string b)
        {
            var rootA = Path.GetPathRoot(Path.GetFullPath(a));
            var rootB = Path.GetPathRoot(Path.GetFullPath(b));
            return string.Equals(
                rootA?.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                rootB?.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                StringComparison.OrdinalIgnoreCase);
        }

        private static string WithTrailingSeparator(string path)
        {
            return path.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal)
                ? path
                : path + Path.DirectorySeparatorChar;
        }
    }
}
