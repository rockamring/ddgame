using System;

namespace GameFramework.Logging
{
    public sealed class LogEntry
    {
        public LogEntry(LogLevel level, string message, string? category = null, Exception? exception = null)
        {
            TimestampUtc = DateTime.UtcNow;
            Level = level;
            Message = message ?? string.Empty;
            Category = string.IsNullOrWhiteSpace(category) ? "Game" : category.Trim();
            Exception = exception;
        }

        public DateTime TimestampUtc { get; }
        public LogLevel Level { get; }
        public string Message { get; }
        public string Category { get; }
        public Exception? Exception { get; }
    }
}
