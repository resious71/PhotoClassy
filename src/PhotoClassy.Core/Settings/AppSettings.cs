using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace PhotoClassy.Core.Settings
{
    /// <summary>
    /// config.json 에 저장되는 사용자 설정.
    /// JSON 키 이름은 요구사항 명세서의 설정 파일 형식을 그대로 따른다.
    /// </summary>
    public class AppSettings
    {
        public const string YearMonthText = "YYYY/MM";
        public const string YearMonthDayText = "YYYY/MM/DD";

        [JsonProperty("sourceFolders")]
        public List<string> SourceFolders { get; set; } = new List<string>();

        [JsonProperty("targetFolder")]
        public string TargetFolder { get; set; } = string.Empty;

        [JsonIgnore]
        public ClassificationFormat Format { get; set; } = ClassificationFormat.YearMonth;

        [JsonIgnore]
        public FileOperation Operation { get; set; } = FileOperation.Move;

        [JsonProperty("classificationFormat")]
        public string ClassificationFormatText
        {
            get => Format == ClassificationFormat.YearMonthDay ? YearMonthDayText : YearMonthText;
            set => Format = string.Equals(value?.Trim(), YearMonthDayText, StringComparison.OrdinalIgnoreCase)
                ? ClassificationFormat.YearMonthDay
                : ClassificationFormat.YearMonth;
        }

        [JsonProperty("fileOperation")]
        public string FileOperationText
        {
            get => Operation == FileOperation.Copy ? "Copy" : "Move";
            set => Operation = string.Equals(value?.Trim(), "Copy", StringComparison.OrdinalIgnoreCase)
                ? FileOperation.Copy
                : FileOperation.Move;
        }

        [JsonProperty("includingExtensions")]
        public List<string> IncludingExtensions { get; set; } = new List<string>();

        [JsonProperty("BarringExtensions")]
        public List<string> BarringExtensions { get; set; } = new List<string>();

        /// <summary>기본값으로 채워진 새 설정.</summary>
        public static AppSettings CreateDefault() => new AppSettings();

        public AppSettings Clone()
        {
            return new AppSettings
            {
                SourceFolders = new List<string>(SourceFolders ?? new List<string>()),
                TargetFolder = TargetFolder,
                Format = Format,
                Operation = Operation,
                IncludingExtensions = new List<string>(IncludingExtensions ?? new List<string>()),
                BarringExtensions = new List<string>(BarringExtensions ?? new List<string>())
            };
        }
    }
}
