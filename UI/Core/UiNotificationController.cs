using System;
using Il2CppAssets.Scripts.UI.Controls;
using MDEN.Managers;
using MDEN.UI.Windows;

namespace MDEN.UI.Core
{
    public static class UiNotificationController
    {
        private static bool _initialized;

        public static void Initialize()
        {
            if (_initialized) return;
            UiNotificationManager.ToastRequested += HandleToastRequested;
            UiNotificationManager.ConfirmRequested += HandleConfirmRequested;
            UiNotificationManager.KickedToLobbyListRequested += HandleKickedToLobbyListRequested;
            _initialized = true;
        }

        public static void Deinitialize()
        {
            if (!_initialized) return;
            UiNotificationManager.ToastRequested -= HandleToastRequested;
            UiNotificationManager.ConfirmRequested -= HandleConfirmRequested;
            UiNotificationManager.KickedToLobbyListRequested -= HandleKickedToLobbyListRequested;
            _initialized = false;
        }

        private static void HandleToastRequested(string message)
        {
            MainThreadDispatcher.Enqueue(() => ShowText.ShowInfo(message));
        }

        private static void HandleConfirmRequested(string title, string message, Action<bool> onCompleted)
        {
            MainThreadDispatcher.Enqueue(() => NativeConfirmDialog.Show(title, message, onCompleted));
        }

        private static void HandleKickedToLobbyListRequested()
        {
            MainThreadDispatcher.Enqueue(() =>
            {
                NavigationButton.RefreshRoomButton();
                RoomHudController.Refresh();
                WindowStackController.OpenWindow(new RoomListWindow());
            });
        }
    }
}
