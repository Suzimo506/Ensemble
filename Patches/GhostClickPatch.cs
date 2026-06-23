using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace MDEN.Patches
{
    [HarmonyPatch(typeof(Toggle), "OnPointerClick")]
    internal static class ToggleGhostClickPatch
    {
        private static void Prefix(Toggle __instance, out Toggle.ToggleEvent __state)
        {
            __state = null;
            if (__instance == null || !IsMDENPopupWindowActive()) return;
            if (!IsBulletinListToggle(__instance)) return;

            __state = __instance.onValueChanged;
            __instance.onValueChanged = new Toggle.ToggleEvent();
        }

        private static void Postfix(Toggle __instance, Toggle.ToggleEvent __state)
        {
            if (__instance == null || __state == null) return;

            __instance.onValueChanged = __state;
        }

        private static bool IsMDENPopupWindowActive()
        {
            return HasActiveTitle("UI/Forward/Tips/PnlBulletinNew/ImgBase/MDENTitle") ||
                   HasActiveTitle("UI/Forward/Tips/PnlBulletinNew/ImgBase/ScrollView/MDENTitle");
        }

        private static bool HasActiveTitle(string path)
        {
            var obj = GameObject.Find(path);
            return obj != null && obj.activeInHierarchy;
        }

        private static bool IsBulletinListToggle(Toggle toggle)
        {
            var transform = toggle.transform;
            if (toggle.gameObject.name.Contains("Bulletin")) return true;
            return transform.parent != null && transform.parent.name == "Content";
        }
    }
}
