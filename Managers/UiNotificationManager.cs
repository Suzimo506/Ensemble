using System;

namespace MDEN.Managers
{
    public static class UiNotificationManager
    {
        public static event Action<string> ToastRequested;
        public static event Action<string, string, Action<bool>> ConfirmRequested;
        public static event Action KickedToLobbyListRequested;

        public static void RequestToast(string message)
        {
            if (string.IsNullOrWhiteSpace(message)) return;
            ToastRequested?.Invoke(message);
        }

        public static void RequestConfirm(string title, string message, Action<bool> onCompleted)
        {
            ConfirmRequested?.Invoke(title, message, onCompleted);
        }

        public static void RequestKickedToLobbyList()
        {
            KickedToLobbyListRequested?.Invoke();
        }
    }
}
