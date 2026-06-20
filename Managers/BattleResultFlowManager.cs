using MDEN.Protocol.Models;

namespace MDEN.Managers
{
    public static class BattleResultFlowManager
    {
        private static bool _canExitBattleResult = true;
        private static bool _battleResultFlowPending;
        private static BattlePlayerEntry[] _lastBattleResultSnapshot = System.Array.Empty<BattlePlayerEntry>();

        public static bool IsHoldingBattleResult => !_canExitBattleResult && _lastBattleResultSnapshot.Length > 0;
        public static bool IsBattleResultFlowPending => LobbyManager.IsInLobby && _battleResultFlowPending;
        public static bool CanExitBattleResult => _canExitBattleResult;

        public static void Reset()
        {
            _canExitBattleResult = true;
            _battleResultFlowPending = false;
            _lastBattleResultSnapshot = System.Array.Empty<BattlePlayerEntry>();
        }

        public static void SetCanExitBattleResult(bool canExit)
        {
            _canExitBattleResult = canExit;
        }

        public static void SetBattleResultFlowPending(bool pending)
        {
            _battleResultFlowPending = pending;
        }

        public static void SetLastBattleResultSnapshot(BattlePlayerEntry[] snapshot)
        {
            _lastBattleResultSnapshot = snapshot ?? System.Array.Empty<BattlePlayerEntry>();
        }

        public static BattlePlayerEntry[] GetLastBattleResultSnapshot()
        {
            return _lastBattleResultSnapshot ?? System.Array.Empty<BattlePlayerEntry>();
        }
    }
}
