namespace PhotoClassy.Core.Settings
{
    /// <summary>날짜 분류 방식.</summary>
    public enum ClassificationFormat
    {
        /// <summary>년-월 (YYYY/MM)</summary>
        YearMonth,

        /// <summary>년-월-일 (YYYY/MM/DD)</summary>
        YearMonthDay
    }

    /// <summary>파일 처리 방식.</summary>
    public enum FileOperation
    {
        /// <summary>이동 (원본 삭제)</summary>
        Move,

        /// <summary>복사 (원본 유지)</summary>
        Copy
    }
}
