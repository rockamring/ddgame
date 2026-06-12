using GameFramework.Logging;
using UnityEngine;

namespace GameClient.Framework
{
    public sealed class UnityLogSink : ILogSink
    {
        public void Write(LogEntry entry)
        {
            var message = $"[{entry.Level}][{entry.Category}] {entry.Message}";
            if (entry.Exception != null)
                message += $"\n{entry.Exception}";

            switch (entry.Level)
            {
                case LogLevel.Warning:
                    Debug.LogWarning(message);
                    break;
                case LogLevel.Error:
                case LogLevel.Critical:
                    Debug.LogError(message);
                    break;
                default:
                    Debug.Log(message);
                    break;
            }
        }
    }
}
