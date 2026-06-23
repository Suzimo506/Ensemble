using Il2Cpp;
using Il2CppAssets.Scripts.Database;
using Il2CppAssets.Scripts.UI.Controls;
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
                    "本地选歌界面未准备好，未能进入联机游戏。");
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
                entry.Difficulty < 0 ||
                entry.Difficulty > 4 ||
                ChartSelectionRules.IsUnsupportedChartKey(entry.ChartKey))
            {
                ReportStartFailure(
                    Managers.LobbyManager.CurrentLobby?.Id ?? 0,
                    Managers.LobbyManager.CurrentLobby?.CurrentBattleId,
                    entryText,
                    "InvalidPlaylistEntry",
                    "联机歌曲信息无效，未能进入游戏。");
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
                    $"本地缺少谱面 {entry.ChartKey}，未能进入联机游戏。");
                return true;
            }

            var syncKey = $"{battleId}:{entry.ChartKey}";
            if (BattleStartSyncs.Add(syncKey))
            {
                Managers.PlayerManager.SyncChartStateFireAndForget();
                return false;
            }

            if (ShouldNavigateToBattleChart(battleId, retriesRemaining))
            {
                NativeChartNavigator.JumpToChart(musicInfo);
                _navigatedBattleId = battleId;
            }

            SyncSelectedChart(musicInfo, entry.Difficulty);

            if (!IsNativeChartSelectionReady(musicInfo))
            {
                return false;
            }

            if (CustomAlbumsWindowGuard.CloseIfOpen("battle start"))
            {
                ShowText.ShowInfo("已关闭自制谱窗口，正在重新尝试进入多人游戏");
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

        private static void SyncSelectedChart(MusicInfo musicInfo, int difficulty)
        {
            HiddenDifficultyController.Sync(musicInfo, difficulty);
            GlobalDataBase.dbMusicTag.selectedDiffTglIndex = difficulty == 4 ? 3 : difficulty;
            GlobalDataBase.dbMusicTag.pnlSelectMusicUid = musicInfo.uid;
            GlobalDataBase.dbMusicTag.m_CurSelectedMusicInfo = musicInfo;
        }

        private static bool IsNativeChartSelectionReady(MusicInfo musicInfo)
        {
            if (musicInfo == null || string.IsNullOrEmpty(musicInfo.uid)) return false;

            var currentInfo = GlobalDataBase.dbMusicTag.m_CurSelectedMusicInfo;
            if (currentInfo == null || currentInfo.Pointer == System.IntPtr.Zero) return false;
            if (currentInfo.uid != musicInfo.uid) return false;

            var selectedUid = GlobalDataBase.dbMusicTag.pnlSelectMusicUid;
            if (!string.IsNullOrEmpty(selectedUid) && selectedUid != musicInfo.uid) return false;

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
                    $"本地缺少谱面 {chartKey}，未能进入联机游戏。");
            });
        }
    }
}

