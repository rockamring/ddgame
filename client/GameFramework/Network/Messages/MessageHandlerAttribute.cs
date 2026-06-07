using System;

namespace GameFramework.Network.Messages
{
    /// <summary>
    /// 消息处理器标记属性。
    /// 标记在处理方法上，指定其处理的消息ID。
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
    public class MessageHandlerAttribute : Attribute
    {
        /// <summary>
        /// 处理的消息ID
        /// </summary>
        public ushort MessageId { get; }

        /// <summary>
        /// 消息描述（可选）
        /// </summary>
        public string? Description { get; set; }

        public MessageHandlerAttribute(ushort messageId)
        {
            MessageId = messageId;
        }
    }
}
