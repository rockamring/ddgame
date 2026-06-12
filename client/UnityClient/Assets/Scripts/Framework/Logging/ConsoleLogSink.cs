using System;

namespace GameFramework.Logging
{
    public sealed class ConsoleLogSink : ILogSink
    {
        public void Write(LogEntry entry)
        {
            var line = $"[{entry.TimestampUtc:HH:mm:ss.fff}][{entry.Level}][{entry.Category}] {entry.Message}";
            if (entry.Level >= LogLevel.Error)
                Console.Error.WriteLine(line);
            else
                Console.WriteLine(line);

            if (entry.Exception != null)
                Console.Error.WriteLine(entry.Exception);
        }
    }
}
