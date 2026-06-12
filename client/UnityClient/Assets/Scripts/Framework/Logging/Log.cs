using System;

namespace GameFramework.Logging
{
    public static class Log
    {
        private static readonly object SyncRoot = new();
        private static LoggerManager? s_logger;

        public static LoggerManager? Current
        {
            get
            {
                lock (SyncRoot)
                {
                    return s_logger;
                }
            }
        }

        public static void SetLogger(LoggerManager logger)
        {
            lock (SyncRoot)
            {
                s_logger = logger ?? throw new ArgumentNullException(nameof(logger));
            }
        }

        public static void ClearLogger(LoggerManager logger)
        {
            lock (SyncRoot)
            {
                if (ReferenceEquals(s_logger, logger))
                    s_logger = null;
            }
        }

        public static void Trace(string message, string? category = null) => Write(LogLevel.Trace, message, category);
        public static void Debug(string message, string? category = null) => Write(LogLevel.Debug, message, category);
        public static void Info(string message, string? category = null) => Write(LogLevel.Info, message, category);
        public static void Warning(string message, string? category = null) => Write(LogLevel.Warning, message, category);
        public static void Error(string message, string? category = null, Exception? exception = null) => Write(LogLevel.Error, message, category, exception);
        public static void Critical(string message, string? category = null, Exception? exception = null) => Write(LogLevel.Critical, message, category, exception);

        public static void Write(LogLevel level, string message, string? category = null, Exception? exception = null)
        {
            var logger = Current;
            if (logger != null)
            {
                logger.Write(level, message, category, exception);
                return;
            }

            Console.WriteLine($"[{level}][{(string.IsNullOrWhiteSpace(category) ? "Game" : category)}] {message}");
            if (exception != null)
                Console.Error.WriteLine(exception);
        }
    }
}
