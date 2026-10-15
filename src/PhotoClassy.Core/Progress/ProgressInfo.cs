using System;

namespace PhotoClassy.Core.Progress
{
    /// <summary>엔진이 UI 에 전달하는 진행 상황 스냅샷.</summary>
    public class ProgressInfo
    {
        public ProcessingStage Stage { get; set; }

        /// <summary>현재 단계에서 처리할 전체 항목 수 (스캔 단계처럼 미리 알 수 없으면 0).</summary>
        public int Total { get; set; }

        /// <summary>현재 단계에서 처리된 항목 수.</summary>
        public int Processed { get; set; }

        public string CurrentFileName { get; set; }

        public string CurrentFilePath { get; set; }

        /// <summary>작업 전체 시작 시각.</summary>
        public DateTime StartTime { get; set; }

        /// <summary>현재 단계 시작 시각 (남은 시간 추정에 사용).</summary>
        public DateTime StageStartTime { get; set; }

        public int Remaining => Math.Max(0, Total - Processed);

        public double Percent => Total <= 0 ? 0 : Math.Min(100.0, Processed * 100.0 / Total);
    }
}
