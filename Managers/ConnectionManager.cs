using System;
using System.Threading.Tasks;
using MDEN.Network;
using MDEN.Protocol;
using MDEN.Protocol.Messages.Auth;

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

            if (NetworkClient.Instance.IsConnected)
            {
                NetworkClient.Instance.Disconnect();
            }

            var connected = await NetworkClient.Instance.ConnectAsync(endpoint.Host, endpoint.Port);
            if (!connected)
            {
                throw new InvalidOperationException("Failed to connect to server.");
            }

            var account = GameAccountManager.GetCurrentAccount();

            var response = await NetworkClient.Instance.SendRequestAsync<LoginRequest, LoginResponse>(
                OpCodes.LoginReq,
                new LoginRequest
                {
                    Uid = account.Uid,
                    Name = account.Nickname,
                    IsReconnect = false
                });

            PlayerManager.SetCurrentUid(account.Uid);
            SessionToken = response.Token;
            IsLoggedIn = true;
            CurrentServerAddress = $"{endpoint.Host}:{endpoint.Port}";
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
