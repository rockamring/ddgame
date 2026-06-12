namespace GameFramework.Logging
{
    public interface ILogSink
    {
        void Write(LogEntry entry);
    }
}
