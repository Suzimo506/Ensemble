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
        private const string FearlessDifficultyMessage = "无畏模式只能选择大触或隐藏难度";
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
                return lobby != null && !lobby.Locked && !lobby.IsPlaying;
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
                   IsEntryUnsupportedForCurrentLobby(entry);
        }

        public static bool ContainsEntry(string entry)
        {
            var playlist = LobbyManager.CurrentLobby?.Playlist;
            return playlist != null && playlist.Any(item => IsSamePlaylistEntryForCurrentMode(item, entry));
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
            if (IsRookieReadySelectionActive())
            {
                return IsLocalPlayerReady() ? "已准备" : "选择并准备";
            }

            if (!CanChangePlaylist) return LobbyManager.CurrentLobby?.Locked == true ? "等待准备" : "等待房主选歌";
            var entry = ChartManager.GetCurrentEntry();
            if (string.IsNullOrEmpty(entry)) return "未选择谱面";
            if (ContainsEntry(entry))
            {
                var actualEntry = FindActualEntry(entry) ?? entry;
                if (IsTenziMode() && !IsTenziEntryOwner(actualEntry, PlayerManager.CurrentUid))
                {
                    return "只能移除自己的谱面";
                }

                return "移除歌曲列表";
            }

            if (IsCurrentChartUnsupported()) return GetCurrentChartUnsupportedMessage(entry);
            if (IsTenziMode() && HasLocalTenziEntry()) return "先移除自己的谱面";
            if (IsTenziRoundClosed()) return "本轮已封盘";
            if (IsPlaylistFull()) return "歌曲列表已满";
            return "加入歌曲列表";
        }

        public static bool CanUsePreparationButton()
        {
            if (!LobbyManager.IsInLobby) return true;
            if (IsRookieReadySelectionActive())
            {
                return !IsLocalPlayerReady() && ChartManager.IsCurrentSelectedChart(PlaylistManager.GetCurrentPlaylistEntry());
            }

            if (!CanChangePlaylist) return false;
            var entry = ChartManager.GetCurrentEntry();
            if (string.IsNullOrEmpty(entry)) return false;
            if (ContainsEntry(entry))
            {
                var actualEntry = FindActualEntry(entry) ?? entry;
                return !IsTenziMode() || IsTenziEntryOwner(actualEntry, PlayerManager.CurrentUid);
            }

            if (IsTenziMode() && HasLocalTenziEntry()) return false;
            if (IsTenziRoundClosed()) return false;
            return !IsEntryUnsupportedForCurrentLobby(entry) && !IsPlaylistFull();
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
                var actualEntry = FindActualEntry(entry) ?? entry;
                if (IsTenziMode() && !IsTenziEntryOwner(actualEntry, PlayerManager.CurrentUid))
                {
                    throw new System.InvalidOperationException("天子模式只能移除自己选择的谱面");
                }

                await RemoveAsync(entry);
                return PlaylistToggleResult.Removed;
            }

            EnsureEntrySupported(entry);
            EnsureEntryAllowedByPlayMode(entry);
            EnsureEntryAllowedByTenziMode();
            await AddAsync(entry);
            return PlaylistToggleResult.Added;
        }

        public static async Task AddAsync(string entry)
        {
            EnsureReady();
            EnsureEntrySupported(entry);
            EnsureEntryAllowedByPlayMode(entry);
            EnsureEntryAllowedByTenziMode();
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
            await SetReadyAsync(ready, 0);
        }

        public static async Task SetReadyAsync(bool ready, int difficulty)
        {
            EnsureReady();
            if (ready)
            {
                await PlayerManager.SyncChartStateAsync();
            }

            await NetworkClient.Instance.SendRequestAsync<LobbyReadyRequest, LobbyReadyResponse>(
                OpCodes.LobbyReadyReq,
                new LobbyReadyRequest { Ready = ready, Difficulty = difficulty });
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
            return playlist?.FirstOrDefault(item => IsSamePlaylistEntryForCurrentMode(item, entry));
        }

        private static bool IsSamePlaylistEntryForCurrentMode(string left, string right)
        {
            if (!IsRookieMode()) return ChartManager.IsSameChart(left, right);

            var leftEntry = ChartManager.ParseEntry(left);
            var rightEntry = ChartManager.ParseEntry(right);
            return leftEntry != null &&
                   rightEntry != null &&
                   leftEntry.ChartKey == rightEntry.ChartKey;
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

            if (IsTenziMode() && !string.IsNullOrWhiteSpace(lobby.TenziSelectedEntry))
            {
                return lobby.TenziSelectedEntry;
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

        private static void EnsureEntryAllowedByPlayMode(string entry)
        {
            if (!IsFearlessMode()) return;

            if (!IsEntryAllowedByFearlessMode(entry))
            {
                throw new System.InvalidOperationException(FearlessDifficultyMessage);
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

            if (IsFearlessMode() && playlist.Any(entry => !IsEntryAllowedByFearlessMode(entry)))
            {
                throw new System.InvalidOperationException(FearlessDifficultyMessage);
            }
        }

        private static bool IsEntryUnsupportedForCurrentLobby(string entry)
        {
            if (ChartSelectionRules.IsUnsupportedPlaylistEntry(entry)) return true;
            if (!IsFearlessMode()) return false;

            return !IsEntryAllowedByFearlessMode(entry);
        }

        private static string GetCurrentChartUnsupportedMessage(string entry)
        {
            if (IsFearlessMode() && !IsEntryAllowedByFearlessMode(entry))
            {
                return FearlessDifficultyMessage;
            }

            return UnsupportedChartMessage;
        }

        private static bool IsEntryAllowedByFearlessMode(string entry)
        {
            var parsed = ChartManager.ParseEntry(entry);
            return ChartSelectionRules.IsFearlessAllowedChart(parsed?.ChartKey, parsed?.Difficulty ?? 0);
        }

        public static bool IsRookieMode()
        {
            return LobbyPlayModeRules.IsRookie(LobbyManager.CurrentLobby?.PlayMode ?? 0);
        }

        public static bool IsFearlessMode()
        {
            return LobbyPlayModeRules.IsFearless(LobbyManager.CurrentLobby?.PlayMode ?? 0);
        }

        public static bool IsTenziMode()
        {
            return LobbyPlayModeRules.IsTenzi(LobbyManager.CurrentLobby?.PlayMode ?? 0);
        }

        public static bool HasLocalTenziEntry()
        {
            return IsTenziMode() && HasTenziEntryFromPlayer(PlayerManager.CurrentUid);
        }

        public static bool CanRemovePlaylistEntry(PlaylistEntryViewModel item)
        {
            if (item == null) return false;
            var lobby = LobbyManager.CurrentLobby;
            if (lobby == null || lobby.IsPlaying) return false;
            if (!IsTenziMode()) return !lobby.Locked;
            return IsTenziEntryOwner(item.Entry, PlayerManager.CurrentUid);
        }

        public static string GetPlaylistRemoveBlockedMessage(PlaylistEntryViewModel item)
        {
            var lobby = LobbyManager.CurrentLobby;
            if (lobby?.IsPlaying == true) return "游戏进行中，不能修改歌曲列表";
            if (IsTenziMode() && !IsTenziEntryOwner(item?.Entry, PlayerManager.CurrentUid))
            {
                return "天子模式只能移除自己选择的谱面";
            }

            return "游戏准备或进行中，不能修改歌曲列表";
        }

        public static bool IsRookieReadySelectionActive()
        {
            var lobby = LobbyManager.CurrentLobby;
            return lobby != null &&
                   LobbyPlayModeRules.IsRookie(lobby.PlayMode) &&
                   lobby.Locked &&
                   !lobby.IsPlaying;
        }

        private static void EnsureReady()
        {
            ConnectionManager.EnsureCanSendRequest();

            if (!LobbyManager.IsInLobby)
            {
                throw new System.InvalidOperationException("Not in lobby.");
            }
        }

        private static void EnsureEntryAllowedByTenziMode()
        {
            if (IsTenziMode() && HasLocalTenziEntry())
            {
                throw new System.InvalidOperationException("天子模式请先移除自己选择的谱面");
            }

            if (IsTenziRoundClosed())
            {
                throw new System.InvalidOperationException("天子模式本轮已封盘，请先移除本轮谱面");
            }
        }

        private static bool IsTenziRoundClosed()
        {
            var lobby = LobbyManager.CurrentLobby;
            return IsTenziMode() &&
                   lobby?.TenziRoundClosed == true &&
                   !string.IsNullOrWhiteSpace(lobby.TenziSelectedEntry);
        }

        private static bool HasTenziEntryFromPlayer(string uid)
        {
            var owners = LobbyManager.CurrentLobby?.PlaylistOwners;
            if (owners == null || string.IsNullOrWhiteSpace(uid)) return false;
            return owners.Any(owner => owner?.Uid == uid);
        }

        private static bool IsTenziEntryOwner(string entry, string uid)
        {
            var owners = LobbyManager.CurrentLobby?.PlaylistOwners;
            if (owners == null || string.IsNullOrWhiteSpace(entry) || string.IsNullOrWhiteSpace(uid)) return false;
            return owners.Any(owner => owner?.Entry == entry && owner.Uid == uid);
        }
    }
}
