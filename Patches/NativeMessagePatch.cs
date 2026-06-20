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
            return !value || !BattleResultBannerDisplay.ShouldSuppressNativeMessageObject(__instance);
        }
    }

    [HarmonyPatch(typeof(Input), nameof(Input.GetKeyDown), typeof(KeyCode))]
    [HarmonyPriority(Priority.First)]
    internal static class BattleResultInputPatch
    {
        private static bool Prefix(KeyCode key, ref bool __result)
        {
            if (!BattleResultBannerDisplay.ShouldConsumeKeyDown(key)) return true;

            BattleResultBannerDisplay.HandleConsumedKeyDown(key);
            __result = false;
            return false;
        }
    }
}
