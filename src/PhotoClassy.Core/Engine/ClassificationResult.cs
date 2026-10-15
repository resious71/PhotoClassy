using System;

namespace PhotoClassy.Core.Engine
{
    public enum RunStatus
    {
        Completed,
        Cancelled,

        /// <summary>심각한 오류로 중단됨.</summary>
        Aborted
    }

    public class ClassificationResult
    {
        public RunStatus Status { get; set; }

        /// <summary>중단 사유 (Aborted 일 때).</summary>
        public string AbortReason { get; set; }

        /// <summary>필터를 통과한 처리 대상 파일 수.</summary>
        public int TotalFiles { get; set; }

        /// <summary>날짜 폴더로 이동/복사된 파일 수.</summary>
        public int Transferred { get; set; }

        /// <summary>_Duplicates 로 보낸 중복 파일 수.</summary>
        public int Duplicates { get; set; }

        public int Failed { get; set; }

        /// <summary>건너뛴 파일 수 (확장자 필터 + 숨김/시스템 파일).</summary>
        public int Skipped { get; set; }

        /// <summary>건너뛴 파일 중 숨김/시스템 파일 수.</summary>
        public int SkippedHiddenOrSystem { get; set; }

        /// <summary>같은 파일명이 있어 이름을 바꾼 파일 수.</summary>
        public int Renamed { get; set; }

        /// <summary>이동 모드에서 비게 되어 삭제한 소스 하위 폴더 수.</summary>
        public int RemovedEmptyFolders { get; set; }

        /// <summary>대체 날짜(수정일/생성일)를 사용한 파일 수.</summary>
        public int FallbackDates { get; set; }

        /// <summary>권한 오류가 있었는지 (관리자 권한 재실행 안내용).</summary>
        public bool HadPermissionErrors { get; set; }

        public DateTime StartTime { get; set; }

        public DateTime EndTime { get; set; }

        public TimeSpan Duration => EndTime - StartTime;

        /// <summary>실제로 처리(성공/중복/실패)된 파일 수.</summary>
        public int Processed => Transferred + Duplicates + Failed;

        public TimeSpan AveragePerFile => Processed == 0 ? TimeSpan.Zero : TimeSpan.FromTicks(Duration.Ticks / Processed);

        public string DuplicatesFolder { get; set; }

        public string TargetFolder { get; set; }

        public string CompletionMessage
        {
            get
            {
                switch (Status)
                {
                    case RunStatus.Cancelled:
                        return $"작업이 취소되었습니다. {Transferred + Duplicates}개 파일 처리됨.";
                    case RunStatus.Aborted:
                        return $"오류로 작업이 중단되었습니다: {AbortReason}";
                    default:
                        return Failed == 0
                            ? $"작업이 완료되었습니다. {Transferred + Duplicates}개 파일 처리됨."
                            : $"작업이 완료되었습니다. {Transferred + Duplicates}개 성공, {Failed}개 실패.";
                }
            }
        }
    }
}
