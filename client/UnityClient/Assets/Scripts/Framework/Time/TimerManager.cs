using System;
using System.Collections.Generic;
using System.Linq;
using GameFramework.Core.GameSystem;
using GameFramework.Logging;

namespace GameFramework.Time
{
    public sealed class TimerManager : GameModule
    {
        private readonly object _syncRoot = new();
        private readonly Dictionary<long, TimerEntry> _timers = new();
        private long _nextId;

        public override string ModuleName => "TimerManager";

        public bool Paused { get; set; }
        public float TimeScale { get; set; } = 1f;
        public int Count
        {
            get
            {
                lock (_syncRoot)
                {
                    return _timers.Count;
                }
            }
        }

        public TimerHandle Schedule(float delay, Action callback, bool ignoreTimeScale = false)
        {
            if (callback == null)
                throw new ArgumentNullException(nameof(callback));

            return AddTimer(Math.Max(0f, delay), repeatInterval: 0f, repeatCount: 1,
                tickCallback: _ => callback(), ignoreTimeScale);
        }

        public TimerHandle Repeat(float interval, Action<int> callback, int repeatCount = -1, bool ignoreTimeScale = false)
        {
            if (callback == null)
                throw new ArgumentNullException(nameof(callback));
            if (interval <= 0f)
                throw new ArgumentOutOfRangeException(nameof(interval), "Repeat interval must be greater than zero.");
            if (repeatCount == 0 || repeatCount < -1)
                throw new ArgumentOutOfRangeException(nameof(repeatCount), "Repeat count must be -1 or greater than zero.");

            return AddTimer(interval, interval, repeatCount, callback, ignoreTimeScale);
        }

        public bool Cancel(TimerHandle handle)
        {
            return handle.IsValid && Cancel(handle.Id);
        }

        public bool Cancel(long id)
        {
            lock (_syncRoot)
            {
                return _timers.Remove(id);
            }
        }

        public bool IsActive(long id)
        {
            lock (_syncRoot)
            {
                return _timers.ContainsKey(id);
            }
        }

        public void Clear()
        {
            lock (_syncRoot)
            {
                _timers.Clear();
            }
        }

        protected override void OnUpdate(float deltaTime)
        {
            if (deltaTime < 0f)
                deltaTime = 0f;

            List<TimerEntry> dueTimers = new();
            lock (_syncRoot)
            {
                foreach (var timer in _timers.Values.ToList())
                {
                    var effectiveDeltaTime = timer.IgnoreTimeScale
                        ? deltaTime
                        : Paused ? 0f : deltaTime * Math.Max(0f, TimeScale);

                    timer.Remaining -= effectiveDeltaTime;
                    if (timer.Remaining <= 0f)
                        dueTimers.Add(timer);
                }
            }

            foreach (var timer in dueTimers)
            {
                InvokeTimer(timer);
            }
        }

        protected override void OnShutdown()
        {
            Clear();
        }

        private TimerHandle AddTimer(
            float delay,
            float repeatInterval,
            int repeatCount,
            Action<int> tickCallback,
            bool ignoreTimeScale)
        {
            lock (_syncRoot)
            {
                var id = ++_nextId;
                _timers[id] = new TimerEntry(id, delay, repeatInterval, repeatCount, tickCallback, ignoreTimeScale);
                return new TimerHandle(this, id);
            }
        }

        private void InvokeTimer(TimerEntry timer)
        {
            lock (_syncRoot)
            {
                if (!_timers.ContainsKey(timer.Id))
                    return;
            }

            timer.InvokeCount++;
            try
            {
                timer.Callback(timer.InvokeCount);
            }
            catch (Exception ex)
            {
                Log.Error($"Timer callback failed. TimerId={timer.Id}", "Timer", ex);
            }

            lock (_syncRoot)
            {
                if (!_timers.ContainsKey(timer.Id))
                    return;

                if (timer.RepeatCount > 0 && timer.InvokeCount >= timer.RepeatCount)
                {
                    _timers.Remove(timer.Id);
                    return;
                }

                if (timer.RepeatInterval <= 0f)
                {
                    _timers.Remove(timer.Id);
                    return;
                }

                timer.Remaining += timer.RepeatInterval;
                if (timer.Remaining <= 0f)
                    timer.Remaining = timer.RepeatInterval;
            }
        }

        private sealed class TimerEntry
        {
            public TimerEntry(
                long id,
                float delay,
                float repeatInterval,
                int repeatCount,
                Action<int> callback,
                bool ignoreTimeScale)
            {
                Id = id;
                Remaining = delay;
                RepeatInterval = repeatInterval;
                RepeatCount = repeatCount;
                Callback = callback;
                IgnoreTimeScale = ignoreTimeScale;
            }

            public long Id { get; }
            public float Remaining { get; set; }
            public float RepeatInterval { get; }
            public int RepeatCount { get; }
            public int InvokeCount { get; set; }
            public Action<int> Callback { get; }
            public bool IgnoreTimeScale { get; }
        }
    }
}
