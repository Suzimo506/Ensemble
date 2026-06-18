using Il2Cpp;
using Il2CppAssets.Scripts.Database;
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
            if (string.IsNullOrWhiteSpace(battleEntry)) return;
            if (_startedLobbyId == lobby.Id && _startedBattleEntry == battleEntry) return;

            _startedLobbyId = lobby.Id;
            _startedBattleEntry = battleEntry;
            ScheduleStartCurrentPlaylistEntry(lobby.Id, battleEntry);
        }

        public static void Reset()
        {
            _startGeneration++;
            _startedLobbyId = 0;
            _startedBattleEntry = null;
        }

        private static void ScheduleStartCurrentPlaylistEntry(int lobbyId, string entryText)
        {
            var generation = ++_startGeneration;
            MainThreadDispatcher.Enqueue(() => RunStartRetry(generation, lobbyId, entryText, StartRetryCount, 0));
        }

        private static void RunStartRetry(int generation, int lobbyId, string entryText, int retriesRemaining, int delayFrames)
        {
            if (generation != _startGeneration) return;

            var lobby = Managers.LobbyManager.CurrentLobby;
            if (lobby == null || lobby.Id != lobbyId || !lobby.IsPlaying) return;
            if (_startedLobbyId != lobbyId || _startedBattleEntry != entryText) return;

            if (delayFrames > 0)
            {
                MainThreadDispatcher.Enqueue(() => RunStartRetry(generation, lobbyId, entryText, retriesRemaining, delayFrames - 1));
                return;
            }

            if (TryStartCurrentPlaylistEntry(entryText))
            {
                return;
            }

            if (retriesRemaining <= 1)
            {
                MelonLogger.Warning("Cannot start multiplayer battle: chart selection was not ready after retries.");
                if (_startedLobbyId == lobbyId && _startedBattleEntry == entryText)
                {
                    _startedLobbyId = 0;
                    _startedBattleEntry = null;
                }

                return;
            }

            MainThreadDispatcher.Enqueue(() => RunStartRetry(generation, lobbyId, entryText, retriesRemaining - 1, StartRetryDelayFrames));
        }

        private static bool TryStartCurrentPlaylistEntry(string entryText)
        {
            var entry = Managers.ChartManager.ParseEntry(entryText);
            if (entry == null)
            {
                MelonLogger.Warning("Cannot start multiplayer battle: playlist entry is missing.");
                return true;
            }

            var musicInfo = Managers.ChartManager.GetMusicInfo(entry.ChartKey);
            if (musicInfo == null)
            {
                MelonLogger.Warning($"Cannot start multiplayer battle: chart {entry.ChartKey} not found locally.");
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
    }
}

