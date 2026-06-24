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

            _lastLobbyId = lobby.Id;
            _lastDrawSeed = lobby.TenziDrawSeed;
            Close();
            _window = new TenziDrawPopupWindow(lobby);
            _window.Show();
        }

        public static void NotifyWindowClosed(TenziDrawPopupWindow window)
        {
            if (!ReferenceEquals(_window, window)) return;

            _window = null;
        }

        public static void Close()
        {
            var window = _window;
            if (window == null) return;

            _window = null;
            window.Close();
            window.Dispose();
        }

        public static void Reset()
        {
            Close();
            _lastLobbyId = 0;
            _lastDrawSeed = 0;
        }
    }
}
