using HarmonyLib;
using Il2CppAssets.Scripts.GameCore.HostComponent;
using Il2CppGameLogic;
using MDEN.Managers;

namespace MDEN.Patches
{
    [HarmonyPatch(typeof(BattleEnemyManager), nameof(BattleEnemyManager.SetPlayResult))]
    internal static class BattleEnemyManagerSetPlayResultPatch
    {
        private static void Postfix(int idx, byte result, bool isMulStart = false, bool isMulEnd = false, bool isLeft = false)
        {
            if (!BattleManager.IsActiveMultiplayerBattle) return;

            AccuracyManager.HandleSetPlayResult(idx, result, isMulStart, isMulEnd, isLeft);
        }
    }

    [HarmonyPatch(typeof(GameMissPlay), nameof(GameMissPlay.MissCube))]
    internal static class GameMissPlayMissCubePatch
    {
        private static void Postfix(int idx, decimal currentTick)
        {
            if (!BattleManager.IsActiveMultiplayerBattle) return;

            AccuracyManager.HandleMissCube(idx, currentTick);
        }
    }
}
