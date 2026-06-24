using System.Linq;
using MDEN.Managers;
using MDEN.Protocol.Models;
using MDEN.UI.Displays;
using UnityEngine;

namespace MDEN.UI.Core
{
    public static class BattleHudController
    {
        private const int MinRefreshIntervalFrames = 2;
        private static readonly BattleLobbyDisplay Display = new BattleLobbyDisplay();
        private static bool _initialized;
        private static bool _battleActive;
        private static BattlePlayerEntry[] _pendingPlayers;
        private static bool _refreshQueued;
        private static int _lastRefreshFrame = -MinRefreshIntervalFrames;
        private static string _lastSnapshotKey;

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
            ResetRefreshState();
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
            ResetRefreshState();
            BattleChartOwnerDisplay.Destroy();
            Display.Destroy();
        }

        private static void HandleBattleDataChanged(BattlePlayerEntry[] players)
        {
            if (!ShouldRefresh(players)) return;

            _pendingPlayers = players ?? System.Array.Empty<BattlePlayerEntry>();
            if (_refreshQueued) return;

            _refreshQueued = true;
            MainThreadDispatcher.Enqueue(RunQueuedRefresh);
        }

        private static void RunQueuedRefresh()
        {
            if (!_battleActive || !LobbyManager.IsInLobby)
            {
                if (Display.IsCreated)
                {
                    Display.Destroy();
                }

                ResetRefreshState();
                return;
            }

            if (Time.frameCount - _lastRefreshFrame < MinRefreshIntervalFrames)
            {
                MainThreadDispatcher.Enqueue(RunQueuedRefresh);
                return;
            }

            var players = _pendingPlayers ?? System.Array.Empty<BattlePlayerEntry>();
            _pendingPlayers = null;
            _refreshQueued = false;
            _lastRefreshFrame = Time.frameCount;

            if (!BattleResultFlowManager.IsBattleResultFlowPending)
            {
                BattleResultBannerDisplay.RefreshIfVisible(players);
            }

            using (PerfTrace.Measure("MDEN.BattleHud.Refresh"))
            {
                Display.Refresh(players);
            }
        }

        private static bool ShouldRefresh(BattlePlayerEntry[] players)
        {
            var key = BuildSnapshotKey(players);
            if (key == _lastSnapshotKey) return false;

            _lastSnapshotKey = key;
            return true;
        }

        private static string BuildSnapshotKey(BattlePlayerEntry[] players)
        {
            if (players == null || players.Length == 0) return string.Empty;

            var ordered = players
                .Where(player => player != null)
                .OrderBy(player => player.Uid)
                .Select(player => $"{player.Uid}:{player.Difficulty}:{player.Score}:{player.Accuracy:0.0000}:{player.Perfects}:{player.Greats}:{player.Earlies}:{player.Lates}:{player.Misses}:{player.FC}:{player.Alive}");
            return string.Join("|", ordered);
        }

        private static void ResetRefreshState()
        {
            _pendingPlayers = null;
            _refreshQueued = false;
            _lastRefreshFrame = -MinRefreshIntervalFrames;
            _lastSnapshotKey = null;
        }
    }
}
