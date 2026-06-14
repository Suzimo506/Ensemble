using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Il2Cpp;
using Il2CppAssets.Scripts.UI.Panels;
using MDEN.Managers;

namespace MDEN.Patches
{
    internal static class BattlePatch
    {
        [HarmonyPatch(typeof(PnlBattle), nameof(PnlBattle.GameStart))]
        [HarmonyPriority(Priority.First)]
        internal static class BattleStartPatch
        {
            private static void Postfix()
            {
                if (!LobbyManager.IsInLobby) return;

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
                if (!LobbyManager.IsInLobby) return;

                _ = BattleManager.ReportBattleFinishedAsync(true);
            }
        }

        [HarmonyPatch(typeof(PnlBattle), nameof(PnlBattle.OnFail))]
        internal static class BattleFailPatch
        {
            private static void Postfix()
            {
                if (!LobbyManager.IsInLobby) return;

                _ = BattleManager.ReportBattleFinishedAsync(false);
            }
        }
    }
}
