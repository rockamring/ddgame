using System;
using System.Collections.Generic;
using GameFramework.Network.Connection;
using Google.Protobuf;

namespace GameFramework.Network.Messages
{
    /// <summary>
    /// 消息处理器接口（类型安全版本）
    /// </summary>
    /// <typeparam name="T">Protobuf消息类型</typeparam>
    public interface IMessageHandler<T>
    {
        void Handle(T message, Packet rawPacket);
    }

    /// <summary>
    /// 消息处理委托
    /// </summary>
    public delegate void MessageHandlerDelegate(Packet packet);

    /// <summary>
    /// 消息调度器。
    /// 根据消息ID将收到的数据包路由到对应的处理器。
    /// </summary>
    public class MessageDispatcher
    {
        private readonly Dictionary<ushort, MessageHandlerDelegate> _handlers = new();

        /// <summary>
        /// 注册消息处理器
        /// </summary>
        public void Register(ushort messageId, MessageHandlerDelegate handler)
        {
            if (handler == null)
                throw new ArgumentNullException(nameof(handler));

            if (_handlers.ContainsKey(messageId))
            {
                // 允许一个消息ID多处理器（合并委托）
                _handlers[messageId] += handler;
            }
            else
            {
                _handlers[messageId] = handler;
            }
        }

        /// <summary>
        /// 注册带Protobuf解析的消息处理器
        /// </summary>
        public void Register<T>(ushort messageId, Action<T> handler) where T : Google.Protobuf.IMessage, new()
        {
            Register(messageId, packet =>
            {
                var message = new T();
                message.MergeFrom(packet.Body.ToArray());
                handler(message);
            });
        }

        /// <summary>
        /// 注册实现了 IMessageHandler<T> 接口的处理器
        /// </summary>
        public void Register<T>(ushort messageId, IMessageHandler<T> handler) where T : Google.Protobuf.IMessage, new()
        {
            Register(messageId, packet =>
            {
                var message = new T();
                message.MergeFrom(packet.Body.ToArray());
                handler.Handle(message, packet);
            });
        }

        /// <summary>
        /// 注销消息处理器
        /// </summary>
        public void Unregister(ushort messageId)
        {
            _handlers.Remove(messageId);
        }

        /// <summary>
        /// 注销指定处理器的特定方法
        /// </summary>
        public void Unregister(ushort messageId, MessageHandlerDelegate handler)
        {
            if (_handlers.TryGetValue(messageId, out var existing))
            {
                existing -= handler;
                if (existing == null)
                {
                    _handlers.Remove(messageId);
                }
                else
                {
                    _handlers[messageId] = existing;
                }
            }
        }

        /// <summary>
        /// 派发数据包到对应的处理器
        /// </summary>
        /// <returns>是否找到了处理器</returns>
        public bool Dispatch(Packet packet)
        {
            if (_handlers.TryGetValue(packet.MessageId, out var handler))
            {
                try
                {
                    handler.Invoke(packet);
                    return true;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[MessageDispatcher] Handler error for message {packet.MessageId}: {ex.Message}");
                    return false;
                }
            }

            return false;
        }

        /// <summary>
        /// 检查是否存在某消息ID的处理器
        /// </summary>
        public bool HasHandler(ushort messageId)
        {
            return _handlers.ContainsKey(messageId);
        }

        /// <summary>
        /// 清空所有处理器
        /// </summary>
        public void Clear()
        {
            _handlers.Clear();
        }

        /// <summary>
        /// 获取已注册的处理器数量
        /// </summary>
        public int Count => _handlers.Count;
    }
}
