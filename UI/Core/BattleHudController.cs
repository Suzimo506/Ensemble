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
            Display.Create();
            Display.Refresh(BattleManager.GetBattleDataSnapshot());
        }

        public static void Destroy()
        {
            _battleActive = false;
            Display.Destroy();
        }

        private static void HandleBattleDataChanged(BattlePlayerEntry[] players)
        {
            if (!_battleActive || !LobbyManager.IsInLobby)
            {
                Display.Destroy();
                return;
            }

            Display.Refresh(players);
        }
    }
}
