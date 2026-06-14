using MDEN.Managers;
using MelonLoader;

namespace MDEN.Network
{
    // 负责断线重连逻辑
    public class ReconnectionManager
    {
        private static ReconnectionManager _instance;
        public static ReconnectionManager Instance => _instance ??= new ReconnectionManager();

        public void Init()
        {
            NetworkClient.Instance.OnDisconnected -= HandleDisconnect;
            NetworkClient.Instance.OnDisconnected += HandleDisconnect;
        }

        private void HandleDisconnect()
        {
            ConnectionManager.MarkDisconnectedByRemote();
            MelonLogger.Warning("Disconnected from server. Automatic reconnect is not enabled yet.");
        }
    }
}
