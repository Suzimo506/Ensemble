using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using HarmonyLib;
using Il2Cpp;
using Il2CppArcadeController.UI.Panel.PnlHome;
using Il2CppAssets.Scripts.Database;
using Il2CppAssets.Scripts.PeroTools.Commons;
using Il2CppAssets.Scripts.PeroTools.Managers;
using Il2CppAssets.Scripts.UI.Panels;
using MDEN.Managers;
using MDEN.Protocol.Models;
using MDEN.UI.Core;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace MDEN.Patches
{
    internal static class BattleFlowPatch
    {
        private const int BattleEndPollIntervalMs = 500;
        private const int VictoryBattleEndWaitTimeoutMs = 180000;
        private const int FailedBattleEndWaitTimeoutMs = 3720000;
        private const int PauseButtonHideIntervalFrames = 30;
        private const int PauseButtonLookupRetryFrames = 120;
        private static readonly System.TimeSpan WaitingHintCooldown = System.TimeSpan.FromSeconds(2.5);
        private static int _nextPauseButtonHideFrame;
        private static GameObject _pauseButton;
        private static int _nextVictoryPanelLookupFrame;
        private static PnlVictory _pnlVictory;
        private static System.DateTime _lastWaitingHintUtc;

        private static bool IsMultiplayerBattleContext => BattleManager.IsActiveMultiplayerBattle;

        internal static void MarkMultiplayerBattleStarting()
        {
            BattleManager.MarkMultiplayerBattleStarting();
            _nextPauseButtonHideFrame = 0;
            _nextVictoryPanelLookupFrame = 0;
            _pauseButton = null;
            _pnlVictory = null;
            _lastWaitingHintUtc = default;
        }

        internal static void ResetBattleSceneState()
        {
            BattleManager.MarkMultiplayerBattleEnded();
            BattleResultFlowManager.Reset();
            _nextPauseButtonHideFrame = 0;
            _nextVictoryPanelLookupFrame = 0;
            _pauseButton = null;
            _pnlVictory = null;
            _lastWaitingHintUtc = default;
        }

        internal static void UpdateBattleUiState()
        {
            TryShowBattleResultByKeyboard();
            if (!IsMultiplayerBattleContext) return;
            if (Time.frameCount < _nextPauseButtonHideFrame) return;

            var foundPauseButton = HidePauseButton();
            _nextPauseButtonHideFrame = Time.frameCount + (foundPauseButton
                ? PauseButtonHideIntervalFrames
                : PauseButtonLookupRetryFrames);
        }

        public static void SceneLoaded()
        {
            _pauseButton = null;
            _pnlVictory = null;
            _nextVictoryPanelLookupFrame = 0;
            BattleResultFlowManager.SetCanExitBattleResult(false);
            if (!IsMultiplayerBattleContext)
            {
                BattleResultFlowManager.SetCanExitBattleResult(true);
                ApplyBattleHealthBarVisibility();
                return;
            }

            BattleManager.MarkMultiplayerBattleStarting();
            ApplyBattleHealthBarVisibility();
            HideBattleControls();
        }

        [HarmonyPatch(typeof(PnlBattle), nameof(PnlBattle.GameStart))]
        [HarmonyPriority(Priority.First)]
        internal static class BattleStartPatch
        {
            private static void Postfix()
            {
                if (!IsMultiplayerBattleContext)
                {
                    ApplyBattleHealthBarVisibility();
                    return;
                }

                BattleManager.MarkMultiplayerBattleStarting();
                BattleResultFlowManager.SetCanExitBattleResult(false);
                BattleResultFlowManager.SetBattleResultFlowPending(false);
                BattleResultFlowManager.SetLastBattleResultSnapshot(System.Array.Empty<BattlePlayerEntry>());
                ApplyBattleHealthBarVisibility();
                HideBattleControls();
                BattleManager.PrepareForNewBattle();
                BattleHudController.OnBattleStarted();
                _ = BattleManager.SyncStartAsync();
            }
        }

        [HarmonyPatch]
        internal static class BattleVictoryPatch
        {
            private static IEnumerable<MethodBase> TargetMethods()
            {
                return typeof(PnlVictory).GetMethods().Where(method => method.Name == nameof(PnlVictory.OnVictory));
            }

            private static void Postfix()
            {
                if (!LobbyManager.IsInLobby)
                {
                    BattleResultFlowManager.SetCanExitBattleResult(true);
                    BattleHudController.Destroy();
                    return;
                }

                if (BattleResultFlowManager.IsBattleResultFlowPending) return;

                SetVictoryButtons(false);
                ChartPreviewController.HoldPreviewUntilResultPanelCloses();
                _ = FinishBattleAndShowResultsAsync(true);
                BattleHudController.Destroy();
            }
        }

        [HarmonyPatch(typeof(PnlBattle), nameof(PnlBattle.OnFail))]
        internal static class BattleFailPatch
        {
            private static void Postfix()
            {
                if (!LobbyManager.IsInLobby) return;
                if (BattleResultFlowManager.IsBattleResultFlowPending) return;

                SetVictoryButtons(false);
                ChartPreviewController.HoldPreviewUntilResultPanelCloses();
                _ = FinishBattleAndShowResultsAsync(false);
                BattleHudController.Destroy();
            }
        }

        [HarmonyPatch]
        [HarmonyPriority(Priority.First)]
        internal static class BattlePausePatch
        {
            private static readonly MethodBase[] Methods =
            {
                typeof(PnlBattle).GetMethod(nameof(PnlBattle.Pause)),
                typeof(UnityGameManager).GetMethod(nameof(UnityGameManager.OnApplicationFocus))
            };

            private static IEnumerable<MethodBase> TargetMethods() => Methods;

            private static bool Prefix()
            {
                if (!IsMultiplayerBattleContext) return true;

                HidePauseButton();
                return false;
            }
        }

        [HarmonyPatch(typeof(Input), nameof(Input.GetKeyDown), typeof(KeyCode))]
        [HarmonyPriority(Priority.First)]
        internal static class BattleEscapeKeyPatch
        {
            private static bool Prefix(KeyCode key, ref bool __result)
            {
                if (key != KeyCode.Escape || !IsMultiplayerBattleContext) return true;

                __result = false;
                return false;
            }
        }

        [HarmonyPatch(typeof(BattleHelper), nameof(BattleHelper.GameRestart))]
        [HarmonyPriority(Priority.First)]
        internal static class BattleRestartPatch
        {
            private static bool Prefix()
            {
                return !IsMultiplayerBattleContext;
            }
        }

        [HarmonyPatch(typeof(BattleHelper), nameof(BattleHelper.GameFinish))]
        [HarmonyPriority(Priority.First)]
        internal static class BattleFinishPatch
        {
            private static bool Prefix()
            {
                if (!IsMultiplayerBattleContext || BattleResultFlowManager.CanExitBattleResult)
                {
                    return true;
                }

                ShowWaitingForOthersHint();
                return false;
            }

            private static void Postfix()
            {
                if (!LobbyManager.IsInLobby)
                {
                    BattleManager.Reset();
                    BattleHudController.Destroy();
                }
            }
        }

        private static void TryShowBattleResultByKeyboard()
        {
            if (!LobbyManager.IsInLobby) return;
            if (BattleResultBannerDisplay.IsConsumingKeyboard) return;
            if (BattleResultFlowManager.GetLastBattleResultSnapshot().Length == 0) return;
            if (!Input.GetKeyDown(KeyCode.R)) return;

            if (BattleResultFlowManager.IsBattleResultFlowPending)
            {
                ShowWaitingForOthersHint();
                return;
            }

            BattleResultBannerDisplay.ShowOrRefresh(GetBattleResultSnapshot());
        }

        private static bool HidePauseButton()
        {
            if (_pauseButton == null)
            {
                _pauseButton = GameObject.Find("UI_2D/Standard/PnlBattle/PnlBattleUI/PnlBattleOthers/Up/BtnPause");
            }

            if (_pauseButton != null)
            {
                if (_pauseButton.activeSelf)
                {
                    _pauseButton.SetActive(false);
                }

                return true;
            }

            return false;
        }

        private static void HideBattleControls()
        {
            HidePauseButton();
            HideFailRestartButton();
            _nextPauseButtonHideFrame = 0;
        }

        public static void ApplyBattleHealthBarVisibility()
        {
            BattleHealthBarController.ApplyVisibility();
        }

        private static void HideFailRestartButton()
        {
            var failRestartButton = GameObject.Find("UI_2D/Standard/PnlFail/ImgBgDown/BtnRestart");
            if (failRestartButton != null)
            {
                failRestartButton.SetActive(false);
            }

            var returnButton = GameObject.Find("UI_2D/Standard/PnlFail/ImgBgDown/BtnReturn");
            if (returnButton != null && failRestartButton != null)
            {
                returnButton.transform.localPosition = failRestartButton.transform.localPosition;
            }
        }

        private static async Task FinishBattleAndShowResultsAsync(bool alive)
        {
            BattleResultFlowManager.SetCanExitBattleResult(false);
            BattleResultFlowManager.SetBattleResultFlowPending(true);
            BattleResultBannerDisplay.ClearAll();
            BattleManager.MarkLocalBattleFinished(alive);
            MainThreadDispatcher.Enqueue(() => SetVictoryButtons(false));
            RememberBattleResultSnapshot(BattleManager.GetBattleDataSnapshot());
            var finishingBattleId = LobbyManager.CurrentLobby?.CurrentBattleId;

            try
            {
                await BattleManager.ReportBattleFinishedAsync(alive);
                RememberBattleResultSnapshot(BattleManager.GetBattleDataSnapshot());
                if (alive)
                {
                    BattleResultBannerDisplay.ShowOrRefresh(GetBattleResultSnapshot());
                }
                else
                {
                    ShowWaitingForOthersHint();
                }

                await WaitForLobbyBattleEndAsync(alive, finishingBattleId);
                RememberBattleResultSnapshot(BattleManager.GetBattleDataSnapshot());
                BattleResultBannerDisplay.ShowOrRefresh(GetBattleResultSnapshot());

                if (LobbyManager.IsInLobby)
                {
                    MainThreadDispatcher.Enqueue(RoomHudController.RebuildRoomCharacters);
                }
            }
            catch (System.Exception ex)
            {
                MDEN.Managers.ClientLogManager.Warning($"Finish battle result flow failed: {ex.Message}");
            }
            finally
            {
                BattleResultFlowManager.SetCanExitBattleResult(true);
                BattleResultFlowManager.SetBattleResultFlowPending(false);
                MainThreadDispatcher.Enqueue(() => SetVictoryButtons(true));
            }
        }

        private static async Task WaitForLobbyBattleEndAsync(bool alive, string battleId)
        {
            var timeoutMs = alive ? VictoryBattleEndWaitTimeoutMs : FailedBattleEndWaitTimeoutMs;
            var waitedMs = 0;
            while (LobbyManager.IsInLobby && LobbyManager.CurrentLobby?.IsPlaying == true)
            {
                var currentBattleId = LobbyManager.CurrentLobby?.CurrentBattleId;
                if (!string.IsNullOrWhiteSpace(battleId) &&
                    !string.IsNullOrWhiteSpace(currentBattleId) &&
                    currentBattleId != battleId)
                {
                    MDEN.Managers.ClientLogManager.Warning($"Lobby battle id changed while waiting for result. old={battleId}, current={currentBattleId}, alive={alive}");
                    return;
                }

                if (waitedMs >= timeoutMs)
                {
                    MDEN.Managers.ClientLogManager.Warning($"Timed out waiting for lobby battle end. waitedMs={waitedMs}, alive={alive}");
                    return;
                }

                MainThreadDispatcher.Enqueue(() => SetVictoryButtons(false));
                BattleResultBannerDisplay.SuppressNativeMessages();
                await BattleManager.ReportBattleFinishedAsync(alive);
                await Task.Delay(BattleEndPollIntervalMs);
                waitedMs += BattleEndPollIntervalMs;
            }
        }

        private static void RememberBattleResultSnapshot(BattlePlayerEntry[] snapshot)
        {
            if (snapshot == null || snapshot.Length == 0) return;
            BattleResultFlowManager.SetLastBattleResultSnapshot(snapshot);
        }

        private static void ShowWaitingForOthersHint()
        {
            var now = System.DateTime.UtcNow;
            if (_lastWaitingHintUtc != default && now - _lastWaitingHintUtc < WaitingHintCooldown)
            {
                return;
            }

            _lastWaitingHintUtc = now;
            MainThreadDispatcher.Enqueue(() =>
            {
                BattleResultBannerDisplay.AllowNativeMessagesBriefly();
                Il2CppAssets.Scripts.UI.Controls.ShowText.ShowInfo("等待其他人完成游戏中...");
            });
        }

        private static void SetVictoryButtons(bool canContinue)
        {
            var pnlVictory = FindVictoryPanel();
            if (pnlVictory == null || pnlVictory.m_CurControls == null) return;

            var btnContinue = pnlVictory.m_CurControls.btnContinue;
            if (btnContinue != null)
            {
                btnContinue.interactable = true;

                var txtContinue = btnContinue.transform.Find("TxtContinue")?.GetComponent<Text>();
                if (txtContinue != null)
                {
                    txtContinue.text = canContinue ? "继续" : "等待中";
                }

                var imgBtnA = btnContinue.transform.Find("TxtContinue/ImgBtnA");
                if (imgBtnA != null) imgBtnA.gameObject.SetActive(canContinue);
            }

            var btnReset = pnlVictory.m_CurControls.btnReset;
            if (btnReset != null)
            {
                btnReset.gameObject.SetActive(canContinue);
                if (canContinue)
                {
                    var txtRestart = btnReset.transform.Find("TxtRestart")?.GetComponent<Text>();
                    if (txtRestart != null) txtRestart.text = "排行榜";

                    btnReset.onClick = new Button.ButtonClickedEvent();
                    btnReset.onClick.AddListener((UnityAction)(() =>
                    {
                        BattleResultBannerDisplay.ShowOrRefresh(GetBattleResultSnapshot());
                    }));
                }
            }
        }

        private static PnlVictory FindVictoryPanel()
        {
            if (_pnlVictory != null && _pnlVictory.gameObject != null) return _pnlVictory;
            if (Time.frameCount < _nextVictoryPanelLookupFrame) return null;

            _nextVictoryPanelLookupFrame = Time.frameCount + 30;
            using (PerfTrace.Measure("MDEN.BattleFlow.FindVictoryPanel"))
            {
                var obj = GameObject.Find("UI_2D/Standard/PnlVictory");
                if (obj != null)
                {
                    _pnlVictory = obj.GetComponent<PnlVictory>();
                    if (_pnlVictory != null) return _pnlVictory;
                }

                _pnlVictory = GameObject.FindObjectOfType<PnlVictory>();
                return _pnlVictory;
            }
        }

        private static BattlePlayerEntry[] GetBattleResultSnapshot()
        {
            if (BattleResultFlowManager.IsBattleResultFlowPending ||
                BattleResultBannerDisplay.IsVisible ||
                BattleResultBannerDisplay.ShowingResults)
            {
                var lastSnapshot = BattleResultFlowManager.GetLastBattleResultSnapshot();
                if (lastSnapshot.Length > 0) return lastSnapshot;
            }

            var current = BattleManager.GetBattleDataSnapshot();
            if (current != null && current.Length > 0)
            {
                RememberBattleResultSnapshot(current);
                return current;
            }

            return BattleResultFlowManager.GetLastBattleResultSnapshot();
        }
    }
}
