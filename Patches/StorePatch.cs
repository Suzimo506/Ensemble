using HarmonyLib;
using Il2CppAssets.Scripts.UI.Controls;
using Il2CppAssets.Scripts.UI.Panels.PnlDLC;
using MDEN.Managers;
using MDEN.UI.Core;
using UnityEngine;

namespace MDEN.Patches
{
    internal static class StorePatch
    {
        private const string StoreDisabledMessage = "多人房间中不能打开商店！";

        [HarmonyPatch(typeof(OpenDlc), nameof(OpenDlc.OnButtonClicked))]
        internal static class OpenDlcPatch
        {
            private static bool Prefix()
            {
                if (!LobbyManager.IsInLobby) return true;

                ShowText.ShowInfo(StoreDisabledMessage);
                return false;
            }
        }

        [HarmonyPatch(typeof(Input), nameof(Input.GetKeyDown), typeof(KeyCode))]
        internal static class InputGetKeyDownPatch
        {
            private static bool Prefix(KeyCode key, ref bool __result)
            {
                if (key != KeyCode.E || !LobbyManager.IsInLobby || !RoomSceneOverlay.IsHomeVisible) return true;
                if (RoomHudController.IsChatConsumingInput) return true;

                __result = false;
                ShowText.ShowInfo(StoreDisabledMessage);
                return false;
            }
        }
    }
}
