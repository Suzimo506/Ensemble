using HarmonyLib;
using MDEN.UI.Core;
using UnityEngine;

namespace MDEN.Patches
{
    [HarmonyPatch(typeof(GameObject), nameof(GameObject.SetActive))]
    internal static class NativeMessagePatch
    {
        private static bool Prefix(GameObject __instance, bool value)
        {
            return !value || !SettlementOverlayController.ShouldSuppressNativeMessageObject(__instance);
        }
    }
}
