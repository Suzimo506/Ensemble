using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace MDEN.Patches
{
    /// <summary>
    /// Safety-net patch that suppresses ghost Toggle clicks while a native MDEN
    /// window is open. With native UI blocking input via isStopKeyAction, this
    /// patch is a secondary guard — if a native window is active, ALL Toggle
    /// clicks are suppressed (not just bulletin panel toggles).
    /// </summary>
    [HarmonyPatch(typeof(Toggle), "OnPointerClick")]
    internal static class ToggleGhostClickPatch
    {
        private static void Prefix(Toggle __instance, out Toggle.ToggleEvent __state)
        {
            __state = null;
            if (__instance == null || !IsMDENPopupWindowActive()) return;

            __state = __instance.onValueChanged;
            __instance.onValueChanged = new Toggle.ToggleEvent();
        }

        private static void Postfix(Toggle __instance, Toggle.ToggleEvent __state)
        {
            if (__instance == null || __state == null) return;

            __instance.onValueChanged = __state;
        }

        /// <summary>
        /// Checks whether any native MDEN window root GameObject is active in the scene.
        /// </summary>
        private static bool IsMDENPopupWindowActive()
        {
            var listWindow = GameObject.Find("MDENListWindowRoot");
            if (listWindow != null && listWindow.activeInHierarchy) return true;
            var dialog = GameObject.Find("MDENInputDialogRoot");
            if (dialog != null && dialog.activeInHierarchy) return true;
            var confirm = GameObject.Find("MDENConfirmDialogRoot");
            if (confirm != null && confirm.activeInHierarchy) return true;
            return false;
        }
    }
}
