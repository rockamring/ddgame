using System;

namespace GameFramework.Network.Connection
{
    /// <summary>
    /// 网络数据包，包含消息ID和序列化后的消息体。
    /// 协议格式： [4字节:包长度] [2字节:消息ID] [消息体]
    /// </summary>
    public readonly struct Packet
    {
        /// <summary>
        /// 消息ID，标识协议类型
        /// </summary>
        public ushort MessageId { get; }

        /// <summary>
        /// 消息体（Protobuf序列化后的字节数据）
        /// </summary>
        public ReadOnlyMemory<byte> Body { get; }

        /// <summary>
        /// 包头长度（长度字段自身 + 消息ID字段）
        /// </summary>
        public const int HeaderSize = 6; // 4 (length) + 2 (messageId)

        public Packet(ushort messageId, byte[] body)
        {
            MessageId = messageId;
            Body = body;
        }

        public Packet(ushort messageId, ReadOnlyMemory<byte> body)
        {
            MessageId = messageId;
            Body = body;
        }
    }
}
