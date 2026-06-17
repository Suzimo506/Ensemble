using System.Threading.Tasks;
using MDEN.Managers;
using MelonLoader;

namespace MDEN.Network
{
    // 负责断线重连逻辑
    public class ReconnectionManager
    {
        private static ReconnectionManager _instance;
        public static ReconnectionManager Instance => _instance ??= new ReconnectionManager();
        private const int MaxReconnectAttempts = 8;
        private const int BaseReconnectDelayMs = 1000;
        private const int MaxReconnectDelayMs = 30000;
        private readonly System.Random _random = new System.Random();
        private bool _isReconnecting;

        public void Init()
        {
            NetworkClient.Instance.OnDisconnected -= HandleDisconnect;
            NetworkClient.Instance.OnDisconnected += HandleDisconnect;
        }

        private void HandleDisconnect()
        {
            ConnectionManager.MarkDisconnectedByRemote();
            StartReconnectLoop();
        }

        private void StartReconnectLoop()
        {
            if (_isReconnecting) return;

            _isReconnecting = true;
            _ = ReconnectLoopAsync();
        }

        private async Task ReconnectLoopAsync()
        {
            try
            {
                for (var attempt = 1; attempt <= MaxReconnectAttempts; attempt++)
                {
                    MelonLogger.Warning($"Disconnected from server. Reconnect attempt {attempt}/{MaxReconnectAttempts}...");

                    await DelayBeforeAttemptAsync(attempt);
                    if (NetworkClient.Instance.IsConnected)
                    {
                        return;
                    }

                    if (await ConnectionManager.ReconnectToCurrentServerAsync())
                    {
                        MelonLogger.Msg("Reconnected to server.");
                        return;
                    }
                }

                MelonLogger.Warning("Reconnect attempts exhausted.");
            }
            finally
            {
                _isReconnecting = false;
            }
        }

        private Task DelayBeforeAttemptAsync(int attempt)
        {
            var exponentialDelay = BaseReconnectDelayMs * (1 << System.Math.Min(attempt - 1, 5));
            var cappedDelay = System.Math.Min(exponentialDelay, MaxReconnectDelayMs);
            var jitterDelay = _random.Next(0, cappedDelay + 1);
            return Task.Delay(jitterDelay);
        }
    }
}
