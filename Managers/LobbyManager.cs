using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MDEN.Network;
using MDEN.Protocol;
using MDEN.Protocol.Enums;
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
        private static readonly HashSet<int> IgnoredLobbySyncIds = new HashSet<int>();
        private static int? _pendingJoinLobbyId;

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
            _pendingJoinLobbyId = lobbyId;
            await PlayerManager.SyncLocalProfileToServerAsync();
            await PlayerManager.SyncCurrentSelectionAsync();
            await PlayerManager.SyncChartStateAsync();

            try
            {
                await NetworkClient.Instance.SendRequestAsync<JoinLobbyRequest, JoinLobbyResponse>(
                    OpCodes.JoinLobbyReq,
                    new JoinLobbyRequest { LobbyId = lobbyId });
            }
            catch
            {
                _pendingJoinLobbyId = null;
                throw;
            }
        }

        public static async Task<int> CreateLobbyAsync(CreateLobbyRequest request)
        {
            EnsureReady();
            await PlayerManager.SyncLocalProfileToServerAsync();
            await PlayerManager.SyncCurrentSelectionAsync();
            await PlayerManager.SyncChartStateAsync();

            var response = await NetworkClient.Instance.SendRequestAsync<CreateLobbyRequest, CreateLobbyResponse>(
                OpCodes.CreateLobbyReq,
                request);

            return response?.LobbyId ?? 0;
        }

        public static async Task LeaveLobbyAsync()
        {
            EnsureReady();
            var leavingLobbyId = CurrentLobby?.Id;

            await NetworkClient.Instance.SendRequestAsync<LeaveLobbyRequest, LeaveLobbyResponse>(
                OpCodes.LeaveLobbyReq,
                new LeaveLobbyRequest());

            if (leavingLobbyId.HasValue)
            {
                IgnoredLobbySyncIds.Add(leavingLobbyId.Value);
            }

            _pendingJoinLobbyId = null;
            CurrentLobby = null;
            NotifyCurrentLobbyChanged();
        }

        public static async Task RefreshLobbiesAfterReconnectAsync()
        {
            if (!NetworkClient.Instance.IsConnected || !ConnectionManager.IsLoggedIn)
            {
                return;
            }

            try
            {
                await RefreshLobbiesAsync();
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Refresh lobbies after reconnect failed: {ex.Message}");
            }
        }

        public static async Task StartLobbyAsync()
        {
            await PlaylistManager.StartPrepareAsync();
        }

        public static async Task SetReadyAsync(bool ready)
        {
            EnsureReady();
            await NetworkClient.Instance.SendRequestAsync<LobbyReadyRequest, LobbyReadyResponse>(
                OpCodes.LobbyReadyReq,
                new LobbyReadyRequest { Ready = ready });
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
            _pendingJoinLobbyId = null;
            IgnoredLobbySyncIds.Clear();
            NotifyCurrentLobbyChanged();
        }

        public static void MarkLobbyEntered(int lobbyId, CreateLobbyRequest request)
        {
            _pendingJoinLobbyId = null;
            IgnoredLobbySyncIds.Remove(lobbyId);
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
                SettlementEnabled = request.SettlementEnabled,
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
                            Title = PlayerManager.CurrentProfile?.Title,
                            ChatColor = PlayerManager.CurrentProfile?.ChatColor,
                            PingMS = 0,
                            Status = (byte)PlayerStatus.InLobby
                        }
                    }
            };
            NotifyCurrentLobbyChanged();
        }

        private static void OnLobbySync(LobbySyncPush push)
        {
            if (push == null) return;

            if (IgnoredLobbySyncIds.Contains(push.Id) &&
                CurrentLobby?.Id != push.Id &&
                _pendingJoinLobbyId != push.Id)
            {
                MelonLogger.Msg($"Ignored stale lobby sync: {push.Id}");
                return;
            }

            if (_pendingJoinLobbyId.HasValue && _pendingJoinLobbyId.Value != push.Id)
            {
                MelonLogger.Msg($"Ignored lobby sync while joining {_pendingJoinLobbyId.Value}: {push.Id}");
                return;
            }

            _pendingJoinLobbyId = null;
            IgnoredLobbySyncIds.Remove(push.Id);
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
