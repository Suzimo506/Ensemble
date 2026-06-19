using Il2Cpp;
using Il2CppAssets.Scripts.Database;
using Il2CppAssets.Scripts.UI.Controls;
using MDEN.Patches;
using MelonLoader;
using UnityEngine;

namespace MDEN.UI.Core
{
    public static class MultiplayerBattleController
    {
        private const int StartRetryCount = 20;
        private const int StartRetryDelayFrames = 3;
        private static int _startedLobbyId;
        private static string _startedBattleId;
        private static string _startedBattleEntry;
        private static int _startGeneration;

        public static void OnLobbyChanged()
        {
            var lobby = Managers.LobbyManager.CurrentLobby;
            if (lobby == null || !lobby.IsPlaying)
            {
                Reset();
                if (!BattleFlowPatch.IsHoldingBattleResult)
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

            if (TryStartCurrentPlaylistEntry(entryText))
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

        private static bool TryStartCurrentPlaylistEntry(string entryText)
        {
            var entry = Managers.ChartManager.ParseEntry(entryText);
            if (entry == null)
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
                ReportStartFailure(
                    Managers.LobbyManager.CurrentLobby?.Id ?? 0,
                    Managers.LobbyManager.CurrentLobby?.CurrentBattleId,
                    entryText,
                    "ChartMissing",
                    $"本地缺少谱面 {entry.ChartKey}，未能进入联机游戏。");
                return true;
            }

            NativeChartNavigator.JumpToChart(musicInfo);
            HiddenDifficultyController.Sync(musicInfo, entry.Difficulty);
            GlobalDataBase.dbMusicTag.selectedDiffTglIndex = entry.Difficulty == 4 ? 3 : entry.Difficulty;
            GlobalDataBase.dbMusicTag.pnlSelectMusicUid = musicInfo.uid;
            GlobalDataBase.dbMusicTag.m_CurSelectedMusicInfo = musicInfo;

            if (!IsNativeChartSelectionReady(musicInfo))
            {
                return false;
            }

            BattleFlowPatch.MarkMultiplayerBattleStarting();
            BattleHelper.GameBattleStart(new Il2CppSystem.Object());
            return true;
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
    }
}

