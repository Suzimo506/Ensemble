using MDEN.Managers;
using MDEN.Protocol.Messages.Lobby;
using MDEN.Protocol.Rules;
using MDEN.UI.Windows;

namespace MDEN.UI.Core
{
    internal static class TenziDrawController
    {
        private static TenziDrawPopupWindow _window;
        private static int _lastLobbyId;
        private static long _lastDrawSeed;
        private static bool _drawResultRevealed;

        public static void OnLobbyChanged(LobbySyncPush lobby)
        {
            if (lobby == null ||
                !LobbyPlayModeRules.IsTenzi(lobby.PlayMode) ||
                string.IsNullOrWhiteSpace(lobby.TenziSelectedEntry) ||
                lobby.TenziDrawSeed == 0)
            {
                Reset();
                return;
            }

            if (_lastLobbyId == lobby.Id && _lastDrawSeed == lobby.TenziDrawSeed) return;

            Close();
            _lastLobbyId = lobby.Id;
            _lastDrawSeed = lobby.TenziDrawSeed;
            _drawResultRevealed = false;
            _window = new TenziDrawPopupWindow(lobby);
            _window.Show();
        }

        public static bool IsDrawResultHidden(LobbySyncPush lobby)
        {
            return lobby != null &&
                   LobbyPlayModeRules.IsTenzi(lobby.PlayMode) &&
                   !string.IsNullOrWhiteSpace(lobby.TenziSelectedEntry) &&
                   lobby.TenziDrawSeed != 0 &&
                   (_lastLobbyId != lobby.Id ||
                    _lastDrawSeed != lobby.TenziDrawSeed ||
                    !_drawResultRevealed);
        }

        public static void NotifyDrawCompleted(TenziDrawPopupWindow window)
        {
            if (!ReferenceEquals(_window, window)) return;

            _drawResultRevealed = true;
            _window = null;
            RoomHudController.RequestRefresh();
            ChartPreviewController.OnLobbyChanged(LobbyManager.CurrentLobby);
        }

        public static void NotifyWindowClosed(TenziDrawPopupWindow window)
        {
            if (!ReferenceEquals(_window, window)) return;

            ReleaseDrawResult();
            _window = null;
        }

        public static void Close()
        {
            var window = _window;
            if (window == null) return;

            _window = null;
            ReleaseDrawResult();
            window.Close();
            window.Dispose();
        }

        public static void Reset()
        {
            Close();
            _lastLobbyId = 0;
            _lastDrawSeed = 0;
            _drawResultRevealed = false;
        }

        private static void ReleaseDrawResult()
        {
            _drawResultRevealed = true;
            RoomHudController.RequestRefresh();
            ChartPreviewController.OnLobbyChanged(LobbyManager.CurrentLobby);
        }
    }
}
