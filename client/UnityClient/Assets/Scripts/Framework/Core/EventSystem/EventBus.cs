using System;

namespace GameFramework.Core.EventSystem
{
    /// <summary>
    /// 全局静态事件总线，提供便捷的静态访问入口。
    /// 内部封装 EventDispatcher 实现。
    /// </summary>
    public static class EventBus
    {
        private static readonly EventDispatcher s_dispatcher = new();

        /// <summary>
        /// 获取底层调度器实例（用于需要直接操作的场景）
        /// </summary>
        public static EventDispatcher Dispatcher => s_dispatcher;

        /// <summary>
        /// 注册事件监听
        /// </summary>
        public static void Register<T>(Action<T> callback, int priority = 0) where T : IEvent
        {
            s_dispatcher.Register(callback, priority);
        }

        /// <summary>
        /// 注册一次性事件监听
        /// </summary>
        public static void RegisterOnce<T>(Action<T> callback, int priority = 0) where T : IEvent
        {
            s_dispatcher.RegisterOnce(callback, priority);
        }

        /// <summary>
        /// 注销事件监听
        /// </summary>
        public static void Unregister<T>(Action<T> callback) where T : IEvent
        {
            s_dispatcher.Unregister(callback);
        }

        /// <summary>
        /// 派发事件
        /// </summary>
        public static void Dispatch<T>(T eventData) where T : IEvent
        {
            s_dispatcher.Dispatch(eventData);
        }

        /// <summary>
        /// 便捷派发：创建并派发一个携带数据的泛型事件
        /// </summary>
        public static void Dispatch<T>(T data, string eventName = "")
        {
            var evt = new GameEvent<T>(data);
            s_dispatcher.Dispatch(evt);
        }

        /// <summary>
        /// 清空所有事件
        /// </summary>
        public static void Clear()
        {
            s_dispatcher.Clear();
        }

        /// <summary>
        /// 清空指定类型事件
        /// </summary>
        public static void Clear<T>() where T : IEvent
        {
            s_dispatcher.Clear<T>();
        }
    }
}
