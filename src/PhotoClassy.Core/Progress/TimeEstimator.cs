using System;

namespace PhotoClassy.Core.Progress
{
    public class TimeEstimate
    {
        public TimeSpan Elapsed { get; set; }

        /// <summary>파일당 평균 처리 시간 (처리된 파일이 없으면 null).</summary>
        public TimeSpan? AveragePerFile { get; set; }

        /// <summary>예상 전체 작업 시간 = 전체 파일 수 × 평균.</summary>
        public TimeSpan? EstimatedTotal { get; set; }

        /// <summary>남은 작업 시간 = 남은 파일 수 × 평균.</summary>
        public TimeSpan? Remaining { get; set; }

        /// <summary>예상 완료 시각 = 현재 시각 + 남은 작업 시간.</summary>
        public DateTime? EstimatedCompletion { get; set; }
    }

    public static class TimeEstimator
    {
        public static TimeEstimate Compute(TimeSpan elapsed, int processed, int total, DateTime now)
        {
            var estimate = new TimeEstimate { Elapsed = elapsed };
            if (processed <= 0 || total <= 0)
                return estimate;

            var average = TimeSpan.FromTicks(elapsed.Ticks / processed);
            var remainingCount = Math.Max(0, total - processed);

            estimate.AveragePerFile = average;
            estimate.EstimatedTotal = TimeSpan.FromTicks(average.Ticks * total);
            estimate.Remaining = TimeSpan.FromTicks(average.Ticks * remainingCount);
            estimate.EstimatedCompletion = now + estimate.Remaining.Value;
            return estimate;
        }
    }
}
