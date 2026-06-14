using HarmonyLib;
using Il2Cpp;
using MDEN.UI.Core;

namespace MDEN.Patches
{
    internal static class PnlPreparationPatch
    {
        [HarmonyPatch(typeof(PnlPreparation), nameof(PnlPreparation.OnEnable))]
        internal static class PnlPreparationOnEnablePatch
        {
            private static void Postfix()
            {
                PreparationStartController.BindOrRefresh();
            }
        }

        [HarmonyPatch(typeof(PnlPreparation), nameof(PnlPreparation.OnDiffTglChanged))]
        internal static class PnlPreparationDiffChangedPatch
        {
            private static void Postfix()
            {
                PreparationStartController.BindOrRefresh();
            }
        }

        [HarmonyPatch(typeof(PnlPreparation), nameof(PnlPreparation.OnBattleStart))]
        internal static class PnlPreparationBattleStartPatch
        {
            private static bool Prefix()
            {
                return !PreparationStartController.ShouldBlockNativeBattleStart();
            }
        }
    }
}
