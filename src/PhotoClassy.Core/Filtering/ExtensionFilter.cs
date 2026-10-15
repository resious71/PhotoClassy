using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace PhotoClassy.Core.Filtering
{
    /// <summary>
    /// 포함/제외 확장자 규칙.
    /// - 둘 다 비어 있으면 모든 파일이 대상
    /// - 포함만 있으면 포함된 확장자만 대상
    /// - 제외만 있으면 제외된 확장자를 뺀 모든 파일이 대상
    /// - 같은 확장자가 양쪽에 있으면 제외가 우선
    /// 대소문자는 구분하지 않는다.
    /// </summary>
    public class ExtensionFilter
    {
        private static readonly char[] Separators = { ',', ';', ' ', '\t', '\r', '\n' };

        private readonly HashSet<string> _include;
        private readonly HashSet<string> _exclude;

        public ExtensionFilter(IEnumerable<string> include, IEnumerable<string> exclude)
        {
            _include = new HashSet<string>(Normalize(include), StringComparer.OrdinalIgnoreCase);
            _exclude = new HashSet<string>(Normalize(exclude), StringComparer.OrdinalIgnoreCase);
        }

        public static ExtensionFilter All { get; } = new ExtensionFilter(null, null);

        public IReadOnlyCollection<string> Include => _include;

        public IReadOnlyCollection<string> Exclude => _exclude;

        public bool IsMatch(string path)
        {
            var extension = Path.GetExtension(path) ?? string.Empty;

            if (_exclude.Contains(extension))
                return false;

            return _include.Count == 0 || _include.Contains(extension);
        }

        /// <summary>
        /// 텍스트 박스 입력("jpg, .PNG; tiff")을 정규화된 확장자 목록(".jpg", ".png", ".tiff")으로 변환한다.
        /// </summary>
        public static List<string> Parse(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return new List<string>();

            return Normalize(text.Split(Separators, StringSplitOptions.RemoveEmptyEntries)).ToList();
        }

        /// <summary>확장자 목록을 텍스트 박스 표시용 문자열로 변환한다.</summary>
        public static string Format(IEnumerable<string> extensions)
        {
            return extensions == null ? string.Empty : string.Join(", ", Normalize(extensions));
        }

        private static IEnumerable<string> Normalize(IEnumerable<string> extensions)
        {
            if (extensions == null)
                yield break;

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var raw in extensions)
            {
                if (string.IsNullOrWhiteSpace(raw))
                    continue;

                var value = raw.Trim().TrimStart('*').Trim().ToLowerInvariant();
                if (value.Length == 0 || value == ".")
                    continue;
                if (!value.StartsWith(".", StringComparison.Ordinal))
                    value = "." + value;

                if (seen.Add(value))
                    yield return value;
            }
        }
    }
}
