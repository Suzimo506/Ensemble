using System;
using System.Threading.Tasks;
using MDEN.Network;
using MDEN.Protocol;
using MDEN.Protocol.Messages.Lobby;
using MDEN.Protocol.Models;
using MelonLoader;

namespace MDEN.Managers
{
    public static class LobbyManager
    {
        public static LobbyListEntry[] CurrentLobbies { get; private set; } = new LobbyListEntry[0];
        public static LobbySyncPush CurrentLobby { get; private set; }
        public static bool IsInLobby => CurrentLobby != null;
        public static event Action<LobbySyncPush> CurrentLobbyChanged;

        public static async Task<LobbyListEntry[]> RefreshLobbiesAsync()
        {
            EnsureReady();

            var response = await NetworkClient.Instance.SendRequestAsync<GetLobbiesRequest, GetLobbiesResponse>(
                OpCodes.GetLobbiesReq,
                new GetLobbiesRequest());

            CurrentLobbies = response?.Lobbies ?? new LobbyListEntry[0];
            return CurrentLobbies;
        }

        public static async Task JoinLobbyAsync(int lobbyId)
        {
            EnsureReady();
            await PlayerManager.SyncCurrentSelectionAsync();

            await NetworkClient.Instance.SendRequestAsync<JoinLobbyRequest, JoinLobbyResponse>(
                OpCodes.JoinLobbyReq,
                new JoinLobbyRequest { LobbyId = lobbyId });
        }

        public static async Task<int> CreateLobbyAsync(CreateLobbyRequest request)
        {
            EnsureReady();
            await PlayerManager.SyncCurrentSelectionAsync();

            var response = await NetworkClient.Instance.SendRequestAsync<CreateLobbyRequest, CreateLobbyResponse>(
                OpCodes.CreateLobbyReq,
                request);

            return response?.LobbyId ?? 0;
        }

        public static async Task LeaveLobbyAsync()
        {
            EnsureReady();

            await NetworkClient.Instance.SendRequestAsync<LeaveLobbyRequest, LeaveLobbyResponse>(
                OpCodes.LeaveLobbyReq,
                new LeaveLobbyRequest());

            CurrentLobby = null;
            NotifyCurrentLobbyChanged();
        }

        public static async Task StartLobbyAsync()
        {
            EnsureReady();
            await NetworkClient.Instance.SendRequestAsync<LobbyLockRequest, LobbyLockResponse>(
                OpCodes.LobbyLockReq,
                new LobbyLockRequest { Locked = true });
        }

        public static async Task KickPlayerAsync(string targetUid)
        {
            EnsureReady();
            await NetworkClient.Instance.SendRequestAsync<LobbyKickRequest, LobbyKickResponse>(
                OpCodes.LobbyKickReq,
                new LobbyKickRequest { TargetUid = targetUid });
        }

        public static async Task TransferHostAsync(string targetUid)
        {
            EnsureReady();
            await NetworkClient.Instance.SendRequestAsync<LobbyTransferHostRequest, LobbyTransferHostResponse>(
                OpCodes.LobbyTransferHostReq,
                new LobbyTransferHostRequest { TargetUid = targetUid });
        }

        public static async Task SetMutedAsync(string targetUid, bool muted)
        {
            EnsureReady();
            await NetworkClient.Instance.SendRequestAsync<LobbyMuteRequest, LobbyMuteResponse>(
                OpCodes.LobbyMuteReq,
                new LobbyMuteRequest { TargetUid = targetUid, Muted = muted });
        }

        public static async Task SetChartSelectBannedAsync(string targetUid, bool banned)
        {
            EnsureReady();
            await NetworkClient.Instance.SendRequestAsync<LobbyBanChartSelectRequest, LobbyBanChartSelectResponse>(
                OpCodes.LobbyBanChartSelectReq,
                new LobbyBanChartSelectRequest { TargetUid = targetUid, Banned = banned });
        }

        public static void Init()
        {
            PushDispatcher.Instance.Register<LobbySyncPush>(OpCodes.LobbySyncPush, OnLobbySync);
        }

        public static void ClearSession()
        {
            CurrentLobbies = new LobbyListEntry[0];
            CurrentLobby = null;
            NotifyCurrentLobbyChanged();
        }

        public static void MarkLobbyEntered(int lobbyId, CreateLobbyRequest request)
        {
            var currentUid = PlayerManager.CurrentUid;
            var currentName = PlayerManager.CurrentProfile?.Name;

            CurrentLobby = new LobbySyncPush
            {
                Id = lobbyId,
                Name = request.Name,
                HostUid = currentUid,
                HostName = currentName,
                MaxPlayers = request.MaxPlayers,
                PlaylistSize = request.PlaylistSize,
                PlayType = request.PlayType,
                ChartSelection = request.ChartSelection,
                Goal = request.Goal,
                Locked = false,
                IsPlaying = false,
                Players = string.IsNullOrEmpty(currentUid) ? new string[0] : new[] { currentUid },
                ReadyPlayers = new string[0],
                Playlist = new string[0],
                PlayerDetails = string.IsNullOrEmpty(currentUid)
                    ? new PlayerSyncEntry[0]
                    : new[]
                    {
                        new PlayerSyncEntry
                        {
                            Uid = currentUid,
                            Name = string.IsNullOrEmpty(currentName) ? currentUid : currentName,
                            PingMS = 0,
                            Status = 2
                        }
                    }
            };
            NotifyCurrentLobbyChanged();
        }

        private static void OnLobbySync(LobbySyncPush push)
        {
            CurrentLobby = push;
            MelonLogger.Msg($"Lobby sync received: {push.Id}, players: {push.Players?.Length ?? 0}/{push.MaxPlayers}");
            NotifyCurrentLobbyChanged();
        }

        private static void NotifyCurrentLobbyChanged()
        {
            CurrentLobbyChanged?.Invoke(CurrentLobby);
        }

        private static void EnsureReady()
        {
            if (!NetworkClient.Instance.IsConnected)
            {
                throw new System.InvalidOperationException("Not connected to server.");
            }

            if (!ConnectionManager.IsLoggedIn)
            {
                throw new System.InvalidOperationException("Not logged in to server.");
            }
        }
    }
}
