using HarmonyLib;
using Il2Cpp;
using MDEN.UI.Core;

namespace MDEN.Patches
{
    internal static class PreparationPanelPatch
    {
        [HarmonyPatch(typeof(PnlPreparation), nameof(PnlPreparation.OnEnable))]
        internal static class PreparationPanelEnabledPatch
        {
            private static void Postfix()
            {
                PreparationStartController.BindOrRefresh();
            }
        }

        [HarmonyPatch(typeof(PnlPreparation), nameof(PnlPreparation.OnDiffTglChanged))]
        internal static class PreparationDifficultyChangedPatch
        {
            private static void Postfix()
            {
                PreparationStartController.BindOrRefresh();
            }
        }

        [HarmonyPatch(typeof(PnlPreparation), nameof(PnlPreparation.OnBattleStart))]
        internal static class PreparationNativeStartGuardPatch
        {
            private static bool Prefix()
            {
                return !PreparationStartController.ShouldBlockNativeBattleStart();
            }
        }
    }
}
