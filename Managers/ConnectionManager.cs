using System;
using System.Threading;
using System.Threading.Tasks;
using MDEN.Network;
using MDEN.Protocol;
using MDEN.Protocol.Messages.Auth;
using MDEN.UI.Core;
using MelonLoader;

namespace MDEN.Managers
{
    public static class ConnectionManager
    {
        public static string CurrentServerAddress { get; private set; }
        public static string CurrentServerDisplayName { get; private set; }
        public static bool CurrentServerIsOfficial { get; private set; }
        public static string SessionToken { get; private set; }
        public static bool IsLoggedIn { get; private set; }
        private static readonly SemaphoreSlim ConnectionSemaphore = new SemaphoreSlim(1, 1);

        public static async Task<LoginResponse> ConnectAndLoginAsync(string address, string serverDisplayName = null, bool isOfficialServer = false)
        {
            return await ConnectAndLoginAsync(address, serverDisplayName, isOfficialServer, false);
        }

        public static async Task<bool> ReconnectToCurrentServerAsync()
        {
            if (string.IsNullOrWhiteSpace(CurrentServerAddress))
            {
                return false;
            }

            try
            {
                await ConnectAndLoginAsync(CurrentServerAddress, CurrentServerDisplayName, CurrentServerIsOfficial, true);
                await LobbyManager.RefreshLobbiesAfterReconnectAsync();
                return true;
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Reconnect failed: {ex.Message}");
                return false;
            }
        }

        private static async Task<LoginResponse> ConnectAndLoginAsync(string address, string serverDisplayName, bool isOfficialServer, bool isReconnect)
        {
            var endpoint = ParseAddress(address);
            var account = GameAccountManager.GetCurrentAccount();
            var configuredName = ModConfigManager.PlayerName;
            var displayName = string.IsNullOrWhiteSpace(configuredName) || configuredName == "Player"
                ? account.Nickname
                : configuredName;
            var selection = GameAccountManager.GetCurrentSelection();

            await ConnectionSemaphore.WaitAsync();
            try
            {
                if (NetworkClient.Instance.IsConnected)
                {
                    NetworkClient.Instance.Disconnect();
                }

                ClearRuntimeSessionState();
                if (!isReconnect)
                {
                    CurrentServerAddress = null;
                    CurrentServerDisplayName = null;
                    CurrentServerIsOfficial = false;
                }

                var connected = await NetworkClient.Instance.ConnectAsync(endpoint.Host, endpoint.Port);
                if (!connected)
                {
                    throw new InvalidOperationException("Failed to connect to server.");
                }

                LoginResponse response;
                try
                {
                    response = await NetworkClient.Instance.SendRequestAsync<LoginRequest, LoginResponse>(
                        OpCodes.LoginReq,
                        new LoginRequest
                        {
                            Uid = account.Uid,
                            Name = displayName,
                            IsReconnect = isReconnect
                        });
                }
                catch
                {
                    NetworkClient.Instance.Disconnect();
                    ClearRuntimeSessionState();
                    throw;
                }

                PlayerManager.SetCurrentIdentity(account.Uid, displayName);
                SessionToken = response.Token;
                IsLoggedIn = true;
                CurrentServerAddress = $"{endpoint.Host}:{endpoint.Port}";
                CurrentServerDisplayName = string.IsNullOrWhiteSpace(serverDisplayName)
                    ? CurrentServerAddress
                    : serverDisplayName.Trim();
                CurrentServerIsOfficial = isOfficialServer;
                _ = SyncLocalProfileAsync();
                PlayerManager.SyncSelectionFireAndForget(selection);
                PlayerManager.SyncChartStateFireAndForget();
                MainThreadDispatcher.Enqueue(NavigationButton.RefreshServerLabel);
                return response;
            }
            finally
            {
                ConnectionSemaphore.Release();
            }
        }

        public static void Disconnect()
        {
            CurrentServerAddress = null;
            CurrentServerDisplayName = null;
            CurrentServerIsOfficial = false;
            ClearRuntimeSessionState();
            NetworkClient.Instance.Disconnect();
            MainThreadDispatcher.Enqueue(NavigationButton.RefreshServerLabel);
        }

        public static void MarkDisconnectedByRemote()
        {
            ClearRuntimeSessionState();
            MainThreadDispatcher.Enqueue(NavigationButton.RefreshServerLabel);
        }

        private static void ClearRuntimeSessionState()
        {
            IsLoggedIn = false;
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
