using System;

namespace GameFramework.Network.Connection
{
    /// <summary>
    /// 数据包编码解码器。
    /// 协议： [4字节:总包长(含包头)] [2字节:消息ID] [N字节:Protobuf消息体]
    /// 总包长 = 6 (HeaderSize) + Body.Length
    /// </summary>
    public static class PacketCodec
    {
        /// <summary>
        /// 编码数据包为字节流
        /// </summary>
        public static byte[] Encode(Packet packet)
        {
            var bodyLength = packet.Body.Length;
            var totalLength = Packet.HeaderSize + bodyLength;

            var buffer = new byte[totalLength];

            // 写入总包长度（大端序）
            buffer[0] = (byte)((totalLength >> 24) & 0xFF);
            buffer[1] = (byte)((totalLength >> 16) & 0xFF);
            buffer[2] = (byte)((totalLength >> 8) & 0xFF);
            buffer[3] = (byte)(totalLength & 0xFF);

            // 写入消息ID（大端序）
            buffer[4] = (byte)((packet.MessageId >> 8) & 0xFF);
            buffer[5] = (byte)(packet.MessageId & 0xFF);

            // 写入消息体
            if (bodyLength > 0)
            {
                Buffer.BlockCopy(packet.Body.ToArray(), 0, buffer, Packet.HeaderSize, bodyLength);
            }

            return buffer;
        }

        /// <summary>
        /// 尝试从缓冲区解码一个完整的数据包。
        /// </summary>
        /// <param name="buffer">接收缓冲区</param>
        /// <param name="offset">当前有效数据起始位置</param>
        /// <param name="length">当前有效数据长度</param>
        /// <param name="packet">解码出的数据包</param>
        /// <param name="consumed">此包消耗的字节数</param>
        /// <returns>是否成功解码出一个完整包</returns>
        public static bool TryDecode(byte[] buffer, int offset, int length, out Packet packet, out int consumed)
        {
            packet = default;
            consumed = 0;

            // 至少需要包头长度才能解析
            if (length < Packet.HeaderSize)
                return false;

            // 读取总包长度
            int totalLength = (buffer[offset] << 24) |
                              (buffer[offset + 1] << 16) |
                              (buffer[offset + 2] << 8) |
                              buffer[offset + 3];

            // 数据不足，等待更多
            if (length < totalLength)
                return false;

            // 读取消息ID
            ushort messageId = (ushort)((buffer[offset + 4] << 8) | buffer[offset + 5]);

            // 读取消息体
            int bodyLength = totalLength - Packet.HeaderSize;
            byte[] body = new byte[bodyLength];
            if (bodyLength > 0)
            {
                Buffer.BlockCopy(buffer, offset + Packet.HeaderSize, body, 0, bodyLength);
            }

            packet = new Packet(messageId, body);
            consumed = totalLength;
            return true;
        }
    }
}
