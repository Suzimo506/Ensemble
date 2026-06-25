using System.Collections;
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
            MelonCoroutines.Start(RunResultPanelHold(generation, ResultPanelWaitFrames));
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
            if (ShouldDelayPreviewForBattleResult())
            {
                HoldPreviewUntilResultPanelCloses();
                return;
            }

            if (!SettlementOverlayController.IsNativeResultOverlayVisible())
            {
                Preview(entry);
            }

            SchedulePreviewRetry(lobby.Id, entry.Entry, PreviewRetryCount);
        }

        private static IEnumerator RunResultPanelHold(int generation, int framesUntilFallbackRelease)
        {
            var framesRemaining = framesUntilFallbackRelease;
            while (generation == _resultHoldGeneration && _resultPreviewHeld)
            {
                var resultPanelVisible = ShouldDelayPreviewForBattleResult();
                if (resultPanelVisible)
                {
                    _resultPanelSeen = true;
                }

                if (_resultPanelSeen)
                {
                    if (!resultPanelVisible)
                    {
                        ReleaseResultHold(generation);
                        yield break;
                    }
                }
                else if (framesRemaining-- <= 0)
                {
                    ReleaseResultHold(generation);
                    yield break;
                }

                yield return new WaitForEndOfFrame();
            }
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
            MelonCoroutines.Start(RunPreviewRetry(generation, lobbyId, entryText, retriesRemaining));
        }

        private static IEnumerator RunPreviewRetry(int generation, int lobbyId, string entryText, int retriesRemaining)
        {
            while (retriesRemaining-- > 0)
            {
                for (var delayFrames = PreviewRetryDelayFrames; delayFrames > 0; delayFrames--)
                {
                    yield return new WaitForEndOfFrame();
                }

                if (generation != _previewGeneration) yield break;
                if (!LobbyManager.IsInLobby || LobbyManager.CurrentLobby?.Id != lobbyId) yield break;
                if (LobbyManager.CurrentLobby.IsPlaying || !LobbyManager.CurrentLobby.Locked) yield break;

                if (ShouldDelayPreviewForBattleResult())
                {
                    HoldPreviewUntilResultPanelCloses();
                    yield break;
                }

                var entry = PlaylistManager.GetCurrentPlaylistEntry();
                if (entry == null || entry.Entry != entryText) yield break;

                Preview(entry);
            }
        }

        private static bool ShouldDelayPreviewForBattleResult()
        {
            return SettlementOverlayController.ShouldDelayChartPreviewForResult();
        }

        internal static void Preview(PlaylistEntryViewModel entry)
        {
            var musicInfo = ChartManager.GetMusicInfo(entry.ChartKey);
            if (musicInfo == null)
            {
                MDEN.Managers.ClientLogManager.Warning($"Cannot preview multiplayer chart: chart {entry.ChartKey} not found locally.");
                return;
            }

            var difficulty = ResolvePreviewDifficulty(entry);
            NativeChartNavigator.JumpToChart(musicInfo);
            SyncSelectedChart(musicInfo, difficulty);
        }

        private static int ResolvePreviewDifficulty(PlaylistEntryViewModel entry)
        {
            if (!PlaylistManager.IsRookieMode() || !PlaylistManager.IsLocalPlayerReady()) return entry.Difficulty;

            var currentBattleDifficulty = LobbyManager.GetCurrentBattleDifficulty(PlayerManager.CurrentUid);
            if (MDEN.Protocol.Rules.DifficultyDisplayRules.IsKnownDifficulty(currentBattleDifficulty))
            {
                return currentBattleDifficulty;
            }

            var readyDifficulty = LobbyManager.GetReadyDifficulty(PlayerManager.CurrentUid);
            return MDEN.Protocol.Rules.DifficultyDisplayRules.IsKnownDifficulty(readyDifficulty)
                ? readyDifficulty
                : entry.Difficulty;
        }

        private static void SyncSelectedChart(MusicInfo musicInfo, int difficulty)
        {
            var nativeDifficulty = HiddenDifficultyController.Sync(musicInfo, difficulty);
            GlobalDataBase.dbMusicTag.selectedDiffTglIndex = nativeDifficulty;
            GlobalDataBase.dbMusicTag.pnlSelectMusicUid = musicInfo.uid;
            GlobalDataBase.dbMusicTag.m_CurSelectedMusicInfo = musicInfo;
        }
    }
}
