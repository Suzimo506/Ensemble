using MDEN.Managers;
using MDEN.Protocol.Messages.Battle;

namespace MDEN.UI.Core
{
    public static class SettlementHudController
    {
        private static bool _initialized;

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
            SettlementResultDialog.Destroy();
            _initialized = false;
        }

        private static void HandleSettlementReceived(SettlementResultPush result)
        {
            SettlementResultDialog.Show(result);
        }
    }
}
