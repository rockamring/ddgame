namespace GameFramework.Core.EventSystem
{
    /// <summary>
    /// 泛型事件，携带特定类型的数据
    /// </summary>
    /// <typeparam name="T">事件数据类型</typeparam>
    public readonly struct GameEvent<T> : IEvent
    {
        public T Data { get; }

        public GameEvent(T data)
        {
            Data = data;
        }

        public static GameEvent<T> Create(T data) => new(data);
    }
}
