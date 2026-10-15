using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using MetadataExtractor;
using MetadataExtractor.Formats.Exif;

namespace PhotoClassy.Core.Dates
{
    /// <summary>
    /// 촬영 날짜 추출기.
    /// 1) EXIF DateTimeOriginal → 2) EXIF DateTime → 3) 파일 수정일 → 4) 파일 생성일 순서로 사용한다.
    /// EXIF 에 기록된 날짜/시간을 그대로 사용하며 시간대(OffsetTime) 보정은 하지 않는다.
    /// </summary>
    public class ExifDateExtractor : IDateExtractor
    {
        private static readonly string[] ExifFormats =
        {
            "yyyy:MM:dd HH:mm:ss",
            "yyyy:MM:dd HH:mm:ss.fff",
            "yyyy:MM:dd HH:mm",
            "yyyy-MM-dd HH:mm:ss",
            "yyyy-MM-ddTHH:mm:ss",
            "yyyy/MM/dd HH:mm:ss",
            "yyyy:MM:dd",
            "yyyy-MM-dd"
        };

        private static readonly DateTime MinValidDate = new DateTime(1800, 1, 1);

        public DateResult Extract(string path)
        {
            string exifProblem = null;

            try
            {
                IReadOnlyList<MetadataExtractor.Directory> directories = ImageMetadataReader.ReadMetadata(path);

                if (TryGetExifDate(directories.OfType<ExifSubIfdDirectory>(), ExifDirectoryBase.TagDateTimeOriginal,
                        out var original, ref exifProblem))
                    return new DateResult { Date = original, Source = DateSource.ExifDateTimeOriginal };

                if (TryGetExifDate(directories.OfType<ExifIfd0Directory>(), ExifDirectoryBase.TagDateTime,
                        out var dateTime, ref exifProblem))
                    return new DateResult { Date = dateTime, Source = DateSource.ExifDateTime };
            }
            catch (ImageProcessingException)
            {
                // 지원하지 않는 형식(동영상 일부, 문서 등) → 대체 날짜 사용
            }
            catch (UnauthorizedAccessException ex)
            {
                return new DateResult { Error = $"파일 읽기 권한이 없습니다: {ex.Message}" };
            }
            catch (FileNotFoundException ex)
            {
                return new DateResult { Error = $"파일을 찾을 수 없습니다: {ex.Message}" };
            }
            catch (IOException ex)
            {
                exifProblem = $"EXIF 데이터를 읽을 수 없습니다 ({ex.Message})";
            }
            catch (Exception ex) when (!(ex is OutOfMemoryException))
            {
                // MetadataExtractor 는 손상된 파일에서 다양한 예외를 던질 수 있다.
                exifProblem = $"EXIF 데이터 손상 ({ex.GetType().Name}: {ex.Message})";
            }

            return Fallback(path, exifProblem);
        }

        internal static DateResult Fallback(string path, string exifProblem)
        {
            var prefix = string.IsNullOrEmpty(exifProblem) ? "EXIF 촬영 시간 없음" : exifProblem;

            try
            {
                var modified = File.GetLastWriteTime(path);
                if (IsValidFileSystemDate(modified))
                    return new DateResult
                    {
                        Date = modified,
                        Source = DateSource.ModifiedDate,
                        Warning = $"{prefix} → 수정일({modified:yyyy-MM-dd}) 사용"
                    };
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is ArgumentException
                                       || ex is NotSupportedException)
            {
            }

            try
            {
                var created = File.GetCreationTime(path);
                if (IsValidFileSystemDate(created))
                    return new DateResult
                    {
                        Date = created,
                        Source = DateSource.CreatedDate,
                        Warning = $"{prefix}, 수정일 읽기 실패 → 생성일({created:yyyy-MM-dd}) 사용"
                    };
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is ArgumentException
                                       || ex is NotSupportedException)
            {
            }

            return new DateResult { Error = $"{prefix}, 파일 수정일/생성일도 읽을 수 없습니다." };
        }

        /// <summary>EXIF 날짜 문자열을 파싱한다. 0000:00:00 같은 무효 값은 실패로 처리한다.</summary>
        public static bool TryParseExifDate(string value, out DateTime date)
        {
            date = default;
            if (string.IsNullOrWhiteSpace(value))
                return false;

            var text = value.Trim().TrimEnd('\0').Trim();

            // 시간대 접미사(예: +09:00, Z)는 무시하고 기록된 로컬 시각을 그대로 사용
            if (text.Length > 19 && (text[19] == '+' || text[19] == '-' || text[19] == 'Z'))
                text = text.Substring(0, 19);

            if (!DateTime.TryParseExact(text, ExifFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
                return false;

            if (!IsPlausible(parsed))
                return false;

            date = parsed;
            return true;
        }

        private static bool TryGetExifDate(IEnumerable<MetadataExtractor.Directory> directories, int tag, out DateTime date,
            ref string problem)
        {
            foreach (var directory in directories)
            {
                if (!directory.ContainsTag(tag))
                    continue;

                var raw = directory.GetString(tag);
                if (TryParseExifDate(raw, out date))
                    return true;

                if (problem == null)
                    problem = $"EXIF 날짜 형식이 유효하지 않음 ('{raw}')";
            }

            date = default;
            return false;
        }

        private static bool IsPlausible(DateTime date)
        {
            return date >= MinValidDate && date <= DateTime.Now.AddYears(1);
        }

        private static bool IsValidFileSystemDate(DateTime date)
        {
            // 파일이 없거나 읽을 수 없으면 1601-01-01 이 반환된다.
            return date.Year > 1601 && IsPlausible(date);
        }
    }
}
