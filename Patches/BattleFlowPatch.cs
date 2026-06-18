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
        private static bool _canExitBattleResult;
        private static bool _battleResultFlowPending;
        private static BattlePlayerEntry[] _lastBattleResultSnapshot = System.Array.Empty<BattlePlayerEntry>();

        internal static bool IsHoldingBattleResult => !_canExitBattleResult && _lastBattleResultSnapshot.Length > 0;
        internal static bool IsBattleResultFlowPending => LobbyManager.IsInLobby && _battleResultFlowPending;

        public static void SceneLoaded()
        {
            _canExitBattleResult = false;
            ApplyBattleHealthBarVisibility();
            if (!LobbyManager.IsInLobby)
            {
                _canExitBattleResult = true;
                return;
            }

            HidePauseButton();
        }

        [HarmonyPatch(typeof(PnlBattle), nameof(PnlBattle.GameStart))]
        [HarmonyPriority(Priority.First)]
        internal static class BattleStartPatch
        {
            private static void Postfix()
            {
                ApplyBattleHealthBarVisibility();
                if (!LobbyManager.IsInLobby) return;

                _canExitBattleResult = false;
                _battleResultFlowPending = false;
                _lastBattleResultSnapshot = System.Array.Empty<BattlePlayerEntry>();
                HidePauseButton();
                HideFailRestartButton();
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
                    _canExitBattleResult = true;
                    BattleHudController.Destroy();
                    return;
                }

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
                return !LobbyManager.IsInLobby;
            }
        }

        [HarmonyPatch(typeof(BattleHelper), nameof(BattleHelper.GameRestart))]
        [HarmonyPriority(Priority.First)]
        internal static class BattleRestartPatch
        {
            private static bool Prefix()
            {
                return !LobbyManager.IsInLobby;
            }
        }

        [HarmonyPatch(typeof(BattleHelper), nameof(BattleHelper.GameFinish))]
        [HarmonyPriority(Priority.First)]
        internal static class BattleFinishPatch
        {
            private static bool Prefix()
            {
                return !LobbyManager.IsInLobby || _canExitBattleResult;
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

        private static void HidePauseButton()
        {
            var pauseButton = GameObject.Find("UI_2D/Standard/PnlBattle/PnlBattleUI/PnlBattleOthers/Up/BtnPause");
            if (pauseButton != null)
            {
                pauseButton.SetActive(false);
            }
        }

        public static void ApplyBattleHealthBarVisibility()
        {
            var healthBar = GameObject.Find("UI_2D/Standard/PnlBattle/PnlBattleUI/PnlBattleOthers/Below");
            if (healthBar == null)
            {
                healthBar = GameObject.Find("PnlBattleOthers")?.transform.Find("Below")?.gameObject;
            }

            if (healthBar != null)
            {
                healthBar.SetActive(!ModConfigManager.HideBattleHealthBar);
            }
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
            _canExitBattleResult = false;
            _battleResultFlowPending = true;
            BattleResultBannerDisplay.ClearAll();
            MainThreadDispatcher.Enqueue(() => SetVictoryButtons(false));
            await BattleManager.ReportBattleFinishedAsync(alive);
            _lastBattleResultSnapshot = BattleManager.GetBattleDataSnapshot();
            await WaitForLobbyBattleEndAsync();

            if (!LobbyManager.IsInLobby)
            {
                _battleResultFlowPending = false;
                return;
            }

            MainThreadDispatcher.Enqueue(RoomHudController.RebuildRoomCharacters);
            BattleResultBannerDisplay.ClearAll();
            await BattleResultBannerDisplay.ShowAsync(GetBattleResultSnapshot());
            _canExitBattleResult = true;
            _battleResultFlowPending = false;
            MainThreadDispatcher.Enqueue(() => SetVictoryButtons(true));
        }

        private static async Task WaitForLobbyBattleEndAsync()
        {
            while (LobbyManager.IsInLobby && LobbyManager.CurrentLobby?.IsPlaying == true)
            {
                MainThreadDispatcher.Enqueue(() => SetVictoryButtons(false));
                await Task.Delay(500);
            }
        }

        private static void SetVictoryButtons(bool canContinue)
        {
            var pnlVictory = GameObject.FindObjectOfType<PnlVictory>();
            if (pnlVictory == null || pnlVictory.m_CurControls == null) return;

            var btnContinue = pnlVictory.m_CurControls.btnContinue;
            if (btnContinue != null)
            {
                btnContinue.interactable = canContinue;

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
                        BattleResultBannerDisplay.ClearAll();
                        _ = BattleResultBannerDisplay.ShowAsync(GetBattleResultSnapshot());
                    }));
                }
            }
        }

        private static BattlePlayerEntry[] GetBattleResultSnapshot()
        {
            var current = BattleManager.GetBattleDataSnapshot();
            if (current != null && current.Length > 0)
            {
                _lastBattleResultSnapshot = current;
                return current;
            }

            return _lastBattleResultSnapshot ?? System.Array.Empty<BattlePlayerEntry>();
        }
    }
}
