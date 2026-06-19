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
    public enum ConnectionLifecycleState
    {
        Disconnected,
        Connecting,
        Connected,
        Reconnecting,
        ReconnectFailed
    }

    public static class ConnectionManager
    {
        public static string CurrentServerAddress { get; private set; }
        public static string CurrentServerDisplayName { get; private set; }
        public static bool CurrentServerIsOfficial { get; private set; }
        public static string SessionToken { get; private set; }
        public static bool IsLoggedIn { get; private set; }
        public static ConnectionLifecycleState State { get; private set; } = ConnectionLifecycleState.Disconnected;
        public static bool IsReconnecting => State == ConnectionLifecycleState.Reconnecting;
        public static bool CanSendRequests =>
            State == ConnectionLifecycleState.Connected &&
            IsLoggedIn &&
            NetworkClient.Instance.IsConnected;
        public static event Action<ConnectionLifecycleState> StateChanged;
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
            catch (ProtocolException ex)
            {
                MDEN.Managers.ClientLogManager.Warning($"Reconnect failed: {ex.Message}");
                MarkReconnectFailed();
                return false;
            }
            catch (Exception ex)
            {
                MDEN.Managers.ClientLogManager.Warning($"Reconnect failed: {ex.Message}");
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
                SetState(isReconnect ? ConnectionLifecycleState.Reconnecting : ConnectionLifecycleState.Connecting);

                if (NetworkClient.Instance.IsConnected)
                {
                    NetworkClient.Instance.Disconnect();
                }

                if (!isReconnect)
                {
                    ClearRuntimeSessionState();
                    CurrentServerAddress = null;
                    CurrentServerDisplayName = null;
                    CurrentServerIsOfficial = false;
                }

                var connected = await NetworkClient.Instance.ConnectAsync(endpoint.Host, endpoint.Port);
                if (!connected)
                {
                    if (!isReconnect)
                    {
                        ClearRuntimeSessionState();
                        SetState(ConnectionLifecycleState.Disconnected);
                    }
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
                            IsReconnect = isReconnect,
                            ClientVersion = GetClientVersion(),
                            ProtocolVersion = ProtocolVersions.Current
                        });
                }
                catch (Exception ex)
                {
                    NetworkClient.Instance.Disconnect(false);
                    if (!isReconnect)
                    {
                        ClearRuntimeSessionState();
                        SetState(ConnectionLifecycleState.Disconnected);
                    }
                    else
                    {
                        IsLoggedIn = false;
                        SessionToken = null;
                        if (ex is ProtocolException)
                        {
                            MarkReconnectFailed();
                        }
                        else
                        {
                            SetState(ConnectionLifecycleState.Reconnecting);
                        }
                    }
                    throw;
                }

                if (response?.ProtocolVersion != ProtocolVersions.Current)
                {
                    NetworkClient.Instance.Disconnect(false);
                    if (!isReconnect)
                    {
                        ClearRuntimeSessionState();
                        SetState(ConnectionLifecycleState.Disconnected);
                    }
                    else
                    {
                        MarkReconnectFailed();
                    }
                    throw new ProtocolException("服务器版本过旧，请更换节点或等待服务器更新。");
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
                SetState(ConnectionLifecycleState.Connected);
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
            SetState(ConnectionLifecycleState.Disconnected);
            NetworkClient.Instance.Disconnect();
            MainThreadDispatcher.Enqueue(NavigationButton.RefreshServerLabel);
        }

        public static void MarkDisconnectedByRemote()
        {
            IsLoggedIn = false;
            SessionToken = null;
            SetState(string.IsNullOrWhiteSpace(CurrentServerAddress)
                ? ConnectionLifecycleState.Disconnected
                : ConnectionLifecycleState.Reconnecting);
            MainThreadDispatcher.Enqueue(NavigationButton.RefreshServerLabel);
        }

        public static void MarkReconnectFailed()
        {
            ClearRuntimeSessionState();
            SetState(ConnectionLifecycleState.ReconnectFailed);
            MainThreadDispatcher.Enqueue(NavigationButton.RefreshServerLabel);
        }

        public static void EnsureCanSendRequest()
        {
            if (IsReconnecting)
            {
                throw new InvalidOperationException("正在重连服务器，请稍候。");
            }

            if (!NetworkClient.Instance.IsConnected)
            {
                throw new InvalidOperationException("未连接服务器。");
            }

            if (!IsLoggedIn)
            {
                throw new InvalidOperationException("尚未登录服务器。");
            }
        }

        private static void ClearRuntimeSessionState()
        {
            IsLoggedIn = false;
            SessionToken = null;
            PlayerManager.ClearSession();
            LobbyManager.ClearSession();
        }

        private static void SetState(ConnectionLifecycleState state)
        {
            if (State == state) return;

            State = state;
            try
            {
                StateChanged?.Invoke(state);
            }
            catch (Exception ex)
            {
                MDEN.Managers.ClientLogManager.Warning($"Connection state listener failed: {ex.Message}");
            }
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
                if (!CanSendRequests)
                {
                    return;
                }

                MDEN.Managers.ClientLogManager.Warning($"Failed to sync local player profile: {ex.Message}");
            }
        }

        private static string GetClientVersion()
        {
            return MelonBase.FindMelon("Ensemble", "MDENTeam")?.Info?.Version ?? "unknown";
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
