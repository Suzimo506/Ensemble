using System.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Linq;
using CustomAlbums.Managers;
using Il2CppAssets.Scripts.Database;
using MDEN.Network;
using MDEN.Protocol;
using MDEN.Protocol.Enums;
using MDEN.Protocol.Messages.Player;
using MDEN.UI.Core;
using MelonLoader;

namespace MDEN.Managers
{
    public static class PlayerManager
    {
        private const int SelectionSyncMinFrameInterval = 30;
        private const int ChartStateSyncCacheMs = 10000;
        private const double SlowChartStateStepWarningMs = 250.0;
        private const double SlowChartStateSyncWarningMs = 750.0;
        private static readonly object ChartStateSyncLock = new object();
        private static readonly object CustomChartSnapshotLock = new object();

        public static string CurrentUid { get; private set; }
        public static GetPlayerResponse CurrentProfile { get; private set; }
        public static bool IsSinglePlaying => _lastPresenceStatus == PlayerStatus.SinglePlaying && !LobbyManager.IsInLobby;
        public static event Action ProfileChanged;
        private static GameSelectionInfo _lastSyncedSelection = new GameSelectionInfo(int.MinValue, int.MinValue);
        private static GameSelectionInfo _lastSyncedFavSelection = new GameSelectionInfo(int.MinValue, int.MinValue);
        private static int _lastSelectionSyncFrame;
        private static bool _selectionSyncInProgress;
        private static Task _chartStateSyncTask;
        private static DateTime _lastChartStateSyncUtc;
        private static CustomChartSnapshotItem[] _customChartSnapshot = Array.Empty<CustomChartSnapshotItem>();
        private static PlayerStatus? _lastPresenceStatus;

        public static void SetCurrentUid(string uid)
        {
            CurrentUid = uid;
        }

        public static void SetCurrentIdentity(string uid, string name)
        {
            CurrentUid = uid;
            CurrentProfile = CreateLocalProfile(uid, name, PlayerStatus.Online);
        }

        public static void ClearSession()
        {
            CurrentUid = null;
            CurrentProfile = null;
            _lastSyncedSelection = new GameSelectionInfo(int.MinValue, int.MinValue);
            _lastSyncedFavSelection = new GameSelectionInfo(int.MinValue, int.MinValue);
            _lastSelectionSyncFrame = 0;
            _selectionSyncInProgress = false;
            _lastPresenceStatus = null;
            InvalidateChartStateCache();
        }

        public static void SyncPresenceIfChanged(PlayerStatus status)
        {
            if (LobbyManager.IsInLobby) return;
            if (_lastPresenceStatus == status) return;

            _lastPresenceStatus = status;
            if (CurrentProfile != null)
            {
                CurrentProfile.Status = (byte)status;
            }

            SocialManager.SyncPresence(status);
        }

        public static async Task<GetPlayerResponse> GetMyProfileAsync()
        {
            var uid = ResolveLocalUid();
            CurrentUid ??= uid;
            CurrentProfile = CreateLocalProfile(uid, GetLocalPlayerName(), PlayerStatus.Online);
            NotifyProfileChanged();
            return await Task.FromResult(CurrentProfile);
        }

        public static Task<GetPlayerResponse> GetProfileAsync(string targetUid)
        {
            EnsureReady();

            return NetworkClient.Instance.SendRequestAsync<GetPlayerRequest, GetPlayerResponse>(
                OpCodes.GetPlayerReq,
                new GetPlayerRequest { TargetUid = targetUid });
        }

        public static Task SyncCurrentSelectionAsync()
        {
            return SyncSelectionAsync(GameAccountManager.GetCurrentSelection());
        }

        public static async Task SyncSelectionAsync(GameSelectionInfo selection)
        {
            var favSelection = ModConfigManager.EnableFavGirlDisplayForOthers
                ? GameAccountManager.GetCurrentFavGirlSelection()
                : selection;

            await UpdateMyProfileAsync(new UpdatePlayerRequest
            {
                GirlIndex = selection.GirlIndex,
                ElfinIndex = selection.ElfinIndex,
                FavGirlIndex = favSelection.GirlIndex,
                FavElfinIndex = favSelection.ElfinIndex
            });

            _lastSyncedSelection = selection;
            _lastSyncedFavSelection = favSelection;
        }

        public static void SyncCurrentSelectionIfChanged()
        {
            if (!ConnectionManager.CanSendRequests ||
                string.IsNullOrEmpty(CurrentUid) ||
                _selectionSyncInProgress)
            {
                return;
            }

            if (UnityEngine.Time.frameCount - _lastSelectionSyncFrame < SelectionSyncMinFrameInterval)
            {
                return;
            }

            var selection = GameAccountManager.RefreshSelectionSnapshot();
            var favSelection = ModConfigManager.EnableFavGirlDisplayForOthers
                ? GameAccountManager.GetCurrentFavGirlSelection()
                : selection;
            if (selection.Equals(_lastSyncedSelection) && favSelection.Equals(_lastSyncedFavSelection))
            {
                return;
            }

            _lastSelectionSyncFrame = UnityEngine.Time.frameCount;
            _selectionSyncInProgress = true;
            _ = SyncSelectionAsync(selection).ContinueWith(task =>
            {
                _selectionSyncInProgress = false;
                if (task.IsFaulted)
                {
                    LogSyncFailure("Sync selection failed", task.Exception?.GetBaseException());
                }
            });
        }

        public static void SyncSelectionFireAndForget(GameSelectionInfo selection)
        {
            if (!ConnectionManager.CanSendRequests || string.IsNullOrEmpty(CurrentUid)) return;

            _ = SyncSelectionAsync(selection).ContinueWith(task =>
            {
                if (task.IsFaulted)
                {
                    LogSyncFailure("Sync selection failed", task.Exception?.GetBaseException());
                }
            });
        }

        public static Task SyncCustomChartsAsync()
        {
            return SyncCustomChartsAsyncCore();
        }

        public static Task SyncHiddenChartsAsync()
        {
            return SyncHiddenChartsAsyncCore();
        }

        private static async Task SyncCustomChartsAsyncCore()
        {
            var snapshot = await MainThreadDispatcher.InvokeAsync(CollectCustomChartSnapshot);
            var result = await Task.Run(() => CollectCustomChartMd5s(snapshot));
            MainThreadDispatcher.Enqueue(() => ChartManager.CacheCustomChartMd5s(result.IndexEntries));
            await UpdateMyProfileAsync(new UpdatePlayerRequest { Customs = result.Md5s });
        }

        private static async Task SyncHiddenChartsAsyncCore()
        {
            var hiddens = await MainThreadDispatcher.InvokeAsync(CollectHiddenChartKeys);
            await UpdateMyProfileAsync(new UpdatePlayerRequest { Hiddens = hiddens });
        }

        public static async Task SyncChartStateAsync()
        {
            await SyncChartStateAsync(false);
        }

        public static Task SyncChartStateAsync(bool force)
        {
            lock (ChartStateSyncLock)
            {
                if (!force && IsChartStateSyncFreshUnsafe())
                {
                    return Task.CompletedTask;
                }

                if (_chartStateSyncTask != null && !_chartStateSyncTask.IsCompleted)
                {
                    return _chartStateSyncTask;
                }

                _chartStateSyncTask = SyncChartStateAsyncCore();
                return _chartStateSyncTask;
            }
        }

        private static async Task SyncChartStateAsyncCore()
        {
            var startedAt = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                var customSnapshot = await MainThreadDispatcher.InvokeAsync(CollectCustomChartSnapshot);
                var customsTask = Task.Run(() => CollectCustomChartMd5s(customSnapshot));
                var hiddens = await MainThreadDispatcher.InvokeAsync(CollectHiddenChartKeys);
                var customResult = await customsTask;
                MainThreadDispatcher.Enqueue(() => ChartManager.CacheCustomChartMd5s(customResult.IndexEntries));
                await UpdateMyProfileAsync(new UpdatePlayerRequest
                {
                    Customs = customResult.Md5s,
                    Hiddens = hiddens
                });
                lock (ChartStateSyncLock)
                {
                    _lastChartStateSyncUtc = DateTime.UtcNow;
                }
            }
            finally
            {
                startedAt.Stop();
                if (startedAt.Elapsed.TotalMilliseconds >= SlowChartStateSyncWarningMs)
                {
                    ClientLogManager.SlowOperation($"[MDEN.Perf] SyncChartStateAsync took {startedAt.Elapsed.TotalMilliseconds:F0}ms");
                }

                lock (ChartStateSyncLock)
                {
                    _chartStateSyncTask = null;
                }
            }
        }

        public static void InvalidateChartStateCache()
        {
            lock (ChartStateSyncLock)
            {
                _lastChartStateSyncUtc = default;
            }

            lock (CustomChartSnapshotLock)
            {
                _customChartSnapshot = Array.Empty<CustomChartSnapshotItem>();
            }
        }

        public static void SyncChartStateFireAndForget()
        {
            SyncChartStateFireAndForget(false);
        }

        public static void SyncChartStateFireAndForget(bool force)
        {
            if (!ConnectionManager.CanSendRequests || string.IsNullOrEmpty(CurrentUid)) return;

            _ = SyncChartStateAsync(force).ContinueWith(task =>
            {
                if (task.IsFaulted)
                {
                    LogSyncFailure("Sync chart state failed", task.Exception?.GetBaseException());
                }
            });
        }

        public static async Task UpdateMyProfileAsync(UpdatePlayerRequest request)
        {
            EnsureReady();

            await NetworkClient.Instance.SendRequestAsync<UpdatePlayerRequest, UpdatePlayerResponse>(
                OpCodes.UpdatePlayerReq,
                request);

            if (CurrentProfile != null)
            {
                ApplyLocalUpdate(CurrentProfile, request);
            }

            if (!string.IsNullOrWhiteSpace(request.Name))
            {
                ModConfigManager.SetPlayerName(request.Name);
            }

            NotifyProfileChanged();
        }

        public static async Task SyncLocalProfileToServerAsync()
        {
            EnsureReady();

            var request = CreateLocalProfileUpdateRequest();
            await NetworkClient.Instance.SendRequestAsync<UpdatePlayerRequest, UpdatePlayerResponse>(
                OpCodes.UpdatePlayerReq,
                request);

            CurrentProfile ??= CreateLocalProfile(CurrentUid, GetLocalPlayerName(), PlayerStatus.Online);
            ApplyLocalUpdate(CurrentProfile, request);
            NotifyProfileChanged();
        }

        public static Task UpdateNameAsync(string name)
        {
            ModConfigManager.SetPlayerName(name);
            ApplyLocalProfileConfig();
            return Task.CompletedTask;
        }

        public static Task UpdateChatColorAsync(string chatColor)
        {
            ModConfigManager.SetPlayerChatColor(chatColor);
            ApplyLocalProfileConfig();
            return Task.CompletedTask;
        }

        public static Task UpdateBioAsync(string bio)
        {
            ModConfigManager.SetPlayerBio(bio);
            ApplyLocalProfileConfig();
            return Task.CompletedTask;
        }

        public static Task UpdateEntranceMessageAsync(string entranceMessage)
        {
            ModConfigManager.SetPlayerEntranceMessage(entranceMessage);
            ApplyLocalProfileConfig();
            return Task.CompletedTask;
        }

        public static Task UpdateTitleAsync(string title)
        {
            ModConfigManager.SetPlayerTitle(title);
            ApplyLocalProfileConfig();
            return Task.CompletedTask;
        }

        private static void ApplyLocalProfileConfig()
        {
            CurrentUid ??= ResolveLocalUid();

            if (!string.IsNullOrEmpty(CurrentUid))
            {
                CurrentProfile ??= CreateLocalProfile(CurrentUid, GetLocalPlayerName(), PlayerStatus.Online);
                ApplyLocalUpdate(CurrentProfile, CreateLocalProfileUpdateRequest());
            }

            NotifyProfileChanged();
        }

        private static GetPlayerResponse CreateLocalProfile(string uid, string fallbackName, PlayerStatus status)
        {
            var totalMultiplayerGames = CurrentProfile != null && CurrentProfile.Uid == uid
                ? CurrentProfile.TotalMultiplayerGames
                : 0;

            return new GetPlayerResponse
            {
                Uid = uid,
                Name = GetLocalPlayerName(fallbackName),
                Status = (byte)status,
                Bio = ModConfigManager.PlayerBio,
                ChatColor = ModConfigManager.PlayerChatColor,
                EntranceMessage = ModConfigManager.PlayerEntranceMessage,
                Title = ModConfigManager.PlayerTitle,
                AvatarName = ModConfigManager.PlayerAvatarName,
                AvatarData = AvatarManager.GetLocalAvatarData(),
                TotalMultiplayerGames = totalMultiplayerGames
            };
        }

        private static UpdatePlayerRequest CreateLocalProfileUpdateRequest()
        {
            return new UpdatePlayerRequest
            {
                Name = GetLocalPlayerName(),
                Bio = ModConfigManager.PlayerBio ?? string.Empty,
                ChatColor = ModConfigManager.PlayerChatColor ?? "ffffff",
                EntranceMessage = ModConfigManager.PlayerEntranceMessage ?? string.Empty,
                Title = ModConfigManager.PlayerTitle ?? string.Empty,
                AvatarName = ModConfigManager.PlayerAvatarName ?? AvatarManager.DefaultAvatarName,
                AvatarData = AvatarManager.GetLocalAvatarData()
            };
        }

        public static Task UpdateAvatarAsync(string avatarName)
        {
            ModConfigManager.SetPlayerAvatarName(avatarName);
            ApplyLocalProfileConfig();
            return Task.CompletedTask;
        }

        private static string GetLocalPlayerName(string fallbackName = null)
        {
            return string.IsNullOrWhiteSpace(ModConfigManager.PlayerName) || ModConfigManager.PlayerName == "Player"
                ? fallbackName ?? CurrentProfile?.Name ?? CurrentUid ?? "Player"
                : ModConfigManager.PlayerName;
        }

        private static string ResolveLocalUid()
        {
            if (!string.IsNullOrEmpty(CurrentUid)) return CurrentUid;

            try
            {
                var account = GameAccountManager.GetCurrentAccount();
                if (!string.IsNullOrWhiteSpace(account.Uid)) return account.Uid;
            }
            catch
            {
            }

            return "Local";
        }

        private static void EnsureReady()
        {
            ConnectionManager.EnsureCanSendRequest();

            if (string.IsNullOrEmpty(CurrentUid))
            {
                throw new System.InvalidOperationException(I18nManager.T("account.not_logged_in"));
            }
        }

        private static void LogSyncFailure(string prefix, Exception ex)
        {
            if (!ConnectionManager.CanSendRequests)
            {
                return;
            }

            MDEN.Managers.ClientLogManager.Warning($"{prefix}: {ex?.Message}");
        }

        private static void ApplyLocalUpdate(GetPlayerResponse profile, UpdatePlayerRequest request)
        {
            if (request.Name != null) profile.Name = request.Name;
            if (request.Bio != null) profile.Bio = request.Bio;
            if (request.ChatColor != null) profile.ChatColor = request.ChatColor;
            if (request.EntranceMessage != null) profile.EntranceMessage = request.EntranceMessage;
            if (request.Title != null) profile.Title = request.Title;
            if (request.AvatarName != null) profile.AvatarName = request.AvatarName;
            if (request.AvatarData != null) profile.AvatarData = request.AvatarData;
            if (request.GirlIndex.HasValue) profile.GirlIndex = request.GirlIndex.Value;
            if (request.ElfinIndex.HasValue) profile.ElfinIndex = request.ElfinIndex.Value;
            if (request.FavGirlIndex.HasValue) profile.FavGirlIndex = request.FavGirlIndex.Value;
            if (request.FavElfinIndex.HasValue) profile.FavElfinIndex = request.FavElfinIndex.Value;
        }

        private static void NotifyProfileChanged()
        {
            MainThreadDispatcher.Enqueue(() => ProfileChanged?.Invoke());
        }

        private static CustomChartSnapshotItem[] CollectCustomChartSnapshot()
        {
            var snapshot = new List<CustomChartSnapshotItem>();
            foreach (var pair in AlbumManager.LoadedAlbums)
            {
                var album = pair.Value;
                if (album?.Sheets == null) continue;

                foreach (var sheet in album.Sheets.Values)
                {
                    if (sheet == null) continue;

                    snapshot.Add(new CustomChartSnapshotItem(album, sheet));
                }
            }

            lock (CustomChartSnapshotLock)
            {
                _customChartSnapshot = snapshot.ToArray();
                return _customChartSnapshot;
            }
        }

        private static CustomChartMd5Result CollectCustomChartMd5s(CustomChartSnapshotItem[] snapshot)
        {
            var startedAt = System.Diagnostics.Stopwatch.StartNew();
            var customs = new List<string>();
            var indexEntries = new List<KeyValuePair<string, CustomAlbums.Data.Album>>();

            foreach (var item in snapshot ?? Array.Empty<CustomChartSnapshotItem>())
            {
                var md5 = item.Sheet?.Md5;
                if (!string.IsNullOrEmpty(md5))
                {
                    customs.Add(md5);
                    if (item.Album != null)
                    {
                        indexEntries.Add(new KeyValuePair<string, CustomAlbums.Data.Album>(md5, item.Album));
                    }
                }
            }

            var result = customs.Distinct().ToArray();
            startedAt.Stop();
            if (startedAt.Elapsed.TotalMilliseconds >= SlowChartStateStepWarningMs)
            {
                ClientLogManager.SlowOperation($"[MDEN.Perf] CollectCustomChartMd5s took {startedAt.Elapsed.TotalMilliseconds:F0}ms, snapshot={snapshot?.Length ?? 0}, charts={result.Length}");
            }

            return new CustomChartMd5Result(result, indexEntries.ToArray());
        }

        private static string[] CollectHiddenChartKeys()
        {
            var startedAt = System.Diagnostics.Stopwatch.StartNew();
            var hiddens = new List<string>();
            var hiddenUids = GlobalDataBase.dbMusicTag?.Hide;
            if (hiddenUids != null)
            {
                foreach (string hiddenUid in hiddenUids)
                {
                    var key = ChartManager.GetEntryKey(hiddenUid);
                    if (!string.IsNullOrEmpty(key))
                    {
                        hiddens.Add(key);
                    }
                }
            }

            var result = hiddens.Distinct().ToArray();
            startedAt.Stop();
            if (startedAt.Elapsed.TotalMilliseconds >= SlowChartStateStepWarningMs)
            {
                ClientLogManager.SlowOperation($"[MDEN.Perf] CollectHiddenChartKeys took {startedAt.Elapsed.TotalMilliseconds:F0}ms, hidden={hiddenUids?.Count ?? 0}, charts={result.Length}");
            }

            return result;
        }

        private static bool IsChartStateSyncFreshUnsafe()
        {
            return _lastChartStateSyncUtc != default &&
                   DateTime.UtcNow - _lastChartStateSyncUtc < TimeSpan.FromMilliseconds(ChartStateSyncCacheMs);
        }

        private readonly struct CustomChartSnapshotItem
        {
            public CustomChartSnapshotItem(CustomAlbums.Data.Album album, CustomAlbums.Data.Sheet sheet)
            {
                Album = album;
                Sheet = sheet;
            }

            public readonly CustomAlbums.Data.Album Album;
            public readonly CustomAlbums.Data.Sheet Sheet;
        }

        private readonly struct CustomChartMd5Result
        {
            public CustomChartMd5Result(string[] md5s, KeyValuePair<string, CustomAlbums.Data.Album>[] indexEntries)
            {
                Md5s = md5s;
                IndexEntries = indexEntries;
            }

            public readonly string[] Md5s;
            public readonly KeyValuePair<string, CustomAlbums.Data.Album>[] IndexEntries;
        }
    }
}
