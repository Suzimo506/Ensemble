using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MDEN.Network;
using MDEN.Protocol;
using MDEN.Protocol.Enums;
using MDEN.Protocol.Messages.Lobby;
using MDEN.Protocol.Models;
using MDEN.Protocol.Rules;
using MDEN.UI.Core;
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
        private static readonly Dictionary<int, long> LobbySyncRevisions = new Dictionary<int, long>();
        private static readonly object LobbySyncQueueLock = new object();
        private static int? _pendingJoinLobbyId;
        private static LobbySyncPush _pendingLobbySyncPush;
        private static bool _lobbySyncApplyQueued;
        private static int _pendingLobbySyncCount;
        private static int _lobbySyncGeneration;

        public static async Task<LobbyListEntry[]> RefreshLobbiesAsync()
        {
            EnsureReady();

            var response = await NetworkClient.Instance.SendRequestAsync<GetLobbiesRequest, GetLobbiesResponse>(
                OpCodes.GetLobbiesReq,
                new GetLobbiesRequest());

            CurrentLobbies = response?.Lobbies ?? new LobbyListEntry[0];
            return CurrentLobbies;
        }

        public static Task JoinLobbyAsync(int lobbyId)
        {
            return JoinLobbyAsync(lobbyId, null);
        }

        public static async Task JoinLobbyAsync(int lobbyId, string password)
        {
            EnsureReady();
            _pendingJoinLobbyId = lobbyId;

            try
            {
                await PlayerManager.SyncLocalProfileToServerAsync();
                await PlayerManager.SyncCurrentSelectionAsync();
                await PlayerManager.SyncChartStateAsync();
                await NetworkClient.Instance.SendRequestAsync<JoinLobbyRequest, JoinLobbyResponse>(
                    OpCodes.JoinLobbyReq,
                    new JoinLobbyRequest { LobbyId = lobbyId, Password = password });
            }
            catch
            {
                _pendingJoinLobbyId = null;
                throw;
            }
        }

        public static void CancelPendingJoin(int lobbyId)
        {
            if (_pendingJoinLobbyId == lobbyId)
            {
                _pendingJoinLobbyId = null;
            }
        }

        public static void CancelPendingJoin()
        {
            _pendingJoinLobbyId = null;
        }

        public static void MarkLobbyEntered(LobbyListEntry entry)
        {
            if (entry == null) return;

            _pendingJoinLobbyId = null;
            IgnoredLobbySyncIds.Remove(entry.Id);
            if (CurrentLobby?.Id == entry.Id && CurrentLobby.Revision > 0)
            {
                RememberLobbySyncRevision(CurrentLobby);
                return;
            }

            var currentUid = PlayerManager.CurrentUid;
            var currentName = PlayerManager.CurrentProfile?.Name;

            CurrentLobby = new LobbySyncPush
            {
                Id = entry.Id,
                Revision = 0,
                Name = entry.Name,
                HostUid = entry.HostUid,
                HostName = entry.HostName,
                PlayMode = entry.PlayMode,
                MaxPlayers = entry.MaxPlayers,
                PlaylistSize = entry.PlaylistSize,
                TenziSongsPerPlayer = LobbyPlayModeRules.NormalizeTenziSongsPerPlayer(entry.TenziSongsPerPlayer, entry.PlaylistSize),
                SettlementEnabled = entry.SettlementEnabled,
                IsPrivate = entry.IsPrivate,
                JoinLocked = entry.JoinLocked,
                PlayType = entry.PlayType,
                ChartSelection = entry.ChartSelection,
                Goal = entry.Goal,
                Locked = entry.Locked,
                IsPlaying = entry.IsPlaying,
                Players = string.IsNullOrEmpty(currentUid) ? new string[0] : new[] { currentUid },
                ReadyPlayers = new string[0],
                MutedPlayers = new string[0],
                ChartSelectBannedPlayers = new string[0],
                Playlist = new string[0],
                PlaylistOwners = new LobbyPlaylistOwnerEntry[0],
                PlayerDetails = string.IsNullOrEmpty(currentUid)
                    ? new PlayerSyncEntry[0]
                    : new[]
                    {
                        new PlayerSyncEntry
                        {
                            Uid = currentUid,
                            Name = string.IsNullOrEmpty(currentName) ? currentUid : currentName,
                            Bio = PlayerManager.CurrentProfile?.Bio,
                            Title = PlayerManager.CurrentProfile?.Title,
                            ChatColor = PlayerManager.CurrentProfile?.ChatColor,
                            AvatarName = PlayerManager.CurrentProfile?.AvatarName,
                            AvatarData = PlayerManager.CurrentProfile?.AvatarData,
                            PingMS = 0,
                            Status = (byte)PlayerStatus.InLobby
                        }
                    }
            };
            if (!LobbySyncRevisions.ContainsKey(entry.Id))
            {
                LobbySyncRevisions[entry.Id] = 0;
            }

            NotifyCurrentLobbyChanged();
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

        public static string FormatCreateLobbyFailureMessage(string reason, ushort playlistSize)
        {
            if (string.IsNullOrWhiteSpace(reason)) return I18nManager.T("common.unknown_error");
            if (IsTenziSongsPerPlayerRangeFailure(reason))
            {
                return FormatTenziSongsPerPlayerRangeFailure(
                    ProtocolReasonText.TryReadLastPositiveInt(reason, out var max)
                        ? max
                        : LobbyPlayModeRules.GetTenziSongsPerPlayerMax(playlistSize));
            }

            return reason;
        }

        public static Task<int> CreateLobbyAsync(
            string name,
            ushort maxPlayers,
            byte playMode,
            LobbyGoal goal,
            ushort playlistSize,
            byte tenziSongsPerPlayer,
            bool settlementEnabled,
            string password)
        {
            return CreateLobbyWithSnapshotAsync(new CreateLobbyRequest
            {
                Name = name,
                MaxPlayers = maxPlayers,
                PlayMode = playMode,
                PlayType = (byte)LobbyPlayType.All,
                ChartSelection = (byte)LobbyChartSelection.HostPlaylist,
                Goal = (byte)goal,
                PlaylistSize = playlistSize,
                TenziSongsPerPlayer = tenziSongsPerPlayer,
                SettlementEnabled = settlementEnabled,
                Password = password
            });
        }

        private static async Task<int> CreateLobbyWithSnapshotAsync(CreateLobbyRequest request)
        {
            var lobbyId = await CreateLobbyAsync(request);
            MarkLobbyEntered(lobbyId, request);
            return lobbyId;
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

            ResetPendingLobbySync();
            _pendingJoinLobbyId = null;
            CurrentLobby = null;
            BattleManager.Reset();
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
                await RefreshCurrentLobbyAfterReconnectAsync();
                await RefreshLobbiesAsync();
            }
            catch (Exception ex)
            {
                MDEN.Managers.ClientLogManager.Warning($"Refresh lobbies after reconnect failed: {ex.Message}");
            }
        }

        private static async Task RefreshCurrentLobbyAfterReconnectAsync()
        {
            var response = await NetworkClient.Instance.SendRequestAsync<GetCurrentLobbyRequest, GetCurrentLobbyResponse>(
                OpCodes.GetCurrentLobbyReq,
                new GetCurrentLobbyRequest());

            if (response?.Lobby == null)
            {
                CurrentLobby = null;
                _pendingJoinLobbyId = null;
                IgnoredLobbySyncIds.Clear();
                LobbySyncRevisions.Clear();
                BattleManager.Reset();
                NotifyCurrentLobbyChanged();
                return;
            }

            ApplyCurrentLobbySnapshot(response?.Lobby, true);
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

        public static async Task SetLobbySettingsAsync(bool joinLocked, bool updatePassword, string password)
        {
            await SetLobbySettingsAsync(new LobbySettingsRequest
            {
                JoinLocked = joinLocked,
                UpdatePassword = updatePassword,
                Password = password
            });
        }

        public static Task SetJoinLockedAsync(bool joinLocked)
        {
            return SetLobbySettingsAsync(new LobbySettingsRequest
            {
                JoinLocked = joinLocked
            });
        }

        public static Task SetPasswordAsync(string password)
        {
            return SetLobbySettingsAsync(new LobbySettingsRequest
            {
                JoinLocked = CurrentLobby?.JoinLocked == true,
                UpdatePassword = true,
                Password = password
            });
        }

        public static Task SetPlayModeAsync(byte playMode)
        {
            return SetLobbySettingsAsync(new LobbySettingsRequest
            {
                JoinLocked = CurrentLobby?.JoinLocked == true,
                UpdatePlayMode = true,
                PlayMode = playMode
            });
        }

        public static Task SetGoalAsync(byte goal)
        {
            return SetLobbySettingsAsync(new LobbySettingsRequest
            {
                JoinLocked = CurrentLobby?.JoinLocked == true,
                UpdateGoal = true,
                Goal = goal
            });
        }

        public static Task SetSettlementEnabledAsync(bool settlementEnabled)
        {
            return SetLobbySettingsAsync(new LobbySettingsRequest
            {
                JoinLocked = CurrentLobby?.JoinLocked == true,
                UpdateSettlementEnabled = true,
                SettlementEnabled = settlementEnabled
            });
        }

        public static Task SetTenziSongsPerPlayerAsync(byte value)
        {
            return SetLobbySettingsAsync(new LobbySettingsRequest
            {
                JoinLocked = CurrentLobby?.JoinLocked == true,
                UpdateTenziSongsPerPlayer = true,
                TenziSongsPerPlayer = value
            });
        }

        public static bool CanSetTenziSongsPerPlayer(byte value, out string blockedMessage)
        {
            blockedMessage = null;
            var lobby = CurrentLobby;
            if (lobby == null) return true;

            var maxOwned = GetMaxTenziEntryCount(lobby);
            if (value >= maxOwned) return true;

            blockedMessage = I18nManager.Tf("tenzi.songs_per_player.existing_limit", maxOwned);
            return false;
        }

        public static async Task SetLobbySettingsAsync(LobbySettingsRequest request)
        {
            EnsureReady();
            await NetworkClient.Instance.SendRequestAsync<LobbySettingsRequest, LobbySettingsResponse>(
                OpCodes.LobbySettingsReq,
                request ?? new LobbySettingsRequest());
        }

        public static string FormatSettingsFailureMessage(string reason)
        {
            if (string.IsNullOrWhiteSpace(reason)) return I18nManager.T("common.unknown_error");
            if (reason == "游戏准备或进行中，不能修改房间规则") return I18nManager.T("room.rules_locked");
            if (reason == "已有玩家点歌数超过新上限，请先移除多余谱面")
            {
                return I18nManager.T("tenzi.songs_per_player.existing_limit_server");
            }

            if (IsTenziSongsPerPlayerRangeFailure(reason))
            {
                return FormatTenziSongsPerPlayerRangeFailure(
                    ProtocolReasonText.TryReadLastPositiveInt(reason, out var max)
                        ? max
                        : LobbyPlayModeRules.GetTenziSongsPerPlayerMax(CurrentLobby?.PlaylistSize ?? 2));
            }

            return reason;
        }

        private static bool IsTenziSongsPerPlayerRangeFailure(string reason)
        {
            return reason.StartsWith("天子模式每人点歌数必须在", System.StringComparison.Ordinal);
        }

        private static string FormatTenziSongsPerPlayerRangeFailure(int max)
        {
            return I18nManager.Tf(
                "create.number_range",
                I18nManager.T("tenzi.songs_per_player.title"),
                LobbyPlayModeRules.MinTenziSongsPerPlayer,
                max);
        }

        private static int GetMaxTenziEntryCount(LobbySyncPush lobby)
        {
            return lobby?.PlaylistOwners?
                .Where(owner => !string.IsNullOrWhiteSpace(owner?.Uid))
                .GroupBy(owner => owner.Uid)
                .Select(group => group.Count())
                .DefaultIfEmpty(0)
                .Max() ?? 0;
        }

        public static void Init()
        {
            PushDispatcher.Instance.Register<LobbySyncPush>(OpCodes.LobbySyncPush, OnLobbySync);
            PushDispatcher.Instance.Register<LobbyKickedPush>(OpCodes.LobbyKickedPush, OnLobbyKicked);
        }

        public static void ClearSession()
        {
            CurrentLobbies = new LobbyListEntry[0];
            CurrentLobby = null;
            _pendingJoinLobbyId = null;
            IgnoredLobbySyncIds.Clear();
            LobbySyncRevisions.Clear();
            ResetPendingLobbySync();
            BattleManager.Reset();
            NotifyCurrentLobbyChanged();
        }

        public static void MarkLobbyEntered(int lobbyId, CreateLobbyRequest request)
        {
            _pendingJoinLobbyId = null;
            IgnoredLobbySyncIds.Remove(lobbyId);
            if (CurrentLobby?.Id == lobbyId && CurrentLobby.Revision > 0)
            {
                RememberLobbySyncRevision(CurrentLobby);
                return;
            }

            var currentUid = PlayerManager.CurrentUid;
            var currentName = PlayerManager.CurrentProfile?.Name;

            CurrentLobby = new LobbySyncPush
            {
                Id = lobbyId,
                Revision = 0,
                Name = request.Name,
                HostUid = currentUid,
                HostName = currentName,
                PlayMode = request.PlayMode,
                MaxPlayers = request.MaxPlayers,
                PlaylistSize = request.PlaylistSize,
                TenziSongsPerPlayer = LobbyPlayModeRules.NormalizeTenziSongsPerPlayer(request.TenziSongsPerPlayer, request.PlaylistSize),
                SettlementEnabled = request.SettlementEnabled,
                IsPrivate = !string.IsNullOrWhiteSpace(request.Password),
                JoinLocked = false,
                PlayType = request.PlayType,
                ChartSelection = request.ChartSelection,
                Goal = request.Goal,
                Locked = false,
                IsPlaying = false,
                Players = string.IsNullOrEmpty(currentUid) ? new string[0] : new[] { currentUid },
                ReadyPlayers = new string[0],
                MutedPlayers = new string[0],
                ChartSelectBannedPlayers = new string[0],
                Playlist = new string[0],
                PlaylistOwners = new LobbyPlaylistOwnerEntry[0],
                PlayerDetails = string.IsNullOrEmpty(currentUid)
                    ? new PlayerSyncEntry[0]
                    : new[]
                    {
                        new PlayerSyncEntry
                        {
                            Uid = currentUid,
                            Name = string.IsNullOrEmpty(currentName) ? currentUid : currentName,
                            Bio = PlayerManager.CurrentProfile?.Bio,
                            Title = PlayerManager.CurrentProfile?.Title,
                            ChatColor = PlayerManager.CurrentProfile?.ChatColor,
                            AvatarName = PlayerManager.CurrentProfile?.AvatarName,
                            AvatarData = PlayerManager.CurrentProfile?.AvatarData,
                            PingMS = 0,
                            Status = (byte)PlayerStatus.InLobby
                        }
                    }
            };
            if (!LobbySyncRevisions.ContainsKey(lobbyId))
            {
                LobbySyncRevisions[lobbyId] = 0;
            }

            NotifyCurrentLobbyChanged();
        }

        private static void OnLobbySync(LobbySyncPush push)
        {
            if (push == null) return;

            int generation;
            lock (LobbySyncQueueLock)
            {
                if (!ShouldReplacePendingLobbySync(_pendingLobbySyncPush, push))
                {
                    return;
                }

                _pendingLobbySyncPush = push;
                _pendingLobbySyncCount++;
                if (_lobbySyncApplyQueued) return;

                _lobbySyncApplyQueued = true;
                generation = _lobbySyncGeneration;
            }

            MainThreadDispatcher.Enqueue(() => ApplyPendingLobbySync(generation));
        }

        private static void ApplyPendingLobbySync(int generation)
        {
            LobbySyncPush push;
            int coalescedCount;
            lock (LobbySyncQueueLock)
            {
                if (generation != _lobbySyncGeneration)
                {
                    _lobbySyncApplyQueued = false;
                    _pendingLobbySyncPush = null;
                    _pendingLobbySyncCount = 0;
                    return;
                }

                push = _pendingLobbySyncPush;
                coalescedCount = _pendingLobbySyncCount;
                _pendingLobbySyncPush = null;
                _pendingLobbySyncCount = 0;
                _lobbySyncApplyQueued = false;
            }

            if (coalescedCount > 1)
            {
                MDEN.Managers.ClientLogManager.SlowOperation($"[MDEN.Perf] Coalesced {coalescedCount} lobby sync pushes, applying latest revision {push?.Revision ?? 0}");
            }

            ApplyCurrentLobbySnapshot(push, false);
        }

        private static void ResetPendingLobbySync()
        {
            lock (LobbySyncQueueLock)
            {
                _pendingLobbySyncPush = null;
                _lobbySyncApplyQueued = false;
                _pendingLobbySyncCount = 0;
                _lobbySyncGeneration++;
            }
        }

        private static bool ShouldReplacePendingLobbySync(LobbySyncPush current, LobbySyncPush incoming)
        {
            if (current == null) return true;
            if (incoming == null) return false;
            if (current.Id != incoming.Id) return true;
            if (current.Revision <= 0 || incoming.Revision <= 0) return true;

            return incoming.Revision >= current.Revision;
        }

        private static void ApplyCurrentLobbySnapshot(LobbySyncPush push, bool force)
        {
            if (push == null) return;

            if (!force &&
                IgnoredLobbySyncIds.Contains(push.Id) &&
                CurrentLobby?.Id != push.Id &&
                _pendingJoinLobbyId != push.Id)
            {
                MDEN.Managers.ClientLogManager.Msg($"Ignored stale lobby sync: {push.Id}");
                return;
            }

            if (!force &&
                _pendingJoinLobbyId.HasValue &&
                _pendingJoinLobbyId.Value != push.Id)
            {
                MDEN.Managers.ClientLogManager.Msg($"Ignored lobby sync while joining {_pendingJoinLobbyId.Value}: {push.Id}");
                return;
            }

            if (!force && IsStaleLobbySync(push))
            {
                MDEN.Managers.ClientLogManager.Msg($"Ignored stale lobby sync: {push.Id}, revision: {push.Revision}");
                return;
            }

            var pingOnlySync = IsPingOnlyLobbySync(CurrentLobby, push);

            _pendingJoinLobbyId = null;
            IgnoredLobbySyncIds.Remove(push.Id);
            CurrentLobby = push;
            RememberLobbySyncRevision(push);
            if (pingOnlySync) return;

            MDEN.Managers.ClientLogManager.Msg($"Lobby sync received: {push.Id}, players: {push.Players?.Length ?? 0}/{push.MaxPlayers}");
            NotifyCurrentLobbyChanged();
        }

        private static void OnLobbyKicked(LobbyKickedPush push)
        {
            var reason = string.IsNullOrWhiteSpace(push?.Reason) ? I18nManager.T("lobby.kicked") : push.Reason;
            ResetPendingLobbySync();
            _pendingJoinLobbyId = null;
            CurrentLobby = null;
            BattleManager.Reset();
            NotifyCurrentLobbyChanged();
            UiNotificationManager.RequestToast(reason);
            UiNotificationManager.RequestKickedToLobbyList();
        }

        private static void NotifyCurrentLobbyChanged()
        {
            var lobby = CurrentLobby;
            MainThreadDispatcher.Enqueue(() => CurrentLobbyChanged?.Invoke(lobby));
        }

        private static bool IsStaleLobbySync(LobbySyncPush push)
        {
            if (push.Revision <= 0) return false;
            return LobbySyncRevisions.TryGetValue(push.Id, out var latestRevision) &&
                   push.Revision <= latestRevision;
        }

        public static int GetReadyDifficulty(string uid)
        {
            return GetDifficulty(CurrentLobby?.ReadyPlayerDifficulties, uid);
        }

        public static int GetCurrentBattleDifficulty(string uid)
        {
            var difficulty = GetDifficulty(CurrentLobby?.CurrentBattleDifficulties, uid);
            if (difficulty > 0) return difficulty;

            var entry = PlaylistManager.GetCurrentPlaylistEntry();
            return entry?.Difficulty ?? 0;
        }

        private static bool IsPingOnlyLobbySync(LobbySyncPush previous, LobbySyncPush next)
        {
            if (previous == null || next == null || previous.Revision <= 0) return false;
            if (previous.Id != next.Id) return false;
            if (previous.Name != next.Name ||
                previous.HostUid != next.HostUid ||
                previous.HostName != next.HostName ||
                previous.PlayMode != next.PlayMode ||
                previous.PlayType != next.PlayType ||
                previous.ChartSelection != next.ChartSelection ||
                previous.Goal != next.Goal ||
                previous.MaxPlayers != next.MaxPlayers ||
                previous.PlaylistSize != next.PlaylistSize ||
                previous.TenziSongsPerPlayer != next.TenziSongsPerPlayer ||
                previous.SettlementEnabled != next.SettlementEnabled ||
                previous.IsPrivate != next.IsPrivate ||
                previous.JoinLocked != next.JoinLocked ||
                previous.Locked != next.Locked ||
                previous.IsPlaying != next.IsPlaying ||
                previous.WatcherCount != next.WatcherCount ||
                previous.CurrentPlaylistEntry != next.CurrentPlaylistEntry ||
                previous.CurrentBattleId != next.CurrentBattleId ||
                previous.CurrentBattleEntry != next.CurrentBattleEntry ||
                previous.TenziSelectedEntry != next.TenziSelectedEntry ||
                previous.TenziRoundClosed != next.TenziRoundClosed ||
                previous.TenziDrawSeed != next.TenziDrawSeed)
            {
                return false;
            }

            return StringArrayEquals(previous.Players, next.Players) &&
                   StringArrayEquals(previous.ReadyPlayers, next.ReadyPlayers) &&
                   StringArrayEquals(previous.MutedPlayers, next.MutedPlayers) &&
                   StringArrayEquals(previous.ChartSelectBannedPlayers, next.ChartSelectBannedPlayers) &&
                   StringArrayEquals(previous.Playlist, next.Playlist) &&
                   PlaylistOwnerEntriesEqual(previous.PlaylistOwners, next.PlaylistOwners) &&
                   DifficultyEntriesEqual(previous.ReadyPlayerDifficulties, next.ReadyPlayerDifficulties) &&
                   DifficultyEntriesEqual(previous.CurrentBattleDifficulties, next.CurrentBattleDifficulties) &&
                   PlayerCharactersEqual(previous.PlayerCharacters, next.PlayerCharacters) &&
                   PlayerDetailsEqualIgnoringPing(previous.PlayerDetails, next.PlayerDetails);
        }

        private static bool StringArrayEquals(string[] left, string[] right)
        {
            var leftLength = left?.Length ?? 0;
            var rightLength = right?.Length ?? 0;
            if (leftLength != rightLength) return false;

            for (var i = 0; i < leftLength; i++)
            {
                if (left[i] != right[i]) return false;
            }

            return true;
        }

        private static bool DifficultyEntriesEqual(LobbyPlayerDifficultyEntry[] left, LobbyPlayerDifficultyEntry[] right)
        {
            var leftLength = left?.Length ?? 0;
            var rightLength = right?.Length ?? 0;
            if (leftLength != rightLength) return false;

            for (var i = 0; i < leftLength; i++)
            {
                var a = left[i];
                var b = right[i];
                if (a == null || b == null)
                {
                    if (a != b) return false;
                    continue;
                }

                if (a.Uid != b.Uid || a.Difficulty != b.Difficulty)
                {
                    return false;
                }
            }

            return true;
        }

        private static bool PlaylistOwnerEntriesEqual(LobbyPlaylistOwnerEntry[] left, LobbyPlaylistOwnerEntry[] right)
        {
            var leftLength = left?.Length ?? 0;
            var rightLength = right?.Length ?? 0;
            if (leftLength != rightLength) return false;

            for (var i = 0; i < leftLength; i++)
            {
                var a = left[i];
                var b = right[i];
                if (a == null || b == null)
                {
                    if (a != b) return false;
                    continue;
                }

                if (a.Entry != b.Entry || a.Uid != b.Uid)
                {
                    return false;
                }
            }

            return true;
        }

        private static int GetDifficulty(LobbyPlayerDifficultyEntry[] entries, string uid)
        {
            if (entries == null || string.IsNullOrWhiteSpace(uid)) return 0;

            for (var i = 0; i < entries.Length; i++)
            {
                if (entries[i]?.Uid == uid) return entries[i].Difficulty;
            }

            return 0;
        }

        private static bool PlayerCharactersEqual(LobbyPlayerCharacterEntry[] left, LobbyPlayerCharacterEntry[] right)
        {
            var leftLength = left?.Length ?? 0;
            var rightLength = right?.Length ?? 0;
            if (leftLength != rightLength) return false;

            for (var i = 0; i < leftLength; i++)
            {
                var a = left[i];
                var b = right[i];
                if (a == null || b == null)
                {
                    if (a != b) return false;
                    continue;
                }

                if (a.Uid != b.Uid ||
                    a.GirlIndex != b.GirlIndex ||
                    a.ElfinIndex != b.ElfinIndex ||
                    a.FavGirlIndex != b.FavGirlIndex ||
                    a.FavElfinIndex != b.FavElfinIndex)
                {
                    return false;
                }
            }

            return true;
        }

        private static bool PlayerDetailsEqualIgnoringPing(PlayerSyncEntry[] left, PlayerSyncEntry[] right)
        {
            var leftLength = left?.Length ?? 0;
            var rightLength = right?.Length ?? 0;
            if (leftLength != rightLength) return false;

            for (var i = 0; i < leftLength; i++)
            {
                var a = left[i];
                var b = right[i];
                if (a == null || b == null)
                {
                    if (a != b) return false;
                    continue;
                }

                if (a.Uid != b.Uid ||
                    a.Name != b.Name ||
                    a.Bio != b.Bio ||
                    a.Title != b.Title ||
                    a.ChatColor != b.ChatColor ||
                    a.AvatarName != b.AvatarName ||
                    a.AvatarData != b.AvatarData ||
                    a.Status != b.Status)
                {
                    return false;
                }
            }

            return true;
        }

        private static void RememberLobbySyncRevision(LobbySyncPush push)
        {
            if (push.Revision > 0)
            {
                LobbySyncRevisions[push.Id] = push.Revision;
            }
        }

        private static void EnsureReady()
        {
            ConnectionManager.EnsureCanSendRequest();
        }
    }
}
