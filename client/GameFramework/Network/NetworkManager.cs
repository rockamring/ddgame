using System;
using GameFramework.Core.GameSystem;
using GameFramework.Network.Connection;
using GameFramework.Network.Messages;
using Google.Protobuf;

namespace GameFramework.Network
{
    /// <summary>
    /// 网络管理器，整合连接管理和消息分发。
    /// 作为 GameModule 注册到 GameApp 中。
    /// </summary>
    public class NetworkManager : GameModule
    {
        private readonly TcpConnection _connection = new();
        private readonly MessageDispatcher _dispatcher = new();

        public override string ModuleName => "NetworkManager";

        /// <summary>
        /// 底层TCP连接
        /// </summary>
        public TcpConnection Connection => _connection;

        /// <summary>
        /// 消息调度器
        /// </summary>
        public MessageDispatcher Dispatcher => _dispatcher;

        /// <summary>
        /// 连接状态
        /// </summary>
        public ConnectionState State => _connection.State;

        /// <summary>
        /// 连接状态变化事件
        /// </summary>
        public event Action<ConnectionState>? OnStateChanged;

        /// <summary>
        /// 连接出错事件
        /// </summary>
        public event Action<string>? OnError;

        protected override void OnInit()
        {
            _connection.OnPacketReceived += OnPacketReceived;
            _connection.OnStateChanged += OnConnectionStateChanged;
            _connection.OnError += OnConnectionError;
        }

        protected override void OnShutdown()
        {
            _connection.OnPacketReceived -= OnPacketReceived;
            _connection.OnStateChanged -= OnConnectionStateChanged;
            _connection.OnError -= OnConnectionError;
            _connection.Dispose();
            _dispatcher.Clear();
        }

        /// <summary>
        /// 连接到服务器
        /// </summary>
        public async System.Threading.Tasks.Task ConnectAsync(string host, int port)
        {
            await _connection.ConnectAsync(host, port);
        }

        /// <summary>
        /// 断开连接
        /// </summary>
        public async System.Threading.Tasks.Task DisconnectAsync()
        {
            await _connection.DisconnectAsync();
        }

        /// <summary>
        /// 发送消息（自动编码为Packet）
        /// </summary>
        public void Send(ushort messageId, Google.Protobuf.IMessage message)
        {
            var body = message.ToByteArray();
            var packet = new Packet(messageId, body);
            _connection.Send(packet);
        }

        /// <summary>
        /// 发送原始数据包
        /// </summary>
        public void Send(Packet packet)
        {
            _connection.Send(packet);
        }

        /// <summary>
        /// 注册消息处理器
        /// </summary>
        public void RegisterHandler(ushort messageId, MessageHandlerDelegate handler)
        {
            _dispatcher.Register(messageId, handler);
        }

        /// <summary>
        /// 注册带Protobuf解析的处理器
        /// </summary>
        public void RegisterHandler<T>(ushort messageId, Action<T> handler)
            where T : Google.Protobuf.IMessage, new()
        {
            _dispatcher.Register(messageId, handler);
        }

        /// <summary>
        /// 注册实现 IMessageHandler 接口的处理器
        /// </summary>
        public void RegisterHandler<T>(ushort messageId, IMessageHandler<T> handler)
            where T : Google.Protobuf.IMessage, new()
        {
            _dispatcher.Register(messageId, handler);
        }

        private void OnPacketReceived(Packet packet)
        {
            _dispatcher.Dispatch(packet);
        }

        private void OnConnectionStateChanged(ConnectionState state)
        {
            OnStateChanged?.Invoke(state);
        }

        private void OnConnectionError(string error)
        {
            Console.WriteLine($"[NetworkManager] Error: {error}");
            OnError?.Invoke(error);
        }
    }
}
