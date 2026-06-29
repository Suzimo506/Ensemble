using Il2Cpp;
using Il2CppAssets.Scripts.Database;
using Il2CppAssets.Scripts.UI.Controls;
using MDEN.Managers;
using MDEN.Protocol.Rules;
using UnityEngine;

namespace MDEN.UI.Core
{
    public static class MultiplayerBattleController
    {
        private const int StartRetryCount = 60;
        private const int StartRetryDelayFrames = 3;
        private const int StartNavigationRetryInterval = 10;
        private static int _startedLobbyId;
        private static string _startedBattleId;
        private static string _startedBattleEntry;
        private static string _navigatedBattleId;
        private static int _startGeneration;
        private static readonly System.Collections.Generic.HashSet<string> BattleStartSyncs = new System.Collections.Generic.HashSet<string>();
        private static readonly System.Collections.Generic.HashSet<string> MissingChartSyncs = new System.Collections.Generic.HashSet<string>();

        public static void OnLobbyChanged()
        {
            var lobby = Managers.LobbyManager.CurrentLobby;
            if (lobby == null || !lobby.IsPlaying)
            {
                if (lobby != null &&
                    lobby.Locked &&
                    !lobby.IsPlaying &&
                    Managers.PlaylistManager.IsRookieMode())
                {
                    NavigateToRookieReadyChart();
                }

                Reset();
                if (!Managers.BattleResultFlowManager.IsHoldingBattleResult)
                {
                    Managers.BattleManager.Reset();
                }
                return;
            }

            if (lobby.ReadyPlayers == null ||
                System.Array.IndexOf(lobby.ReadyPlayers, Managers.PlayerManager.CurrentUid) < 0)
            {
                return;
            }

            var battleEntry = !string.IsNullOrWhiteSpace(lobby.CurrentBattleEntry)
                ? lobby.CurrentBattleEntry
                : (lobby.Playlist != null && lobby.Playlist.Length > 0 ? lobby.Playlist[0] : null);
            if (string.IsNullOrWhiteSpace(battleEntry) || string.IsNullOrWhiteSpace(lobby.CurrentBattleId)) return;
            if (_startedLobbyId == lobby.Id && _startedBattleId == lobby.CurrentBattleId) return;

            _startedLobbyId = lobby.Id;
            _startedBattleId = lobby.CurrentBattleId;
            _startedBattleEntry = battleEntry;
            ScheduleStartCurrentPlaylistEntry(lobby.Id, lobby.CurrentBattleId, battleEntry);
        }

        public static bool NavigateToRookieReadyChart()
        {
            var entry = Managers.PlaylistManager.GetCurrentPlaylistEntry();
            if (entry == null) return false;

            var difficulty = ResolveRookieReadyDifficulty(entry);
            if (Managers.ChartManager.IsCurrentSelectedChart(entry) &&
                IsRookieDifficultySelectionReady(Managers.ChartManager.CurrentMusicInfo, difficulty))
            {
                return true;
            }

            var musicInfo = Managers.ChartManager.GetMusicInfo(entry.ChartKey);
            if (musicInfo == null)
            {
                return false;
            }

            NativeChartNavigator.JumpToChart(entry.ChartKey, musicInfo, difficulty);
            return true;
        }

        public static void Reset()
        {
            _startGeneration++;
            _startedLobbyId = 0;
            _startedBattleId = null;
            _startedBattleEntry = null;
            _navigatedBattleId = null;
            BattleStartSyncs.Clear();
            MissingChartSyncs.Clear();
        }

        private static void ScheduleStartCurrentPlaylistEntry(int lobbyId, string battleId, string entryText)
        {
            var generation = ++_startGeneration;
            MainThreadDispatcher.Enqueue(() => RunStartRetry(generation, lobbyId, battleId, entryText, StartRetryCount, 0));
        }

        private static void RunStartRetry(int generation, int lobbyId, string battleId, string entryText, int retriesRemaining, int delayFrames)
        {
            if (generation != _startGeneration) return;

            var lobby = Managers.LobbyManager.CurrentLobby;
            if (lobby == null || lobby.Id != lobbyId || !lobby.IsPlaying) return;
            if (lobby.CurrentBattleId != battleId ||
                _startedLobbyId != lobbyId ||
                _startedBattleId != battleId ||
                _startedBattleEntry != entryText) return;

            if (delayFrames > 0)
            {
                MainThreadDispatcher.Enqueue(() => RunStartRetry(generation, lobbyId, battleId, entryText, retriesRemaining, delayFrames - 1));
                return;
            }

            if (TryStartCurrentPlaylistEntry(battleId, entryText, retriesRemaining))
            {
                return;
            }

            if (retriesRemaining <= 1)
            {
                ReportStartFailure(
                    lobbyId,
                    battleId,
                    entryText,
                    "SelectionNotReady",
                    I18nManager.T("battle.start.selection_not_ready"));
                if (_startedLobbyId == lobbyId && _startedBattleId == battleId && _startedBattleEntry == entryText)
                {
                    _startedLobbyId = 0;
                    _startedBattleId = null;
                    _startedBattleEntry = null;
                }

                return;
            }

            MainThreadDispatcher.Enqueue(() => RunStartRetry(generation, lobbyId, battleId, entryText, retriesRemaining - 1, StartRetryDelayFrames));
        }

        private static bool TryStartCurrentPlaylistEntry(string battleId, string entryText, int retriesRemaining)
        {
            var entry = Managers.ChartManager.ParseEntry(entryText);
            if (entry == null ||
                !DifficultyDisplayRules.IsKnownDifficulty(entry.Difficulty) ||
                ChartSelectionRules.IsUnsupportedChartKey(entry.ChartKey))
            {
                ReportStartFailure(
                    Managers.LobbyManager.CurrentLobby?.Id ?? 0,
                    Managers.LobbyManager.CurrentLobby?.CurrentBattleId,
                    entryText,
                    "InvalidPlaylistEntry",
                    I18nManager.T("battle.start.invalid_entry"));
                return true;
            }

            var musicInfo = Managers.ChartManager.GetMusicInfo(entry.ChartKey);
            if (musicInfo == null)
            {
                var missingSyncKey = $"{battleId}:{entry.ChartKey}";
                if (MissingChartSyncs.Add(missingSyncKey))
                {
                    _ = SyncMissingChartThenReportAsync(
                        Managers.LobbyManager.CurrentLobby?.Id ?? 0,
                        Managers.LobbyManager.CurrentLobby?.CurrentBattleId,
                        entryText,
                        entry.ChartKey);
                    return true;
                }

                ReportStartFailure(
                    Managers.LobbyManager.CurrentLobby?.Id ?? 0,
                    Managers.LobbyManager.CurrentLobby?.CurrentBattleId,
                    entryText,
                    "ChartMissing",
                    I18nManager.Tf("battle.start.chart_missing", entry.ChartKey));
                return true;
            }

            if (Managers.PlaylistManager.IsRookieMode())
            {
                var selectedDifficulty = Managers.LobbyManager.GetCurrentBattleDifficulty(Managers.PlayerManager.CurrentUid);
                if (!DifficultyDisplayRules.IsKnownDifficulty(selectedDifficulty))
                {
                    ReportStartFailure(
                        Managers.LobbyManager.CurrentLobby?.Id ?? 0,
                        Managers.LobbyManager.CurrentLobby?.CurrentBattleId,
                        entryText,
                        "DifficultyMissing",
                        I18nManager.T("battle.start.difficulty_missing"));
                    return true;
                }

                if (!Managers.ChartManager.IsEntryDifficultyPlayable(entry, selectedDifficulty))
                {
                    ReportStartFailure(
                        Managers.LobbyManager.CurrentLobby?.Id ?? 0,
                        Managers.LobbyManager.CurrentLobby?.CurrentBattleId,
                        entryText,
                        "DifficultyUnavailable",
                        I18nManager.T("prepare.valid_difficulty"));
                    return true;
                }

                entry.Difficulty = selectedDifficulty;
            }

            var syncKey = $"{battleId}:{entry.ChartKey}";
            if (BattleStartSyncs.Add(syncKey))
            {
                Managers.PlayerManager.SyncChartStateFireAndForget();
                return false;
            }

            if (ShouldNavigateToBattleChart(battleId, retriesRemaining))
            {
                NativeChartNavigator.JumpToChart(entry.ChartKey, musicInfo, entry.Difficulty);
                _navigatedBattleId = battleId;
            }

            SyncSelectedChart(entry.ChartKey, musicInfo, entry.Difficulty);

            if (!IsNativeChartSelectionReady(entry.ChartKey, musicInfo))
            {
                return false;
            }

            if (Managers.PlaylistManager.IsRookieMode() &&
                !IsRookieDifficultySelectionReady(musicInfo, entry.Difficulty))
            {
                return false;
            }

            if (CustomAlbumsWindowGuard.CloseIfOpen("battle start"))
            {
                ShowText.ShowInfo(I18nManager.T("battle.start.custom_closed_retry"));
                return false;
            }

            Managers.BattleManager.MarkMultiplayerBattleStarting();
            BattleHelper.GameBattleStart(new Il2CppSystem.Object());
            return true;
        }

        private static bool ShouldNavigateToBattleChart(string battleId, int retriesRemaining)
        {
            if (_navigatedBattleId != battleId) return true;
            return retriesRemaining > 0 && retriesRemaining % StartNavigationRetryInterval == 0;
        }

        private static int ResolveRookieReadyDifficulty(Managers.PlaylistEntryViewModel entry)
        {
            if (entry == null) return 0;

            if (Managers.ChartManager.TryGetCurrentReadyDifficultyForEntry(entry, out var currentDifficulty))
            {
                return currentDifficulty;
            }

            return entry.Difficulty;
        }

        private static void SyncSelectedChart(string chartKey, MusicInfo musicInfo, int difficulty)
        {
            NativeChartSelectionSync.Sync(musicInfo, difficulty, chartKey);
        }

        private static bool IsRookieDifficultySelectionReady(MusicInfo musicInfo, int difficulty)
        {
            if (difficulty == 4)
            {
                return HiddenDifficultyController.IsHiddenDifficultySelected(
                    musicInfo,
                    GlobalDataBase.dbMusicTag.selectedDiffTglIndex);
            }

            if (difficulty == 3)
            {
                return GlobalDataBase.dbMusicTag.selectedDiffTglIndex == 3 &&
                       !HiddenDifficultyController.IsHiddenInvoked(musicInfo);
            }

            if (difficulty == DifficultyDisplayRules.Spell)
            {
                return SpecialDifficultyController.IsSpecialDifficultySelected(
                    musicInfo,
                    GlobalDataBase.dbMusicTag.selectedDiffTglIndex);
            }

            return GlobalDataBase.dbMusicTag.selectedDiffTglIndex == difficulty;
        }

        private static bool IsNativeChartSelectionReady(string chartKey, MusicInfo musicInfo)
        {
            if (musicInfo == null || string.IsNullOrEmpty(musicInfo.uid) || string.IsNullOrEmpty(chartKey)) return false;

            var currentInfo = GlobalDataBase.dbMusicTag.m_CurSelectedMusicInfo;
            if (currentInfo == null || currentInfo.Pointer == System.IntPtr.Zero) return false;
            if (currentInfo.uid != musicInfo.uid && currentInfo.uid != chartKey)
            {
                if (!SpecialChartVariantResolver.IsKnownVariantPair(chartKey) ||
                    SpecialChartVariantResolver.GetBaseUid(currentInfo.uid) != SpecialChartVariantResolver.GetBaseUid(chartKey))
                {
                    return false;
                }
            }

            var selectedUid = GlobalDataBase.dbMusicTag.pnlSelectMusicUid;
            if (!string.IsNullOrEmpty(selectedUid) && selectedUid != chartKey) return false;

            var stage = GameObject.Find("UI/Standerd/PnlStage");
            return stage != null && stage.activeInHierarchy;
        }

        private static void ReportStartFailure(
            int lobbyId,
            string battleId,
            string entryText,
            string reasonCode,
            string reason)
        {
            MDEN.Managers.ClientLogManager.Warning($"Cannot start multiplayer battle: {reasonCode}, {reason}");
            ShowText.ShowInfo(reason);
            Managers.BattleManager.ReportBattleStartFailed(lobbyId, battleId, entryText, reasonCode, reason);
        }

        private static async System.Threading.Tasks.Task SyncMissingChartThenReportAsync(
            int lobbyId,
            string battleId,
            string entryText,
            string chartKey)
        {
            try
            {
                await Managers.PlayerManager.SyncChartStateAsync();
            }
            catch (System.Exception ex)
            {
                MDEN.Managers.ClientLogManager.Warning($"Sync chart state before missing-chart report failed: {ex.Message}");
            }

            MainThreadDispatcher.Enqueue(() =>
            {
                ReportStartFailure(
                    lobbyId,
                    battleId,
                    entryText,
                    "ChartMissing",
                    I18nManager.Tf("battle.start.chart_missing", chartKey));
            });
        }
    }
}

