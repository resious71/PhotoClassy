using System;
using System.IO;
using System.Runtime.InteropServices;

namespace PhotoClassy.Core.IO
{
    public static class DiskSpace
    {
        /// <summary>
        /// 경로가 속한 드라이브의 사용 가능한 여유 공간(바이트). 확인할 수 없으면 null.
        /// UNC 경로도 지원하기 위해 Windows 에서는 GetDiskFreeSpaceEx 를 우선 사용한다.
        /// </summary>
        public static long? GetAvailableFreeSpace(string path)
        {
            try
            {
                var full = Path.GetFullPath(path);

                if (Environment.OSVersion.Platform == PlatformID.Win32NT)
                {
                    var directory = full;
                    while (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                        directory = Path.GetDirectoryName(directory);

                    if (!string.IsNullOrEmpty(directory)
                        && GetDiskFreeSpaceEx(directory, out var available, out _, out _))
                        return (long)available;
                }

                var root = Path.GetPathRoot(full);
                if (string.IsNullOrEmpty(root))
                    return null;
                return new DriveInfo(root).AvailableFreeSpace;
            }
            catch (Exception ex) when (ex is IOException || ex is ArgumentException
                                       || ex is UnauthorizedAccessException || ex is DllNotFoundException
                                       || ex is EntryPointNotFoundException)
            {
                return null;
            }
        }

        public static string FormatBytes(long bytes)
        {
            string[] units = { "B", "KB", "MB", "GB", "TB" };
            double value = bytes;
            var unit = 0;
            while (value >= 1024 && unit < units.Length - 1)
            {
                value /= 1024;
                unit++;
            }
            return unit == 0 ? $"{bytes} B" : $"{value:0.##} {units[unit]}";
        }

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetDiskFreeSpaceEx(
            string lpDirectoryName,
            out ulong lpFreeBytesAvailable,
            out ulong lpTotalNumberOfBytes,
            out ulong lpTotalNumberOfFreeBytes);
    }
}
