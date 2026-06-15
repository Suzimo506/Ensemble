using System;
using Il2CppUI.Controls;

namespace MDEN.UI.Core
{
    public static class NativeConfirmDialog
    {
        public static void Show(string title, string message, Action<bool> onCompleted)
        {
            var body = string.IsNullOrWhiteSpace(title) ? message : $"{title}\n{message}";
            CommonMessageBox.ShowConfrimAndCancel(
                body,
                new Action(() => onCompleted?.Invoke(true)),
                new Action(() => onCompleted?.Invoke(false)));
        }
    }
}
