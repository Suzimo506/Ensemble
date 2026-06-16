using Il2CppAssets.Scripts.Database;
using MDEN.Managers;
using MDEN.Protocol.Messages.Lobby;
using MelonLoader;
using UnityEngine;

namespace MDEN.UI.Core
{
    internal static class ChartPreviewController
    {
        private const int PreviewRetryCount = 8;
        private const int PreviewRetryDelayFrames = 6;
        private const int ResultPanelWaitFrames = 300;
        private static int _previewLobbyId;
        private static string _previewEntry;
        private static int _previewGeneration;
        private static int _resultHoldGeneration;
        private static bool _resultPreviewHeld;
        private static bool _resultPanelSeen;

        public static void OnLobbyChanged(LobbySyncPush lobby)
        {
            if (lobby == null || !lobby.Locked)
            {
                ResetAll();
                return;
            }

            if (lobby.IsPlaying)
            {
                Reset();
                return;
            }

            if (_resultPreviewHeld) return;

            PreviewCurrentLockedLobby(lobby);
        }

        public static void HoldPreviewUntilResultPanelCloses()
        {
            _resultPreviewHeld = true;
            _resultPanelSeen = false;
            var generation = ++_resultHoldGeneration;
            MainThreadDispatcher.Enqueue(() => RunResultPanelHold(generation, ResultPanelWaitFrames));
        }

        public static void Reset()
        {
            _previewGeneration++;
            _previewLobbyId = 0;
            _previewEntry = null;
        }

        public static void ResetAll()
        {
            Reset();
            ClearResultHold();
        }

        private static void PreviewCurrentLockedLobby(LobbySyncPush lobby)
        {
            var entry = PlaylistManager.GetCurrentPlaylistEntry();
            if (entry == null || string.IsNullOrWhiteSpace(entry.Entry))
            {
                Reset();
                return;
            }

            if (_previewLobbyId == lobby.Id && _previewEntry == entry.Entry) return;

            _previewLobbyId = lobby.Id;
            _previewEntry = entry.Entry;
            if (!IsNativeResultPanelVisible())
            {
                Preview(entry);
            }

            SchedulePreviewRetry(lobby.Id, entry.Entry, PreviewRetryCount);
        }

        private static void RunResultPanelHold(int generation, int framesUntilFallbackRelease)
        {
            if (generation != _resultHoldGeneration || !_resultPreviewHeld) return;

            var resultPanelVisible = IsNativeResultPanelVisible();
            if (resultPanelVisible)
            {
                _resultPanelSeen = true;
            }

            if (_resultPanelSeen)
            {
                if (!resultPanelVisible)
                {
                    ReleaseResultHold(generation);
                    return;
                }

                MainThreadDispatcher.Enqueue(() => RunResultPanelHold(generation, framesUntilFallbackRelease));
                return;
            }

            if (framesUntilFallbackRelease <= 0)
            {
                ReleaseResultHold(generation);
                return;
            }

            MainThreadDispatcher.Enqueue(() => RunResultPanelHold(generation, framesUntilFallbackRelease - 1));
        }

        private static void ReleaseResultHold(int generation)
        {
            if (generation != _resultHoldGeneration) return;

            _resultPreviewHeld = false;
            _resultPanelSeen = false;
            PreviewCurrentLockedLobby(LobbyManager.CurrentLobby);
        }

        private static void ClearResultHold()
        {
            _resultHoldGeneration++;
            _resultPreviewHeld = false;
            _resultPanelSeen = false;
        }

        private static void SchedulePreviewRetry(int lobbyId, string entryText, int retriesRemaining)
        {
            var generation = ++_previewGeneration;
            MainThreadDispatcher.Enqueue(() => RunPreviewRetry(generation, lobbyId, entryText, retriesRemaining, PreviewRetryDelayFrames));
        }

        private static void RunPreviewRetry(int generation, int lobbyId, string entryText, int retriesRemaining, int delayFrames)
        {
            if (generation != _previewGeneration) return;
            if (!LobbyManager.IsInLobby || LobbyManager.CurrentLobby?.Id != lobbyId) return;
            if (LobbyManager.CurrentLobby.IsPlaying || !LobbyManager.CurrentLobby.Locked) return;

            if (delayFrames > 0)
            {
                MainThreadDispatcher.Enqueue(() => RunPreviewRetry(generation, lobbyId, entryText, retriesRemaining, delayFrames - 1));
                return;
            }

            var entry = PlaylistManager.GetCurrentPlaylistEntry();
            if (entry == null || entry.Entry != entryText) return;

            if (!IsNativeResultPanelVisible())
            {
                Preview(entry);
            }

            if (retriesRemaining <= 1) return;
            MainThreadDispatcher.Enqueue(() => RunPreviewRetry(generation, lobbyId, entryText, retriesRemaining - 1, PreviewRetryDelayFrames));
        }

        private static bool IsNativeResultPanelVisible()
        {
            return IsVisible("UI_2D/Standard/PnlVictory") ||
                   IsVisible("UI_2D/Standard/PnlFail");
        }

        private static bool IsVisible(string path)
        {
            var obj = GameObject.Find(path);
            return obj != null && obj.activeInHierarchy;
        }

        private static void Preview(PlaylistEntryViewModel entry)
        {
            var musicInfo = ChartManager.GetMusicInfo(entry.ChartKey);
            if (musicInfo == null)
            {
                MelonLogger.Warning($"Cannot preview multiplayer chart: chart {entry.ChartKey} not found locally.");
                return;
            }

            NativeChartNavigator.JumpToChart(musicInfo);
            HiddenDifficultyController.Sync(musicInfo, entry.Difficulty);
            SyncSelectedChart(musicInfo, entry.Difficulty);
        }

        private static void SyncSelectedChart(MusicInfo musicInfo, int difficulty)
        {
            GlobalDataBase.dbMusicTag.selectedDiffTglIndex = difficulty == 4 ? 3 : difficulty;
            GlobalDataBase.dbMusicTag.pnlSelectMusicUid = musicInfo.uid;
            GlobalDataBase.dbMusicTag.m_CurSelectedMusicInfo = musicInfo;
        }
    }
}
