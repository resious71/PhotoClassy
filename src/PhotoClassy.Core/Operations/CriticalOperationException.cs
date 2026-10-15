using System;

namespace PhotoClassy.Core.Operations
{
    /// <summary>작업 전체를 중단해야 하는 심각한 오류 (디스크 가득 참, 대상 폴더 사라짐 등).</summary>
    public class CriticalOperationException : Exception
    {
        public CriticalOperationException(string message, Exception inner = null) : base(message, inner)
        {
        }
    }
}
