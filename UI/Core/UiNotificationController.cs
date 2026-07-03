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
            UiNotificationManager.CloudProgressStarted += HandleCloudProgressStarted;
            UiNotificationManager.CloudProgressFinished += HandleCloudProgressFinished;
            UiNotificationManager.CloudNoticeRequested += HandleCloudNoticeRequested;
            _initialized = true;
        }

        public static void Deinitialize()
        {
            if (!_initialized) return;
            UiNotificationManager.ToastRequested -= HandleToastRequested;
            UiNotificationManager.ConfirmRequested -= HandleConfirmRequested;
            UiNotificationManager.KickedToLobbyListRequested -= HandleKickedToLobbyListRequested;
            UiNotificationManager.CloudProgressStarted -= HandleCloudProgressStarted;
            UiNotificationManager.CloudProgressFinished -= HandleCloudProgressFinished;
            UiNotificationManager.CloudNoticeRequested -= HandleCloudNoticeRequested;
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

        private static void HandleCloudProgressStarted(int operationId, string message)
        {
            CloudSyncIndicator.Start(operationId, message);
        }

        private static void HandleCloudProgressFinished(int operationId, bool success)
        {
            CloudSyncIndicator.Finish(operationId, success);
        }

        private static void HandleCloudNoticeRequested(int operationId, string message)
        {
            CloudSyncIndicator.ShowNotice(operationId, message);
        }
    }
}
