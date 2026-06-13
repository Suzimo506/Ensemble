using System;
using System.Threading.Tasks;
using MelonLoader;

namespace MDEN.Network
{
    // 负责断线重连逻辑
    public class ReconnectionManager
    {
        private static ReconnectionManager _instance;
        public static ReconnectionManager Instance => _instance ??= new ReconnectionManager();

        private bool _isReconnecting;
        private const int MaxRetries = 5;

        public void Init()
        {
            NetworkClient.Instance.OnDisconnected -= HandleDisconnect;
            NetworkClient.Instance.OnDisconnected += HandleDisconnect;
        }

        private async void HandleDisconnect()
        {
            if (_isReconnecting) return;
            _isReconnecting = true;

            MelonLogger.Msg("Disconnect detected, preparing to reconnect...");

            for (int i = 1; i <= MaxRetries; i++)
            {
                await Task.Delay(2000); // 等待2秒
                MelonLogger.Msg($"Attempting to reconnect (Attempt {i})...");
                
                // 替换为实际配置的服务器地址
                bool success = await NetworkClient.Instance.ConnectAsync("127.0.0.1", 12345);
                if (success)
                {
                    MelonLogger.Msg("Reconnected successfully!");
                    _isReconnecting = false;
                    return;
                }
            }

            MelonLogger.Error("Reconnection failed. Please check your network or contact administrator.");
            _isReconnecting = false;
        }
    }
}
