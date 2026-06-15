using System.Linq;
using System.Threading.Tasks;
using MDEN.Network;
using MDEN.Protocol;
using MDEN.Protocol.Enums;
using MDEN.Protocol.Messages.Lobby;
using MDEN.Protocol.Messages.Playlist;

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

        public static bool CanChangePlaylist
        {
            get
            {
                var lobby = LobbyManager.CurrentLobby;
                if (lobby == null || lobby.Locked || lobby.IsPlaying) return false;
                if (lobby.HostUid == PlayerManager.CurrentUid) return true;
                return lobby.ChartSelection == (byte)LobbyChartSelection.Playlist;
            }
        }

        public static bool IsCurrentChartInPlaylist()
        {
            var entry = ChartManager.GetCurrentEntry();
            return !string.IsNullOrEmpty(entry) && ContainsEntry(entry);
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
            return playlist
                .Select(ChartManager.ParseEntry)
                .Where(item => item != null)
                .ToArray();
        }

        public static string GetPreparationButtonText()
        {
            if (!LobbyManager.IsInLobby) return "PLAY!";
            if (!CanChangePlaylist) return LobbyManager.CurrentLobby?.Locked == true ? "等待准备" : "等待房主选歌";
            if (IsCurrentChartInPlaylist()) return "移除歌曲列表";
            if (IsPlaylistFull()) return "歌曲列表已满";
            return "加入歌曲列表";
        }

        public static bool CanUsePreparationButton()
        {
            if (!LobbyManager.IsInLobby) return true;
            if (!CanChangePlaylist) return false;
            return IsCurrentChartInPlaylist() || !IsPlaylistFull();
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

            await AddAsync(entry);
            return PlaylistToggleResult.Added;
        }

        public static async Task AddAsync(string entry)
        {
            EnsureReady();
            await PlayerManager.SyncCustomChartsAsync();
            var response = await NetworkClient.Instance.SendRequestAsync<PlaylistAddRequest, PlaylistAddResponse>(
                OpCodes.PlaylistAddReq,
                new PlaylistAddRequest { Entry = entry });

            var result = response?.Result ?? AddSuccess;
            if (result == AddSuccess) return;

            if (result == AddHidden)
            {
                throw new System.InvalidOperationException("有人隐藏了此谱面。");
            }

            if (result == AddNotSynced)
            {
                throw new System.InvalidOperationException("有人没有此自制谱面。");
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
            var response = await NetworkClient.Instance.SendRequestAsync<LobbyLockRequest, LobbyLockResponse>(
                OpCodes.LobbyLockReq,
                new LobbyLockRequest { Locked = true });

            if (response != null && response.Result != 0)
            {
                throw new System.InvalidOperationException($"开始准备失败，错误码 {response.Result}。");
            }
        }

        public static async Task SetReadyAsync(bool ready)
        {
            EnsureReady();
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

            if (!LobbyManager.IsInLobby)
            {
                throw new System.InvalidOperationException("Not in lobby.");
            }
        }
    }
}
