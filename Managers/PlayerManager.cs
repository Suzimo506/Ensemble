using System.Threading.Tasks;
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
        private static GameSelectionInfo _lastSyncedSelection = new GameSelectionInfo(int.MinValue, int.MinValue);
        private static int _lastSelectionSyncFrame;
        private static bool _selectionSyncInProgress;

        public static void SetCurrentUid(string uid)
        {
            CurrentUid = uid;
        }

        public static void SetCurrentIdentity(string uid, string name)
        {
            CurrentUid = uid;
            CurrentProfile = new GetPlayerResponse
            {
                Uid = uid,
                Name = name,
                Status = (byte)PlayerStatus.Online
            };
        }

        public static void ClearSession()
        {
            CurrentUid = null;
            CurrentProfile = null;
            _lastSyncedSelection = new GameSelectionInfo(int.MinValue, int.MinValue);
            _lastSelectionSyncFrame = 0;
            _selectionSyncInProgress = false;
        }

        public static async Task<GetPlayerResponse> GetMyProfileAsync()
        {
            EnsureReady();

            CurrentProfile = await NetworkClient.Instance.SendRequestAsync<GetPlayerRequest, GetPlayerResponse>(
                OpCodes.GetPlayerReq,
                new GetPlayerRequest { TargetUid = CurrentUid });

            return CurrentProfile;
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
            await UpdateMyProfileAsync(new UpdatePlayerRequest
            {
                GirlIndex = selection.GirlIndex,
                ElfinIndex = selection.ElfinIndex,
                FavGirlIndex = selection.GirlIndex,
                FavElfinIndex = selection.ElfinIndex
            });

            _lastSyncedSelection = selection;
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
            if (selection.Equals(_lastSyncedSelection))
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
        }

        public static Task UpdateNameAsync(string name)
        {
            return UpdateMyProfileAsync(new UpdatePlayerRequest { Name = name });
        }

        public static Task UpdateChatColorAsync(string chatColor)
        {
            return UpdateMyProfileAsync(new UpdatePlayerRequest { ChatColor = chatColor });
        }

        public static Task UpdateBioAsync(string bio)
        {
            return UpdateMyProfileAsync(new UpdatePlayerRequest { Bio = bio });
        }

        public static Task UpdateEntranceMessageAsync(string entranceMessage)
        {
            return UpdateMyProfileAsync(new UpdatePlayerRequest { EntranceMessage = entranceMessage });
        }

        public static Task UpdateTitleAsync(string title)
        {
            return UpdateMyProfileAsync(new UpdatePlayerRequest { Title = title });
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
