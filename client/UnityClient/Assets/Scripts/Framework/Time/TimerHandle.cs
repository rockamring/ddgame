namespace GameFramework.Time
{
    public readonly struct TimerHandle
    {
        private readonly TimerManager? _manager;

        internal TimerHandle(TimerManager manager, long id)
        {
            _manager = manager;
            Id = id;
        }

        public long Id { get; }
        public bool IsValid => _manager != null && Id > 0;
        public bool IsActive => _manager?.IsActive(Id) == true;

        public bool Cancel()
        {
            return _manager?.Cancel(Id) == true;
        }
    }
}
