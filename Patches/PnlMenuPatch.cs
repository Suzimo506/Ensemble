using HarmonyLib;
using Il2CppAssets.Scripts.UI.Panels;
using MDEN.UI.Core;
using System;

namespace MDEN.Patches
{
    // 拦截游戏主菜单激活时机，注入自定义入口按键
    [HarmonyPatch(typeof(PnlMenu), nameof(PnlMenu.OnEnable))]
    internal static class PnlMenuPatch
    {
        private static void Postfix()
        {
            try
            {
                NavigationButton.Create();
            }
            catch (Exception ex)
            {
                MelonLoader.MelonLogger.Error($"Failed to inject entrance button: {ex}");
            }
        }
    }
}
