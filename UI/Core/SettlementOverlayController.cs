using System;
using Il2CppAssets.Scripts.UI.Panels;
using MDEN.Managers;
using MDEN.Protocol.Messages.Battle;
using MDEN.Protocol.Models;
using UnityEngine;

namespace MDEN.UI.Core
{
    internal static class SettlementOverlayController
    {
        private static bool _rankKeyWasDown;

        public static bool IsBattleResultActive => BattleResultBannerDisplay.IsActive;
        public static bool IsBattleResultVisible => BattleResultBannerDisplay.IsVisible;
        public static bool IsBattleResultShowing => BattleResultBannerDisplay.ShowingResults;
        public static bool IsFinalSettlementVisible => SettlementResultDialog.IsVisible;
        public static bool IsAnyMdenOverlayActive => IsBattleResultActive || IsFinalSettlementVisible;

        public static void Reset()
        {
            _rankKeyWasDown = false;
            ClearAll();
        }

        public static void ResetShortcutState()
        {
            _rankKeyWasDown = false;
        }

        public static void Update()
        {
            BattleResultBannerDisplay.Update();
            SettlementResultDialog.Update();
        }

        public static void ClearAll()
        {
            _rankKeyWasDown = false;
            BattleResultBannerDisplay.ClearAll();
            SettlementResultDialog.Destroy();
            NativeInputBlocker.ClearAndForceUnblockIfIdle("BattleResult");
        }

        public static void CloseBattleResult()
        {
            BattleResultBannerDisplay.ClearAll();
            NativeInputBlocker.ClearAndForceUnblockIfIdle("BattleResult");
        }

        public static void ShowBattleResult(BattlePlayerEntry[] players)
        {
            if (IsFinalSettlementVisible) return;

            BattleResultBannerDisplay.ShowOrRefresh(players);
        }

        public static void RefreshBattleResultIfVisible(BattlePlayerEntry[] players)
        {
            BattleResultBannerDisplay.RefreshIfVisible(players);
        }

        public static void AllowNativeMessagesBriefly()
        {
            BattleResultBannerDisplay.AllowNativeMessagesBriefly();
        }

        public static void ShowFinalSettlement(SettlementResultPush result)
        {
            CloseBattleResult();
            NativeInputBlocker.ClearAndForceUnblockIfIdle("BattleResult");
            SettlementResultDialog.Show(result);
        }

        public static void UpdateBattleResultShortcut(
            Func<BattlePlayerEntry[]> getLastSnapshot,
            Func<BattlePlayerEntry[]> getResultSnapshot,
            Action showPendingHint)
        {
            var rankKeyPressed = ConsumeRankKeyDown();
            if (!LobbyManager.IsInLobby) return;
            if (IsAnyMdenOverlayActive) return;

            var lastSnapshot = getLastSnapshot?.Invoke() ?? Array.Empty<BattlePlayerEntry>();
            if (lastSnapshot.Length == 0) return;
            if (!rankKeyPressed) return;

            if (BattleResultFlowManager.IsBattleResultFlowPending)
            {
                showPendingHint?.Invoke();
                return;
            }

            var snapshot = getResultSnapshot?.Invoke() ?? lastSnapshot;
            ShowBattleResult(snapshot);
        }

        public static bool ShouldBlockNativeRestartShortcut()
        {
            if (!LobbyManager.IsInLobby) return false;

            return IsAnyMdenOverlayActive || IsNativeResultOverlayVisible();
        }

        public static bool ShouldDelayChartPreviewForResult()
        {
            return BattleResultFlowManager.IsBattleResultFlowPending ||
                   IsAnyMdenOverlayActive ||
                   IsNativeResultOverlayVisible();
        }

        public static bool IsNativeBattleResultPanelVisible()
        {
            return IsVisible("UI_2D/Standard/PnlVictory") ||
                   IsVisible("UI_2D/Standard/PnlFail");
        }

        public static bool IsNativeResultOverlayVisible()
        {
            return IsNativeBattleResultPanelVisible() ||
                   IsVisible("UI_2D/Standard/PnlRank") ||
                   IsActiveRankPanelVisible();
        }

        public static bool IsNativeVictoryPanelVisible()
        {
            return IsVisible("UI_2D/Standard/PnlVictory");
        }

        public static bool IsNativeFailPanelVisible()
        {
            return IsVisible("UI_2D/Standard/PnlFail");
        }

        public static bool ShouldSuppressNativeMessageObject(GameObject obj)
        {
            return BattleResultBannerDisplay.ShouldSuppressNativeMessageObject(obj);
        }

        private static bool ConsumeRankKeyDown()
        {
            var rankKeyDown = Input.GetKey(KeyCode.R);
            if (!rankKeyDown)
            {
                _rankKeyWasDown = false;
                return false;
            }

            if (_rankKeyWasDown) return false;

            _rankKeyWasDown = true;
            return true;
        }

        private static bool IsVisible(string path)
        {
            var obj = GameObject.Find(path);
            return obj != null && obj.activeInHierarchy;
        }

        private static bool IsActiveRankPanelVisible()
        {
            try
            {
                var panel = GameObject.FindObjectOfType<PnlRank>();
                return panel != null && panel.gameObject != null && panel.gameObject.activeInHierarchy;
            }
            catch
            {
                return false;
            }
        }
    }
}
