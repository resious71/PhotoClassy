using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using PhotoClassy.Core.IO;
using PhotoClassy.Core.Settings;

namespace PhotoClassy.Core.Validation
{
    public class FolderValidationResult
    {
        public List<string> Errors { get; } = new List<string>();

        /// <summary>대상 폴더 경로는 올바르지만 아직 존재하지 않는 경우 (생성 여부를 사용자에게 묻는다).</summary>
        public bool TargetMissing { get; set; }

        public bool IsValid => Errors.Count == 0;
    }

    /// <summary>작업 시작 전 폴더 유효성 및 겹침 검사.</summary>
    public static class FolderValidator
    {
        public static FolderValidationResult Validate(AppSettings settings)
        {
            var result = new FolderValidationResult();
            var sources = (settings.SourceFolders ?? new List<string>())
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .ToList();

            if (sources.Count == 0)
                result.Errors.Add("소스 폴더를 하나 이상 추가하세요.");

            var validSources = new List<string>();
            foreach (var source in sources)
            {
                if (!TryNormalize(source, out var normalized))
                {
                    result.Errors.Add($"소스 폴더 경로가 올바르지 않습니다: {source}");
                    continue;
                }
                if (!Directory.Exists(normalized))
                {
                    result.Errors.Add($"소스 폴더가 존재하지 않습니다: {source}");
                    continue;
                }
                validSources.Add(normalized);
            }

            string target = null;
            if (string.IsNullOrWhiteSpace(settings.TargetFolder))
            {
                result.Errors.Add("대상 폴더를 선택하세요.");
            }
            else if (!TryNormalize(settings.TargetFolder, out target))
            {
                result.Errors.Add($"대상 폴더 경로가 올바르지 않습니다: {settings.TargetFolder}");
                target = null;
            }
            else if (!Directory.Exists(target))
            {
                result.TargetMissing = true;
            }

            // 소스끼리 포함 관계 (같은 폴더 중복 포함)
            for (var i = 0; i < validSources.Count; i++)
            {
                for (var j = i + 1; j < validSources.Count; j++)
                {
                    var a = validSources[i];
                    var b = validSources[j];
                    if (PathUtil.AreSame(a, b))
                        result.Errors.Add($"같은 소스 폴더가 두 번 추가되었습니다: {a}");
                    else if (PathUtil.IsSameOrUnder(a, b))
                        result.Errors.Add($"소스 폴더가 다른 소스 폴더 안에 있습니다: {a} ⊂ {b}");
                    else if (PathUtil.IsSameOrUnder(b, a))
                        result.Errors.Add($"소스 폴더가 다른 소스 폴더 안에 있습니다: {b} ⊂ {a}");
                }
            }

            // 대상과 소스의 겹침
            if (target != null)
            {
                foreach (var source in validSources)
                {
                    if (PathUtil.AreSame(target, source))
                        result.Errors.Add($"대상 폴더가 소스 폴더와 같습니다: {source}");
                    else if (PathUtil.IsSameOrUnder(target, source))
                        result.Errors.Add($"대상 폴더가 소스 폴더 안에 있습니다: {target} ⊂ {source}");
                    else if (PathUtil.IsSameOrUnder(source, target))
                        result.Errors.Add($"소스 폴더가 대상 폴더 안에 있습니다: {source} ⊂ {target}");
                }
            }

            return result;
        }

        private static bool TryNormalize(string path, out string normalized)
        {
            try
            {
                normalized = PathUtil.Normalize(path);
                return true;
            }
            catch (Exception ex) when (ex is ArgumentException || ex is NotSupportedException || ex is PathTooLongException
                                       || ex is System.Security.SecurityException)
            {
                normalized = null;
                return false;
            }
        }
    }
}
