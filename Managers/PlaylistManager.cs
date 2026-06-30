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
            if (!LobbyManager.IsInLobby) return I18nManager.T("prepare.play");
            if (IsRookieReadySelectionActive())
            {
                if (ChartManager.IsCurrentSelectionLockedByPlaylistVariant(GetCurrentPlaylistEntry()))
                {
                    return I18nManager.T("playlist.variant_locked");
                }

                return IsLocalPlayerReady() ? I18nManager.T("playlist.ready") : I18nManager.T("playlist.select_and_ready");
            }

            if (!CanChangePlaylist) return LobbyManager.CurrentLobby?.Locked == true ? I18nManager.T("playlist.wait_ready") : I18nManager.T("playlist.wait_host");
            var entry = ChartManager.GetCurrentEntry();
            if (string.IsNullOrEmpty(entry)) return I18nManager.T("playlist.no_chart");
            if (ContainsEntry(entry))
            {
                var actualEntry = FindActualEntry(entry) ?? entry;
                if (IsTenziMode() && !IsTenziEntryOwner(actualEntry, PlayerManager.CurrentUid))
                {
                    return I18nManager.T("playlist.remove_own_only");
                }

                return I18nManager.T("playlist.remove");
            }

            if (IsCurrentChartUnsupported()) return GetCurrentChartUnsupportedMessage(entry);
            if (IsPlaylistFull()) return I18nManager.T("playlist.full");
            if (IsTenziMode() && HasReachedLocalTenziEntryLimit()) return I18nManager.Tf("playlist.tenzi_limit", GetTenziSongsPerPlayerLimit());
            if (IsTenziRoundClosed()) return I18nManager.T("playlist.round_closed");
            return I18nManager.T("playlist.add");
        }

        public static bool CanUsePreparationButton()
        {
            if (!LobbyManager.IsInLobby) return true;
            if (IsRookieReadySelectionActive())
            {
                return !IsLocalPlayerReady() &&
                       ChartManager.TryGetCurrentReadyDifficultyForEntry(GetCurrentPlaylistEntry(), out _);
            }

            if (!CanChangePlaylist) return false;
            var entry = ChartManager.GetCurrentEntry();
            if (string.IsNullOrEmpty(entry)) return false;
            if (ContainsEntry(entry))
            {
                var actualEntry = FindActualEntry(entry) ?? entry;
                return !IsTenziMode() || IsTenziEntryOwner(actualEntry, PlayerManager.CurrentUid);
            }

            if (IsTenziMode() && HasReachedLocalTenziEntryLimit()) return false;
            if (IsTenziRoundClosed()) return false;
            return !IsEntryUnsupportedForCurrentLobby(entry) && !IsPlaylistFull();
        }

        public static async Task<PlaylistToggleResult> ToggleCurrentChartAsync()
        {
            EnsureReady();
            var entry = ChartManager.GetCurrentEntry();
            if (string.IsNullOrEmpty(entry))
            {
                throw new System.InvalidOperationException(I18nManager.T("playlist.no_chart"));
            }

            if (ContainsEntry(entry))
            {
                var actualEntry = FindActualEntry(entry) ?? entry;
                if (IsTenziMode() && !IsTenziEntryOwner(actualEntry, PlayerManager.CurrentUid))
                {
                    throw new System.InvalidOperationException(I18nManager.T("playlist.remove_own_only"));
                }

                await RemoveAsync(entry);
                return PlaylistToggleResult.Removed;
            }

            EnsureEntrySupported(entry);
            EnsureEntryAllowedByPlayMode(entry);
            EnsurePlaylistHasSpace();
            EnsureEntryAllowedByTenziMode();
            await AddAsync(entry);
            return PlaylistToggleResult.Added;
        }

        public static async Task AddAsync(string entry)
        {
            EnsureReady();
            EnsureEntrySupported(entry);
            EnsureEntryAllowedByPlayMode(entry);
            EnsurePlaylistHasSpace();
            EnsureEntryAllowedByTenziMode();
            await PlayerManager.SyncChartStateAsync();
            var response = await NetworkClient.Instance.SendRequestAsync<PlaylistAddRequest, PlaylistAddResponse>(
                OpCodes.PlaylistAddReq,
                new PlaylistAddRequest { Entry = entry });

            var result = response?.Result ?? AddSuccess;
            if (result == AddSuccess) return;

            if (result == AddHidden)
            {
                throw new System.InvalidOperationException(I18nManager.T("playlist.hidden_by_someone"));
            }

            if (result == AddNotSynced)
            {
                throw new System.InvalidOperationException(I18nManager.T("playlist.missing_by_someone"));
            }

            if (result == AddUnsupported)
            {
                throw new System.InvalidOperationException(I18nManager.T("playlist.unsupported"));
            }

            throw new System.InvalidOperationException(I18nManager.Tf("playlist.add_failed", result));
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
                throw new System.InvalidOperationException(I18nManager.T("playlist.missing_by_someone"));
            }

            if (response.Result == LockUnsupported)
            {
                throw new System.InvalidOperationException(I18nManager.T("playlist.lock_unsupported"));
            }

            if (response.Result != LockSuccess)
            {
                throw new System.InvalidOperationException(I18nManager.Tf("playlist.start_failed", response.Result));
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
                    ChartName = I18nManager.T("playlist.invalid.title")
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
                throw new System.InvalidOperationException(I18nManager.T("playlist.unsupported"));
            }
        }

        private static void EnsureEntryAllowedByPlayMode(string entry)
        {
            if (IsRookieMode() && IsEntryUnsupportedByRookieMode(entry))
            {
                throw new System.InvalidOperationException(I18nManager.T("playlist.rookie_touhou_spell"));
            }

            if (!IsFearlessMode()) return;

            if (!IsEntryAllowedByFearlessMode(entry))
            {
                throw new System.InvalidOperationException(I18nManager.T("playlist.fearless_difficulty"));
            }
        }

        private static void EnsurePlaylistSupported()
        {
            var playlist = LobbyManager.CurrentLobby?.Playlist;
            if (playlist == null) return;

            if (playlist.Any(ChartSelectionRules.IsUnsupportedPlaylistEntry))
            {
                throw new System.InvalidOperationException(I18nManager.T("playlist.lock_unsupported"));
            }

            if (IsRookieMode() && playlist.Any(IsEntryUnsupportedByRookieMode))
            {
                throw new System.InvalidOperationException(I18nManager.T("playlist.rookie_touhou_spell"));
            }

            if (IsFearlessMode() && playlist.Any(entry => !IsEntryAllowedByFearlessMode(entry)))
            {
                throw new System.InvalidOperationException(I18nManager.T("playlist.fearless_difficulty"));
            }
        }

        private static bool IsEntryUnsupportedForCurrentLobby(string entry)
        {
            if (ChartSelectionRules.IsUnsupportedPlaylistEntry(entry)) return true;
            if (IsRookieMode() && IsEntryUnsupportedByRookieMode(entry)) return true;
            if (!IsFearlessMode()) return false;

            return !IsEntryAllowedByFearlessMode(entry);
        }

        private static string GetCurrentChartUnsupportedMessage(string entry)
        {
            if (IsRookieMode() && IsEntryUnsupportedByRookieMode(entry))
            {
                return I18nManager.T("playlist.rookie_touhou_spell");
            }

            if (IsFearlessMode() && !IsEntryAllowedByFearlessMode(entry))
            {
                return I18nManager.T("playlist.fearless_difficulty");
            }

            return I18nManager.T("playlist.unsupported");
        }

        private static bool IsEntryAllowedByFearlessMode(string entry)
        {
            var parsed = ChartManager.ParseEntry(entry);
            return ChartSelectionRules.IsFearlessAllowedChart(parsed?.ChartKey, parsed?.Difficulty ?? 0);
        }

        private static bool IsEntryUnsupportedByRookieMode(string entry)
        {
            var parsed = ChartManager.ParseEntry(entry);
            return ChartSelectionRules.IsRookieUnsupportedChart(parsed?.ChartKey, parsed?.Difficulty ?? 0);
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

        public static int GetLocalTenziEntryCount()
        {
            return IsTenziMode() ? GetTenziEntryCountFromPlayer(PlayerManager.CurrentUid) : 0;
        }

        public static int GetTenziSongsPerPlayerLimit()
        {
            var lobby = LobbyManager.CurrentLobby;
            return LobbyPlayModeRules.NormalizeTenziSongsPerPlayer(
                lobby?.TenziSongsPerPlayer ?? 0,
                lobby?.PlaylistSize ?? LobbyPlayModeRules.DefaultTenziSongsPerPlayer);
        }

        public static bool HasReachedLocalTenziEntryLimit()
        {
            return IsTenziMode() && GetLocalTenziEntryCount() >= GetTenziSongsPerPlayerLimit();
        }

        public static string FormatPlaylistFailureMessage(string reason)
        {
            if (string.IsNullOrWhiteSpace(reason)) return I18nManager.T("common.unknown_error");
            if (reason == "游戏准备或进行中，不能修改歌曲列表") return I18nManager.T("playlist.locked_blocked");
            if (reason == "游戏进行中，不能修改歌曲列表") return I18nManager.T("playlist.playing_blocked");
            if (reason == "你已被禁止选谱") return I18nManager.T("playlist.chart_selection_banned");
            if (reason == "歌曲列表已满") return I18nManager.T("playlist.full");
            if (reason == "歌曲已在列表中") return I18nManager.T("playlist.already_added");
            if (reason == "歌曲不在列表中") return I18nManager.T("playlist.not_found");
            if (reason == "天子模式本轮已封盘，请先移除本轮谱面") return I18nManager.T("playlist.tenzi_round_remove");
            if (reason == "天子模式只能移除自己选择的谱面") return I18nManager.T("playlist.remove_own_only");
            if (reason.StartsWith("天子模式每人最多选择", System.StringComparison.Ordinal))
            {
                return I18nManager.Tf(
                    "playlist.tenzi_limit",
                    ProtocolReasonText.TryReadLastPositiveInt(reason, out var limit) ? limit : GetTenziSongsPerPlayerLimit());
            }

            return reason;
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
            if (lobby?.IsPlaying == true) return I18nManager.T("playlist.playing_blocked");
            if (IsTenziMode() && !IsTenziEntryOwner(item?.Entry, PlayerManager.CurrentUid))
            {
                return I18nManager.T("playlist.remove_own_only");
            }

            return I18nManager.T("playlist.locked_blocked");
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
                throw new System.InvalidOperationException(I18nManager.T("player.not_in_room"));
            }
        }

        private static void EnsureEntryAllowedByTenziMode()
        {
            if (IsTenziMode() && HasReachedLocalTenziEntryLimit())
            {
                throw new System.InvalidOperationException(I18nManager.Tf("playlist.tenzi_limit", GetTenziSongsPerPlayerLimit()));
            }

            if (IsTenziRoundClosed())
            {
                throw new System.InvalidOperationException(I18nManager.T("playlist.tenzi_round_remove"));
            }
        }

        private static void EnsurePlaylistHasSpace()
        {
            if (IsPlaylistFull())
            {
                throw new System.InvalidOperationException(I18nManager.T("playlist.full"));
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
            return GetTenziEntryCountFromPlayer(uid) > 0;
        }

        private static int GetTenziEntryCountFromPlayer(string uid)
        {
            var owners = LobbyManager.CurrentLobby?.PlaylistOwners;
            if (owners == null || string.IsNullOrWhiteSpace(uid)) return 0;
            return owners.Count(owner => owner?.Uid == uid);
        }

        private static bool IsTenziEntryOwner(string entry, string uid)
        {
            var owners = LobbyManager.CurrentLobby?.PlaylistOwners;
            if (owners == null || string.IsNullOrWhiteSpace(entry) || string.IsNullOrWhiteSpace(uid)) return false;
            return owners.Any(owner => owner?.Entry == entry && owner.Uid == uid);
        }
    }
}
