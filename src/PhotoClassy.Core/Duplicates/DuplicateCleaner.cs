using System;
using System.IO;

namespace PhotoClassy.Core.Duplicates
{
    public static class DuplicateCleaner
    {
        public const string FolderName = "_Duplicates";

        public static string GetFolder(string targetFolder) => Path.Combine(targetFolder, FolderName);

        /// <summary>_Duplicates 폴더의 모든 파일을 삭제한다. 삭제한 파일 수를 돌려준다.</summary>
        public static int DeleteAll(string targetFolder, out string error)
        {
            error = null;
            var folder = GetFolder(targetFolder);
            if (!Directory.Exists(folder))
                return 0;

            var deleted = 0;
            var failed = 0;
            foreach (var file in Directory.GetFiles(folder, "*", SearchOption.AllDirectories))
            {
                try
                {
                    File.SetAttributes(file, FileAttributes.Normal);
                    File.Delete(file);
                    deleted++;
                }
                catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
                {
                    failed++;
                    error = $"{failed}개 파일을 삭제하지 못했습니다. 마지막 오류: {ex.Message}";
                }
            }

            if (failed == 0)
            {
                try
                {
                    Directory.Delete(folder, true);
                }
                catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
                {
                    error = $"파일은 삭제했지만 _Duplicates 폴더를 제거하지 못했습니다: {ex.Message}";
                }
            }

            return deleted;
        }
    }
}
