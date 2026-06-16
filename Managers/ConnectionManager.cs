using System;
using System.Threading.Tasks;
using MDEN.Network;
using MDEN.Protocol;
using MDEN.Protocol.Messages.Auth;
using MelonLoader;

namespace MDEN.Managers
{
    public static class ConnectionManager
    {
        public static string CurrentServerAddress { get; private set; }
        public static string SessionToken { get; private set; }
        public static bool IsLoggedIn { get; private set; }

        public static async Task<LoginResponse> ConnectAndLoginAsync(string address)
        {
            var endpoint = ParseAddress(address);
            var account = GameAccountManager.GetCurrentAccount();
            var configuredName = ModConfigManager.PlayerName;
            var displayName = string.IsNullOrWhiteSpace(configuredName) || configuredName == "Player"
                ? account.Nickname
                : configuredName;
            var selection = GameAccountManager.GetCurrentSelection();

            if (NetworkClient.Instance.IsConnected)
            {
                NetworkClient.Instance.Disconnect();
            }

            var connected = await NetworkClient.Instance.ConnectAsync(endpoint.Host, endpoint.Port);
            if (!connected)
            {
                throw new InvalidOperationException("Failed to connect to server.");
            }

            var response = await NetworkClient.Instance.SendRequestAsync<LoginRequest, LoginResponse>(
                OpCodes.LoginReq,
                new LoginRequest
                {
                    Uid = account.Uid,
                    Name = displayName,
                    IsReconnect = false
                });

            PlayerManager.SetCurrentIdentity(account.Uid, displayName);
            SessionToken = response.Token;
            IsLoggedIn = true;
            CurrentServerAddress = $"{endpoint.Host}:{endpoint.Port}";
            _ = SyncLocalProfileAsync();
            PlayerManager.SyncSelectionFireAndForget(selection);
            PlayerManager.SyncChartStateFireAndForget();
            return response;
        }

        public static void Disconnect()
        {
            IsLoggedIn = false;
            CurrentServerAddress = null;
            SessionToken = null;
            PlayerManager.ClearSession();
            LobbyManager.ClearSession();
            NetworkClient.Instance.Disconnect();
        }

        public static void MarkDisconnectedByRemote()
        {
            IsLoggedIn = false;
            CurrentServerAddress = null;
            SessionToken = null;
            PlayerManager.ClearSession();
            LobbyManager.ClearSession();
        }

        private static ServerEndpoint ParseAddress(string address)
        {
            if (string.IsNullOrWhiteSpace(address))
            {
                throw new ArgumentException("Server address is empty.");
            }

            var parts = address.Trim().Split(':');
            var host = parts[0].Trim();
            var port = 10423;

            if (string.IsNullOrWhiteSpace(host))
            {
                throw new ArgumentException("Server address is empty.");
            }

            if (parts.Length > 1 && !int.TryParse(parts[1], out port))
            {
                throw new ArgumentException("Invalid server port.");
            }

            return new ServerEndpoint(host, port);
        }

        private static async Task SyncLocalProfileAsync()
        {
            try
            {
                await PlayerManager.SyncLocalProfileToServerAsync();
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Failed to sync local player profile: {ex.Message}");
            }
        }

        private readonly struct ServerEndpoint
        {
            public ServerEndpoint(string host, int port)
            {
                Host = host;
                Port = port;
            }

            public string Host { get; }
            public int Port { get; }
        }
    }
}
