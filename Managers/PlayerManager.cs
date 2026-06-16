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
using MelonLoader;

namespace MDEN.Managers
{
    public static class PlayerManager
    {
        private const int SelectionSyncMinFrameInterval = 30;

        public static string CurrentUid { get; private set; }
        public static GetPlayerResponse CurrentProfile { get; private set; }
        public static event Action ProfileChanged;
        private static GameSelectionInfo _lastSyncedSelection = new GameSelectionInfo(int.MinValue, int.MinValue);
        private static GameSelectionInfo _lastSyncedFavSelection = new GameSelectionInfo(int.MinValue, int.MinValue);
        private static int _lastSelectionSyncFrame;
        private static bool _selectionSyncInProgress;

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
        }

        public static async Task<GetPlayerResponse> GetMyProfileAsync()
        {
            var uid = ResolveLocalUid();
            CurrentUid ??= uid;
            CurrentProfile = CreateLocalProfile(uid, GetLocalPlayerName(), PlayerStatus.Online);
            ProfileChanged?.Invoke();
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
            if (!NetworkClient.Instance.IsConnected ||
                !ConnectionManager.IsLoggedIn ||
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
                    MelonLogger.Warning($"Sync selection failed: {task.Exception?.GetBaseException().Message}");
                }
            });
        }

        public static void SyncSelectionFireAndForget(GameSelectionInfo selection)
        {
            if (!NetworkClient.Instance.IsConnected || !ConnectionManager.IsLoggedIn || string.IsNullOrEmpty(CurrentUid)) return;

            _ = SyncSelectionAsync(selection).ContinueWith(task =>
            {
                if (task.IsFaulted)
                {
                    MelonLogger.Warning($"Sync selection failed: {task.Exception?.GetBaseException().Message}");
                }
            });
        }

        public static Task SyncCustomChartsAsync()
        {
            var customs = new List<string>();
            foreach (var pair in AlbumManager.LoadedAlbums)
            {
                var md5 = ChartManager.GetCustomChartMd5(pair.Value?.Uid);
                if (!string.IsNullOrEmpty(md5))
                {
                    customs.Add(md5);
                }
            }

            return UpdateMyProfileAsync(new UpdatePlayerRequest { Customs = customs.Distinct().ToArray() });
        }

        public static Task SyncHiddenChartsAsync()
        {
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

            return UpdateMyProfileAsync(new UpdatePlayerRequest { Hiddens = hiddens.Distinct().ToArray() });
        }

        public static async Task SyncChartStateAsync()
        {
            await SyncCustomChartsAsync();
            await SyncHiddenChartsAsync();
        }

        public static void SyncChartStateFireAndForget()
        {
            if (!NetworkClient.Instance.IsConnected || !ConnectionManager.IsLoggedIn || string.IsNullOrEmpty(CurrentUid)) return;

            _ = SyncChartStateAsync().ContinueWith(task =>
            {
                if (task.IsFaulted)
                {
                    MelonLogger.Warning($"Sync chart state failed: {task.Exception?.GetBaseException().Message}");
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

            ProfileChanged?.Invoke();
        }

        public static async Task SyncLocalProfileToServerAsync()
        {
            EnsureReady();

            await NetworkClient.Instance.SendRequestAsync<UpdatePlayerRequest, UpdatePlayerResponse>(
                OpCodes.UpdatePlayerReq,
                CreateLocalProfileUpdateRequest());
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

            ProfileChanged?.Invoke();
        }

        private static GetPlayerResponse CreateLocalProfile(string uid, string fallbackName, PlayerStatus status)
        {
            return new GetPlayerResponse
            {
                Uid = uid,
                Name = GetLocalPlayerName(fallbackName),
                Status = (byte)status,
                Bio = ModConfigManager.PlayerBio,
                ChatColor = ModConfigManager.PlayerChatColor,
                EntranceMessage = ModConfigManager.PlayerEntranceMessage,
                Title = ModConfigManager.PlayerTitle,
                AvatarName = ModConfigManager.PlayerAvatarName
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
                AvatarName = ModConfigManager.PlayerAvatarName ?? "head_0"
            };
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
            if (!NetworkClient.Instance.IsConnected)
            {
                throw new System.InvalidOperationException("Not connected to server.");
            }

            if (string.IsNullOrEmpty(CurrentUid))
            {
                throw new System.InvalidOperationException("Not logged in.");
            }
        }

        private static void ApplyLocalUpdate(GetPlayerResponse profile, UpdatePlayerRequest request)
        {
            if (request.Name != null) profile.Name = request.Name;
            if (request.Bio != null) profile.Bio = request.Bio;
            if (request.ChatColor != null) profile.ChatColor = request.ChatColor;
            if (request.EntranceMessage != null) profile.EntranceMessage = request.EntranceMessage;
            if (request.Title != null) profile.Title = request.Title;
            if (request.GirlIndex.HasValue) profile.GirlIndex = request.GirlIndex.Value;
            if (request.ElfinIndex.HasValue) profile.ElfinIndex = request.ElfinIndex.Value;
            if (request.FavGirlIndex.HasValue) profile.FavGirlIndex = request.FavGirlIndex.Value;
            if (request.FavElfinIndex.HasValue) profile.FavElfinIndex = request.FavElfinIndex.Value;
        }
    }
}
