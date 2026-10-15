using System;
using PhotoClassy.Core.Dates;

namespace PhotoClassy.Core.Engine
{
    public enum FileItemStatus
    {
        Pending,
        Transferred,
        Duplicate,
        Failed
    }

    /// <summary>처리 대상 파일 하나의 상태.</summary>
    public class FileItem
    {
        public FileItem(string sourcePath, long size)
        {
            SourcePath = sourcePath;
            Size = size;
        }

        public string SourcePath { get; }

        public string FileName => System.IO.Path.GetFileName(SourcePath);

        public long Size { get; }

        public DateTime? CaptureDate { get; set; }

        public DateSource DateSource { get; set; }

        /// <summary>SHA256 (같은 크기의 다른 파일이 있을 때만 계산).</summary>
        public string Hash { get; set; }

        /// <summary>중복이면 원본(먼저 나온 소스 파일 또는 대상 폴더의 기존 파일) 경로.</summary>
        public string DuplicateOf { get; set; }

        /// <summary>중복의 원본이 이번 실행의 소스 파일이면 그 항목.</summary>
        public FileItem DuplicateOfItem { get; set; }

        public bool IsDuplicate => DuplicateOf != null;

        public FileItemStatus Status { get; set; } = FileItemStatus.Pending;

        public string DestinationPath { get; set; }

        public string Error { get; set; }
    }
}
