using System;
using System.Globalization;

namespace PhotoClassy.Core.Progress
{
    public static class TimeFormatter
    {
        /// <summary>"시간:분:초" 형식. 예: 00:05:30, 02:15:45</summary>
        public static string Full(TimeSpan value)
        {
            if (value < TimeSpan.Zero) value = TimeSpan.Zero;
            return string.Format(CultureInfo.InvariantCulture, "{0:00}:{1:00}:{2:00}",
                (int)value.TotalHours, value.Minutes, value.Seconds);
        }

        /// <summary>짧은 형식. 1시간 미만은 "분:초"(05:30), 1시간 이상은 "시간:분"(2:15).</summary>
        public static string Short(TimeSpan value)
        {
            if (value < TimeSpan.Zero) value = TimeSpan.Zero;
            return value.TotalHours < 1
                ? string.Format(CultureInfo.InvariantCulture, "{0:00}:{1:00}", (int)value.TotalMinutes, value.Seconds)
                : string.Format(CultureInfo.InvariantCulture, "{0}:{1:00}", (int)value.TotalHours, value.Minutes);
        }

        /// <summary>한국어 서술형. 예: "5분 30초", "2시간 15분 45초", "0초"</summary>
        public static string Korean(TimeSpan value)
        {
            if (value < TimeSpan.Zero) value = TimeSpan.Zero;
            var hours = (int)value.TotalHours;
            if (hours > 0)
                return $"{hours}시간 {value.Minutes}분 {value.Seconds}초";
            if (value.Minutes > 0)
                return $"{value.Minutes}분 {value.Seconds}초";
            return $"{value.Seconds}초";
        }

        /// <summary>파일당 평균 처리 시간. 예: "2.64초/파일"</summary>
        public static string PerFile(TimeSpan value)
        {
            return string.Format(CultureInfo.InvariantCulture, "{0:0.00}초/파일", value.TotalSeconds);
        }

        /// <summary>예상 완료 시각. 예: "약 오후 3:45 완료 예상"</summary>
        public static string Completion(DateTime value, CultureInfo culture = null)
        {
            return $"약 {value.ToString("t", culture ?? CultureInfo.CurrentCulture)} 완료 예상";
        }
    }
}
