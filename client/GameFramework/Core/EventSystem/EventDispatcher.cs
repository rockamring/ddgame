using System;
using System.Collections.Generic;

namespace GameFramework.Core.EventSystem
{
    /// <summary>
    /// 事件分发器，负责事件注册、注销和派发。
    /// 支持优先级排序和一次性监听。
    /// </summary>
    public class EventDispatcher
    {
        private readonly Dictionary<Type, List<HandlerEntry>> _handlers = new();

        private readonly Queue<HandlerEntry> _pendingHandlers = new();
        private bool _isDispatching;

        /// <summary>
        /// 注册事件监听
        /// </summary>
        public void Register<T>(Action<T> callback, int priority = 0) where T : IEvent
        {
            if (callback == null)
                throw new ArgumentNullException(nameof(callback));

            var entry = new HandlerEntry
            {
                Callback = e => callback((T)e),
                OriginalCallback = callback,
                Priority = priority,
                IsOneShot = false,
                EventType = typeof(T)
            };

            if (_isDispatching)
            {
                _pendingHandlers.Enqueue(entry);
                return;
            }

            AddEntry(entry);
        }

        /// <summary>
        /// 注册一次性事件监听（触发一次后自动注销）
        /// </summary>
        public void RegisterOnce<T>(Action<T> callback, int priority = 0) where T : IEvent
        {
            if (callback == null)
                throw new ArgumentNullException(nameof(callback));

            var entry = new HandlerEntry
            {
                Callback = e => callback((T)e),
                OriginalCallback = callback,
                Priority = priority,
                IsOneShot = true,
                EventType = typeof(T)
            };

            if (_isDispatching)
            {
                _pendingHandlers.Enqueue(entry);
                return;
            }

            AddEntry(entry);
        }

        /// <summary>
        /// 注销事件监听（通过委托引用匹配移除）
        /// </summary>
        public void Unregister<T>(Action<T> callback) where T : IEvent
        {
            if (callback == null)
                throw new ArgumentNullException(nameof(callback));

            var type = typeof(T);
            if (!_handlers.TryGetValue(type, out var list))
                return;

            foreach (var entry in list)
            {
                if (Delegate.Equals(entry.OriginalCallback, callback))
                    entry.IsMarkedRemoved = true;
            }

            list.RemoveAll(e => e.IsMarkedRemoved);
        }

        /// <summary>
        /// 派发事件
        /// </summary>
        public void Dispatch<T>(T eventData) where T : IEvent
        {
            var type = typeof(T);
            if (!_handlers.TryGetValue(type, out var list) || list.Count == 0)
                return;

            _isDispatching = true;

            try
            {
                // 遍历前快照，防止过程中列表被修改
                var snapshot = list.ToArray();
                foreach (var entry in snapshot)
                {
                    if (entry.IsMarkedRemoved)
                        continue;

                    entry.Callback?.Invoke(eventData);

                    if (entry.IsOneShot)
                    {
                        entry.IsMarkedRemoved = true;
                    }
                }

                // 清理一次性监听
                list.RemoveAll(e => e.IsMarkedRemoved);
            }
            finally
            {
                _isDispatching = false;
                FlushPending();
            }
        }

        /// <summary>
        /// 清空所有事件监听
        /// </summary>
        public void Clear()
        {
            _handlers.Clear();
            _pendingHandlers.Clear();
        }

        /// <summary>
        /// 清空指定类型的所有监听
        /// </summary>
        public void Clear<T>() where T : IEvent
        {
            _handlers.Remove(typeof(T));
        }

        /// <summary>
        /// 获取某事件的监听数量
        /// </summary>
        public int ListenerCount<T>() where T : IEvent
        {
            var type = typeof(T);
            return _handlers.TryGetValue(type, out var list) ? list.Count : 0;
        }

        private void AddEntry(HandlerEntry entry)
        {
            var type = entry.EventType;
            if (!_handlers.TryGetValue(type, out var list))
            {
                list = new List<HandlerEntry>();
                _handlers[type] = list;
            }

            // 按优先级从大到小插入
            int idx = list.FindIndex(e => e.Priority < entry.Priority);
            if (idx >= 0)
                list.Insert(idx, entry);
            else
                list.Add(entry);
        }

        private void FlushPending()
        {
            while (_pendingHandlers.Count > 0)
            {
                var entry = _pendingHandlers.Dequeue();
                AddEntry(entry);
            }
        }

        private class HandlerEntry
        {
            public Action<IEvent>? Callback { get; set; }
            public Delegate? OriginalCallback { get; set; }
            public int Priority { get; set; }
            public bool IsOneShot { get; set; }
            public bool IsMarkedRemoved { get; set; }
            public Type EventType { get; set; } = null!;
        }
    }
}
