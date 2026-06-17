using MDEN.Managers;
using MDEN.Patches;
using MDEN.Protocol.Messages.Battle;

namespace MDEN.UI.Core
{
    public static class SettlementHudController
    {
        private const int ResultFlowWaitFrames = 600;
        private static bool _initialized;
        private static int _pendingGeneration;

        public static void Initialize()
        {
            if (_initialized) return;
            SettlementManager.SettlementReceived += HandleSettlementReceived;
            _initialized = true;
        }

        public static void Deinitialize()
        {
            if (!_initialized) return;
            SettlementManager.SettlementReceived -= HandleSettlementReceived;
            _pendingGeneration++;
            SettlementResultDialog.Destroy();
            _initialized = false;
        }

        private static void HandleSettlementReceived(SettlementResultPush result)
        {
            if (result == null) return;

            var generation = ++_pendingGeneration;
            ShowAfterBattleResultFlow(result, generation, ResultFlowWaitFrames);
        }

        private static void ShowAfterBattleResultFlow(SettlementResultPush result, int generation, int framesRemaining)
        {
            if (!_initialized || generation != _pendingGeneration) return;

            if (BattlePatch.IsBattleResultFlowPending && framesRemaining > 0)
            {
                MainThreadDispatcher.Enqueue(() => ShowAfterBattleResultFlow(result, generation, framesRemaining - 1));
                return;
            }

            SettlementResultDialog.Show(result);
        }
    }
}
