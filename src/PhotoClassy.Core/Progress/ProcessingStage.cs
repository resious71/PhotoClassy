namespace PhotoClassy.Core.Progress
{
    public enum ProcessingStage
    {
        Idle,
        Scanning,
        Filtering,
        ExtractingDates,
        CheckingDuplicates,
        Transferring,
        Completed,
        Failed,
        Cancelled
    }

    public static class ProcessingStageText
    {
        public static string ToDisplayText(this ProcessingStage stage)
        {
            switch (stage)
            {
                case ProcessingStage.Scanning: return "1/5 소스 폴더 스캔 중...";
                case ProcessingStage.Filtering: return "2/5 파일 필터링 중...";
                case ProcessingStage.ExtractingDates: return "3/5 날짜 정보 추출 중...";
                case ProcessingStage.CheckingDuplicates: return "4/5 중복 파일 검사 중...";
                case ProcessingStage.Transferring: return "5/5 파일 이동/복사 중...";
                case ProcessingStage.Completed: return "작업 완료!";
                case ProcessingStage.Failed: return "오류 발생!";
                case ProcessingStage.Cancelled: return "작업 취소됨";
                default: return "대기 중";
            }
        }
    }
}
