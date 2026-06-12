namespace GameFramework.Core.EventSystem
{
    /// <summary>
    /// 事件处理器接口，用于类级别的处理
    /// </summary>
    /// <typeparam name="T">事件类型</typeparam>
    public interface IEventHandler<T> where T : IEvent
    {
        void Handle(T eventData);
    }
}
