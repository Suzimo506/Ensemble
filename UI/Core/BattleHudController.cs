using MDEN.Managers;
using MDEN.Protocol.Models;
using MDEN.UI.Displays;

namespace MDEN.UI.Core
{
    public static class BattleHudController
    {
        private static readonly BattleLobbyDisplay Display = new BattleLobbyDisplay();
        private static bool _initialized;
        private static bool _battleActive;

        public static void Initialize()
        {
            if (_initialized) return;
            BattleManager.BattleDataChanged += HandleBattleDataChanged;
            _initialized = true;
        }

        public static void Deinitialize()
        {
            if (!_initialized) return;
            BattleManager.BattleDataChanged -= HandleBattleDataChanged;
            Destroy();
            _initialized = false;
        }

        public static void OnBattleStarted()
        {
            if (!LobbyManager.IsInLobby) return;
            _battleActive = true;
            using (PerfTrace.Measure("MDEN.BattleHud.OnBattleStarted"))
            {
                BattleChartOwnerDisplay.Show();
                Display.Create();
                Display.Refresh(BattleManager.GetBattleDataSnapshot());
            }
        }

        public static void Destroy()
        {
            _battleActive = false;
            BattleChartOwnerDisplay.Destroy();
            Display.Destroy();
        }

        private static void HandleBattleDataChanged(BattlePlayerEntry[] players)
        {
            if (_battleActive && !BattleResultFlowManager.IsBattleResultFlowPending)
            {
                BattleResultBannerDisplay.RefreshIfVisible(players);
            }

            if (!_battleActive || !LobbyManager.IsInLobby)
            {
                if (Display.IsCreated)
                {
                    Display.Destroy();
                }

                return;
            }

            using (PerfTrace.Measure("MDEN.BattleHud.Refresh"))
            {
                Display.Refresh(players);
            }
        }
    }
}
