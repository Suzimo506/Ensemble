using System;
using System.Net.Sockets;
using System.Threading.Tasks;
using MDEN.Protocol;
using MDEN.Protocol.Envelopes;
using MelonLoader;

namespace MDEN.Network
{
    // TCP长连接客户端，负责与服务端通信
    public class NetworkClient
    {
        private static NetworkClient _instance;
        public static NetworkClient Instance => _instance ??= new NetworkClient();

        private TcpClient _tcpClient;
        private NetworkStream _stream;
        private bool _isConnected;
        private readonly PacketFramer _framer = new PacketFramer();

        // 当收到服务端的推送消息时触发
        public event Action<ushort, System.Text.Json.JsonElement> OnPushReceived;
        
        // 当断开连接时触发
        public event Action OnDisconnected;

        public async Task<bool> ConnectAsync(string host, int port)
        {
            try
            {
                _tcpClient = new TcpClient();
                await _tcpClient.ConnectAsync(host, port);
                _stream = _tcpClient.GetStream();
                _isConnected = true;
                
                // 启动后台接收循环
                _ = ReceiveLoopAsync();
                return true;
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Failed to connect to server: {ex.Message}");
                return false;
            }
        }

        public void Disconnect()
        {
            _isConnected = false;
            _stream?.Close();
            _tcpClient?.Close();
            OnDisconnected?.Invoke();
        }

        // 发送封包到服务端
        public async Task SendAsync(ClientEnvelope envelope)
        {
            if (!_isConnected || _stream == null) return;
            try
            {
                var bytes = _framer.Encode(envelope);
                await _stream.WriteAsync(bytes, 0, bytes.Length);
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Failed to send data: {ex.Message}");
                Disconnect();
            }
        }

        private async Task ReceiveLoopAsync()
        {
            var buffer = new byte[4096];
            try
            {
                while (_isConnected)
                {
                    int bytesRead = await _stream.ReadAsync(buffer, 0, buffer.Length);
                    if (bytesRead == 0)
                    {
                        MelonLogger.Msg("Server disconnected.");
                        Disconnect();
                        break;
                    }
                    _framer.AppendData(buffer, 0, bytesRead);
                    
                    while (_framer.TryDecode(out ServerEnvelope envelope))
                    {
                        if (envelope.ReqId == null)
                        {
                            OnPushReceived?.Invoke(envelope.Op, (System.Text.Json.JsonElement)envelope.Payload);
                        }
                        else
                        {
                            // 处理常规响应
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                if (_isConnected)
                {
                    MelonLogger.Error($"Receive loop exception: {ex.Message}");
                    Disconnect();
                }
            }
        }
    }
}
