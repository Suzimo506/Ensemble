using System;
using System.Threading;

namespace MDEN.Managers
{
    public static class UiNotificationManager
    {
        public static event Action<string> ToastRequested;
        public static event Action<string, string, Action<bool>> ConfirmRequested;
        public static event Action KickedToLobbyListRequested;
        public static event Action<int, string> CloudProgressStarted;
        public static event Action<int, bool> CloudProgressFinished;

        private static int _cloudProgressOperationId;

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

        public static int RequestCloudProgress(string message)
        {
            var operationId = Interlocked.Increment(ref _cloudProgressOperationId);
            CloudProgressStarted?.Invoke(operationId, message ?? string.Empty);
            return operationId;
        }

        public static void FinishCloudProgress(int operationId, bool success)
        {
            if (operationId <= 0) return;
            CloudProgressFinished?.Invoke(operationId, success);
        }
    }
}
