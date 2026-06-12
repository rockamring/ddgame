using System;
using System.Collections.Generic;
using System.Linq;
using GameFramework.Core.GameSystem;

namespace GameFramework.Logging
{
    public sealed class LoggerManager : GameModule
    {
        private readonly object _syncRoot = new();
        private readonly List<ILogSink> _sinks = new();

        public override string ModuleName => "LoggerManager";

        public LogLevel MinLevel { get; set; } = LogLevel.Info;

        public IReadOnlyList<ILogSink> Sinks
        {
            get
            {
                lock (_syncRoot)
                {
                    return _sinks.ToList();
                }
            }
        }

        public void AddSink(ILogSink sink)
        {
            if (sink == null)
                throw new ArgumentNullException(nameof(sink));

            lock (_syncRoot)
            {
                if (!_sinks.Contains(sink))
                    _sinks.Add(sink);
            }
        }

        public void ClearSinks()
        {
            lock (_syncRoot)
            {
                _sinks.Clear();
            }
        }

        public void Trace(string message, string? category = null) => Write(LogLevel.Trace, message, category);
        public void Debug(string message, string? category = null) => Write(LogLevel.Debug, message, category);
        public void Info(string message, string? category = null) => Write(LogLevel.Info, message, category);
        public void Warning(string message, string? category = null) => Write(LogLevel.Warning, message, category);
        public void Error(string message, string? category = null, Exception? exception = null) => Write(LogLevel.Error, message, category, exception);
        public void Critical(string message, string? category = null, Exception? exception = null) => Write(LogLevel.Critical, message, category, exception);

        public void Write(LogLevel level, string message, string? category = null, Exception? exception = null)
        {
            if (level < MinLevel || MinLevel == LogLevel.Off)
                return;

            var entry = new LogEntry(level, message, category, exception);
            List<ILogSink> sinks;
            lock (_syncRoot)
            {
                sinks = _sinks.ToList();
            }

            foreach (var sink in sinks)
            {
                sink.Write(entry);
            }
        }

        protected override void OnInit()
        {
            lock (_syncRoot)
            {
                if (_sinks.Count == 0)
                    _sinks.Add(new ConsoleLogSink());
            }

            Log.SetLogger(this);
            Info("Logger initialized.", "Framework");
        }

        protected override void OnShutdown()
        {
            Info("Logger shutdown.", "Framework");
            Log.ClearLogger(this);
        }
    }
}
