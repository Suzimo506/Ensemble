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
using MDEN.UI.Core;
using UnityEngine;

namespace MDEN.Patches
{
    internal static class BattlePatch
    {
        public static void SceneLoaded()
        {
            if (!LobbyManager.IsInLobby) return;

            HidePauseButton();
        }

        [HarmonyPatch(typeof(PnlBattle), nameof(PnlBattle.GameStart))]
        [HarmonyPriority(Priority.First)]
        internal static class BattleStartPatch
        {
            private static void Postfix()
            {
                if (!LobbyManager.IsInLobby) return;

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
                    BattleHudController.Destroy();
                    return;
                }

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
                return !LobbyManager.IsInLobby || LobbyManager.CurrentLobby?.IsPlaying != true;
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
            BattleResultBannerDisplay.ClearAll();
            await BattleManager.ReportBattleFinishedAsync(alive);
            await WaitForLobbyBattleEndAsync();

            if (!LobbyManager.IsInLobby) return;

            MainThreadDispatcher.Enqueue(RoomHudController.RebuildRoomCharacters);
            BattleResultBannerDisplay.ClearAll();
            await BattleResultBannerDisplay.ShowAsync(BattleManager.GetBattleDataSnapshot());
        }

        private static async Task WaitForLobbyBattleEndAsync()
        {
            while (LobbyManager.IsInLobby && LobbyManager.CurrentLobby?.IsPlaying == true)
            {
                await Task.Delay(500);
            }
        }
    }
}
