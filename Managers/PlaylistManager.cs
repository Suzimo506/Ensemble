using System.Linq;
using System.Threading.Tasks;
using Il2CppAssets.Scripts.Database;
using MDEN.Network;
using MDEN.Protocol;
using MDEN.Protocol.Enums;
using MDEN.Protocol.Messages.Lobby;
using MDEN.Protocol.Messages.Playlist;
using MDEN.Protocol.Rules;

namespace MDEN.Managers
{
    public enum PlaylistToggleResult
    {
        Added,
        Removed
    }

    public static class PlaylistManager
    {
        private const int AddSuccess = 0;
        private const int AddHidden = 2;
        private const int AddNotSynced = 3;
        private const int AddUnsupported = 4;
        private const int LockSuccess = 0;
        private const int LockNotSynced = 5;
        private const int LockUnsupported = 6;
        private const string UnsupportedChartMessage = "该谱面暂不支持联机";
        private static int _cachedEntryLobbyId;
        private static string _cachedEntryBattleId;
        private static int _cachedEntryIndex = -1;
        private static string _cachedEntryText;
        private static PlaylistEntryViewModel _cachedEntry;
        private static MusicInfo _cachedMusicInfo;

        public static bool CanChangePlaylist
        {
            get
            {
                var lobby = LobbyManager.CurrentLobby;
                if (lobby == null || lobby.Locked || lobby.IsPlaying) return false;
                return true;
            }
        }

        public static bool IsCurrentChartInPlaylist()
        {
            var entry = ChartManager.GetCurrentEntry();
            return !string.IsNullOrEmpty(entry) && ContainsEntry(entry);
        }

        public static bool IsCurrentChartUnsupported()
        {
            var entry = ChartManager.GetCurrentEntry();
            return !string.IsNullOrEmpty(entry) &&
                   !ContainsEntry(entry) &&
                   ChartSelectionRules.IsUnsupportedPlaylistEntry(entry);
        }

        public static bool ContainsEntry(string entry)
        {
            var playlist = LobbyManager.CurrentLobby?.Playlist;
            return playlist != null && playlist.Any(item => ChartManager.IsSameChart(item, entry));
        }

        public static bool IsPlaylistFull()
        {
            var lobby = LobbyManager.CurrentLobby;
            return lobby?.Playlist != null && lobby.Playlist.Length >= lobby.PlaylistSize;
        }

        public static PlaylistEntryViewModel[] GetPlaylistItems()
        {
            var playlist = LobbyManager.CurrentLobby?.Playlist;
            if (playlist == null || playlist.Length == 0) return new PlaylistEntryViewModel[0];

            return playlist.Select(ParsePlaylistItemSafe)
                .Where(item => item != null)
                .ToArray();
        }

        public static PlaylistEntryViewModel GetCurrentPlaylistEntry()
        {
            var lobby = LobbyManager.CurrentLobby;
            if (lobby == null) return null;

            var entryText = GetCurrentPlaylistEntryText(lobby, out var playlistIndex);
            if (string.IsNullOrWhiteSpace(entryText)) return null;

            if (_cachedEntryLobbyId == lobby.Id &&
                _cachedEntryBattleId == lobby.CurrentBattleId &&
                _cachedEntryIndex == playlistIndex &&
                _cachedEntryText == entryText)
            {
                return _cachedEntry;
            }

            _cachedEntryLobbyId = lobby.Id;
            _cachedEntryBattleId = lobby.CurrentBattleId;
            _cachedEntryIndex = playlistIndex;
            _cachedEntryText = entryText;
            _cachedEntry = ChartManager.ParseEntry(entryText);
            _cachedMusicInfo = null;
            return _cachedEntry;
        }

        public static MusicInfo GetCurrentPlaylistMusicInfo()
        {
            var entry = GetCurrentPlaylistEntry();
            if (entry == null) return null;
            return _cachedMusicInfo ??= ChartManager.GetMusicInfo(entry.ChartKey);
        }

        public static bool IsPlaylistBattleActive()
        {
            return LobbyManager.CurrentLobby?.IsPlaying == true && GetCurrentPlaylistEntry() != null;
        }

        public static string GetPreparationButtonText()
        {
            if (!LobbyManager.IsInLobby) return "PLAY!";
            if (!CanChangePlaylist) return LobbyManager.CurrentLobby?.Locked == true ? "等待准备" : "等待房主选歌";
            var entry = ChartManager.GetCurrentEntry();
            if (string.IsNullOrEmpty(entry)) return "未选择谱面";
            if (!string.IsNullOrEmpty(entry) && ContainsEntry(entry)) return "移除歌曲列表";
            if (IsCurrentChartUnsupported()) return UnsupportedChartMessage;
            if (IsPlaylistFull()) return "歌曲列表已满";
            return "加入歌曲列表";
        }

        public static bool CanUsePreparationButton()
        {
            if (!LobbyManager.IsInLobby) return true;
            if (!CanChangePlaylist) return false;
            var entry = ChartManager.GetCurrentEntry();
            if (string.IsNullOrEmpty(entry)) return false;
            if (!string.IsNullOrEmpty(entry) && ContainsEntry(entry)) return true;
            return !ChartSelectionRules.IsUnsupportedPlaylistEntry(entry) && !IsPlaylistFull();
        }

        public static async Task<PlaylistToggleResult> ToggleCurrentChartAsync()
        {
            EnsureReady();
            var entry = ChartManager.GetCurrentEntry();
            if (string.IsNullOrEmpty(entry))
            {
                throw new System.InvalidOperationException("No chart selected.");
            }

            if (ContainsEntry(entry))
            {
                await RemoveAsync(entry);
                return PlaylistToggleResult.Removed;
            }

            EnsureEntrySupported(entry);
            await AddAsync(entry);
            return PlaylistToggleResult.Added;
        }

        public static async Task AddAsync(string entry)
        {
            EnsureReady();
            EnsureEntrySupported(entry);
            await PlayerManager.SyncChartStateAsync();
            var response = await NetworkClient.Instance.SendRequestAsync<PlaylistAddRequest, PlaylistAddResponse>(
                OpCodes.PlaylistAddReq,
                new PlaylistAddRequest { Entry = entry });

            var result = response?.Result ?? AddSuccess;
            if (result == AddSuccess) return;

            if (result == AddHidden)
            {
                throw new System.InvalidOperationException("有人隐藏了该谱面");
            }

            if (result == AddNotSynced)
            {
                throw new System.InvalidOperationException("有人未下载该谱面");
            }

            if (result == AddUnsupported)
            {
                throw new System.InvalidOperationException(UnsupportedChartMessage);
            }

            throw new System.InvalidOperationException($"添加歌曲失败，错误码 {result}。");
        }

        public static async Task RemoveAsync(string entry)
        {
            EnsureReady();
            var actualEntry = FindActualEntry(entry) ?? entry;
            await NetworkClient.Instance.SendRequestAsync<PlaylistRemoveRequest, PlaylistRemoveResponse>(
                OpCodes.PlaylistRemoveReq,
                new PlaylistRemoveRequest { Entry = actualEntry });
        }

        public static async Task StartPrepareAsync()
        {
            EnsureReady();
            EnsurePlaylistSupported();
            await PlayerManager.SyncChartStateAsync();

            var response = await NetworkClient.Instance.SendRequestAsync<LobbyLockRequest, LobbyLockResponse>(
                OpCodes.LobbyLockReq,
                new LobbyLockRequest { Locked = true });

            if (response == null || response.Result == LockSuccess)
            {
                return;
            }

            if (response.Result == LockNotSynced)
            {
                throw new System.InvalidOperationException("有人未下载该谱面");
            }

            if (response.Result == LockUnsupported)
            {
                throw new System.InvalidOperationException("歌曲列表包含暂不支持联机的谱面");
            }

            if (response.Result != LockSuccess)
            {
                throw new System.InvalidOperationException($"开始准备失败，错误码 {response.Result}。");
            }
        }

        public static async Task SetReadyAsync(bool ready)
        {
            EnsureReady();
            if (ready)
            {
                await PlayerManager.SyncChartStateAsync();
            }

            await NetworkClient.Instance.SendRequestAsync<LobbyReadyRequest, LobbyReadyResponse>(
                OpCodes.LobbyReadyReq,
                new LobbyReadyRequest { Ready = ready });
        }

        public static async Task ContinueAsync()
        {
            EnsureReady();
            await NetworkClient.Instance.SendRequestAsync<PlaylistContinueRequest, PlaylistContinueResponse>(
                OpCodes.PlaylistContinueReq,
                new PlaylistContinueRequest());
        }

        public static async Task StopLobbyAsync()
        {
            EnsureReady();
            await NetworkClient.Instance.SendRequestAsync<LobbyStopRequest, LobbyStopResponse>(
                OpCodes.LobbyStopReq,
                new LobbyStopRequest());
        }

        public static bool IsLocalPlayerReady()
        {
            var lobby = LobbyManager.CurrentLobby;
            return lobby?.ReadyPlayers != null && lobby.ReadyPlayers.Contains(PlayerManager.CurrentUid);
        }

        private static string FindActualEntry(string entry)
        {
            var playlist = LobbyManager.CurrentLobby?.Playlist;
            return playlist?.FirstOrDefault(item => ChartManager.IsSameChart(item, entry));
        }

        private static PlaylistEntryViewModel ParsePlaylistItemSafe(string entry)
        {
            try
            {
                return ChartManager.ParseEntry(entry);
            }
            catch (System.Exception ex)
            {
                MDEN.Managers.ClientLogManager.Warning($"Parse playlist entry failed: {ex.Message}");
                return new PlaylistEntryViewModel
                {
                    Entry = entry,
                    ChartKey = entry ?? string.Empty,
                    Difficulty = 0,
                    OwnerName = "Unknown",
                    ChartName = "无法显示的谱面"
                };
            }
        }

        private static string GetCurrentPlaylistEntryText(LobbySyncPush lobby, out int playlistIndex)
        {
            playlistIndex = -1;
            if (!string.IsNullOrWhiteSpace(lobby.CurrentBattleEntry))
            {
                return lobby.CurrentBattleEntry;
            }

            var playlist = lobby.Playlist;
            if (playlist == null || playlist.Length == 0) return null;

            playlistIndex = lobby.CurrentPlaylistEntry < playlist.Length ? lobby.CurrentPlaylistEntry : 0;
            return playlist[playlistIndex];
        }

        private static void EnsureEntrySupported(string entry)
        {
            if (ChartSelectionRules.IsUnsupportedPlaylistEntry(entry))
            {
                throw new System.InvalidOperationException(UnsupportedChartMessage);
            }
        }

        private static void EnsurePlaylistSupported()
        {
            var playlist = LobbyManager.CurrentLobby?.Playlist;
            if (playlist == null) return;

            if (playlist.Any(ChartSelectionRules.IsUnsupportedPlaylistEntry))
            {
                throw new System.InvalidOperationException("歌曲列表包含暂不支持联机的谱面");
            }
        }

        private static void EnsureReady()
        {
            ConnectionManager.EnsureCanSendRequest();

            if (!LobbyManager.IsInLobby)
            {
                throw new System.InvalidOperationException("Not in lobby.");
            }
        }
    }
}
