using System;

namespace PhotoClassy.Core.Logging
{
    public enum LogLevel
    {
        Info,
        Success,
        Warning,
        Error,
        Duplicate
    }

    public class LogEntry
    {
        public LogEntry(LogLevel level, string message, string path = null)
        {
            Time = DateTime.Now;
            Level = level;
            Message = message;
            Path = path;
        }

        public DateTime Time { get; }

        public LogLevel Level { get; }

        public string Message { get; }

        /// <summary>관련 파일 경로 (없으면 null).</summary>
        public string Path { get; }

        public string Icon
        {
            get
            {
                switch (Level)
                {
                    case LogLevel.Success: return "✅";
                    case LogLevel.Warning: return "⚠️";
                    case LogLevel.Error: return "❌";
                    case LogLevel.Duplicate: return "🔄";
                    default: return "ℹ️";
                }
            }
        }

        public override string ToString()
        {
            var text = $"[{Time:yyyy-MM-dd HH:mm:ss}] {Icon} {Message}";
            return string.IsNullOrEmpty(Path) ? text : $"{text} ({Path})";
        }
    }
}
