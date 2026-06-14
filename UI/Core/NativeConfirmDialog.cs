using System;
using LocalizeLib;
using PopupLib.UI.Windows;
using PopupLib.UI.Windows.Abstract;

namespace MDEN.UI.Core
{
    public static class NativeConfirmDialog
    {
        public static void Show(string title, string message, Action<bool> onCompleted)
        {
            var prompt = new PromptWindow(new LocalString(message), new LocalString(title));
            prompt.AutoReset = true;

            void Completion(BaseWindow _)
            {
                prompt.OnCompletion -= Completion;
                onCompleted?.Invoke(prompt.Result == true);
            }

            prompt.OnCompletion += Completion;
            prompt.Show();
        }
    }
}
