using System;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace GameFramework.Network.Connection
{
    /// <summary>
    /// TCP连接状态
    /// </summary>
    public enum ConnectionState
    {
        Disconnected,
        Connecting,
        Connected,
        Reconnecting,
    }

    /// <summary>
    /// 异步TCP连接管理类。
    /// 处理连接建立、数据收发、断线重连。
    /// </summary>
    public class TcpConnection : IDisposable
    {
        private Socket? _socket;
        private readonly byte[] _receiveBuffer = new byte[1024 * 64]; // 64KB接收缓冲区
        private readonly byte[] _packetBuffer = new byte[1024 * 1024]; // 1MB包缓冲区
        private int _packetOffset;
        private int _packetCount;
        private CancellationTokenSource? _cts;
        private bool _isDisposed;

        private string _host = "";
        private int _port;
        private int _reconnectAttempts;
        private int _maxReconnectAttempts = 5;
        private int _reconnectDelayMs = 2000;

        /// <summary>
        /// 连接状态变化事件
        /// </summary>
        public event Action<ConnectionState>? OnStateChanged;

        /// <summary>
        /// 收到数据包事件
        /// </summary>
        public event Action<Packet>? OnPacketReceived;

        /// <summary>
        /// 连接出错事件
        /// </summary>
        public event Action<string>? OnError;

        /// <summary>
        /// 当前连接状态
        /// </summary>
        public ConnectionState State { get; private set; } = ConnectionState.Disconnected;

        /// <summary>
        /// 是否已连接
        /// </summary>
        public bool IsConnected => State == ConnectionState.Connected && _socket?.Connected == true;

        /// <summary>
        /// 设置最大重连次数（0=不重连）
        /// </summary>
        public int MaxReconnectAttempts
        {
            get => _maxReconnectAttempts;
            set => _maxReconnectAttempts = Math.Max(0, value);
        }

        /// <summary>
        /// 重连延迟（毫秒）
        /// </summary>
        public int ReconnectDelayMs
        {
            get => _reconnectDelayMs;
            set => _reconnectDelayMs = Math.Max(100, value);
        }

        /// <summary>
        /// 异步连接到服务器
        /// </summary>
        public async Task ConnectAsync(string host, int port)
        {
            if (State == ConnectionState.Connected || State == ConnectionState.Connecting)
            {
                await DisconnectAsync();
            }

            _host = host;
            _port = port;
            _reconnectAttempts = 0;

            await EstablishConnection();
        }

        /// <summary>
        /// 发送数据包
        /// </summary>
        public void Send(Packet packet)
        {
            if (!IsConnected)
            {
                OnError?.Invoke("Cannot send: not connected.");
                return;
            }

            try
            {
                var data = PacketCodec.Encode(packet);
                _socket?.Send(data, 0, data.Length, SocketFlags.None);
            }
            catch (Exception ex)
            {
                OnError?.Invoke($"Send error: {ex.Message}");
                HandleDisconnect();
            }
        }

        /// <summary>
        /// 异步发送数据包
        /// </summary>
        public async Task SendAsync(Packet packet)
        {
            if (!IsConnected)
            {
                OnError?.Invoke("Cannot send: not connected.");
                return;
            }

            try
            {
                var data = PacketCodec.Encode(packet);
                await _socket!.SendAsync(data, SocketFlags.None);
            }
            catch (Exception ex)
            {
                OnError?.Invoke($"Send error: {ex.Message}");
                HandleDisconnect();
            }
        }

        /// <summary>
        /// 断开连接
        /// </summary>
        public async Task DisconnectAsync()
        {
            _cts?.Cancel();
            HandleDisconnect(raiseEvent: false);
            await Task.CompletedTask;
        }

        private async Task EstablishConnection()
        {
            SetState(ConnectionState.Connecting);

            try
            {
                _cts = new CancellationTokenSource();
                _socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp)
                {
                    NoDelay = true,
                    SendBufferSize = 8192,
                    ReceiveBufferSize = 65536,
                };

                await _socket.ConnectAsync(_host, _port);

                _packetOffset = 0;
                _packetCount = 0;
                SetState(ConnectionState.Connected);

                // 启动接收循环
                _ = ReceiveLoopAsync(_cts.Token);
            }
            catch (Exception ex)
            {
                OnError?.Invoke($"Connection failed: {ex.Message}");
                HandleDisconnect();

                if (_maxReconnectAttempts > 0)
                {
                    _ = ReconnectLoopAsync();
                }
            }
        }

        private async Task ReceiveLoopAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested && _socket?.Connected == true)
            {
                try
                {
                    var received = await _socket.ReceiveAsync(
                        new ArraySegment<byte>(_receiveBuffer), SocketFlags.None);

                    if (received <= 0)
                    {
                        HandleDisconnect();
                        break;
                    }

                    ProcessReceivedData(received);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    if (!token.IsCancellationRequested)
                    {
                        OnError?.Invoke($"Receive error: {ex.Message}");
                        HandleDisconnect();
                    }
                    break;
                }
            }
        }

        private void ProcessReceivedData(int receivedLength)
        {
            // 将接收到的数据追加到包缓冲区
            if (_packetOffset + receivedLength > _packetBuffer.Length)
            {
                OnError?.Invoke("Packet buffer overflow.");
                HandleDisconnect();
                return;
            }

            Buffer.BlockCopy(_receiveBuffer, 0, _packetBuffer, _packetOffset, receivedLength);
            _packetOffset += receivedLength;
            _packetCount += receivedLength;

            // 尝试解码所有完整包
            int consumed = 0;
            while (PacketCodec.TryDecode(_packetBuffer, consumed, _packetOffset - consumed,
                       out var packet, out int packetBytes))
            {
                OnPacketReceived?.Invoke(packet);
                consumed += packetBytes;
            }

            // 将剩余数据移到缓冲区头部
            if (consumed > 0)
            {
                int remaining = _packetOffset - consumed;
                if (remaining > 0)
                {
                    Buffer.BlockCopy(_packetBuffer, consumed, _packetBuffer, 0, remaining);
                }
                _packetOffset = remaining;
                _packetCount = remaining;
            }
        }

        private async Task ReconnectLoopAsync()
        {
            while (_reconnectAttempts < _maxReconnectAttempts && !_cts?.IsCancellationRequested == true)
            {
                _reconnectAttempts++;
                SetState(ConnectionState.Reconnecting);

                OnError?.Invoke($"Reconnecting... attempt {_reconnectAttempts}/{_maxReconnectAttempts}");

                try
                {
                    await Task.Delay(_reconnectDelayMs, _cts?.Token ?? CancellationToken.None);
                }
                catch (OperationCanceledException)
                {
                    break;
                }

                try
                {
                    _socket?.Close();
                    _socket?.Dispose();

                    _socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp)
                    {
                        NoDelay = true,
                    };

                    await _socket.ConnectAsync(_host, _port);

                    _packetOffset = 0;
                    _packetCount = 0;
                    _reconnectAttempts = 0;
                    SetState(ConnectionState.Connected);

                    _ = ReceiveLoopAsync(_cts?.Token ?? CancellationToken.None);
                    return;
                }
                catch
                {
                    // 重连失败，继续下一次
                }
            }

            // 重连耗尽
            OnError?.Invoke("Max reconnect attempts reached.");
            SetState(ConnectionState.Disconnected);
        }

        private void HandleDisconnect(bool raiseEvent = true)
        {
            if (_socket != null)
            {
                try
                {
                    _socket.Close();
                    _socket.Dispose();
                }
                catch { }
                _socket = null;
            }

            SetState(ConnectionState.Disconnected, raiseEvent);
        }

        private void SetState(ConnectionState newState, bool raiseEvent = true)
        {
            if (State == newState)
                return;

            State = newState;

            if (raiseEvent)
            {
                OnStateChanged?.Invoke(newState);
            }
        }

        public void Dispose()
        {
            if (_isDisposed)
                return;

            _isDisposed = true;
            _cts?.Cancel();
            _cts?.Dispose();

            if (_socket != null)
            {
                try
                {
                    _socket.Close();
                    _socket.Dispose();
                }
                catch { }
                _socket = null;
            }
        }
    }
}
