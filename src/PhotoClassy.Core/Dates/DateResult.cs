using System;

namespace PhotoClassy.Core.Dates
{
    public enum DateSource
    {
        None,

        /// <summary>EXIF DateTimeOriginal</summary>
        ExifDateTimeOriginal,

        /// <summary>EXIF DateTime (IFD0)</summary>
        ExifDateTime,

        /// <summary>파일 수정일 (대체)</summary>
        ModifiedDate,

        /// <summary>파일 생성일 (대체)</summary>
        CreatedDate
    }

    public class DateResult
    {
        public DateTime Date { get; set; }

        public DateSource Source { get; set; }

        /// <summary>대체 날짜를 사용했거나 EXIF 가 손상된 경우의 경고 메시지.</summary>
        public string Warning { get; set; }

        /// <summary>어떤 날짜도 얻지 못했을 때의 오류 메시지.</summary>
        public string Error { get; set; }

        public bool Success => Source != DateSource.None;

        public bool IsFallback => Source == DateSource.ModifiedDate || Source == DateSource.CreatedDate;
    }
}
