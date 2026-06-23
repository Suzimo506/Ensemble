using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using MelonLoader;

namespace MDEN.UI.Core
{
    // 拦截开窗请求，保证单一活跃窗口；提供全局防抖锁
    public static class WindowStackController
    {
        private static readonly Stack<MDENWindowBase> _windowStack = new Stack<MDENWindowBase>();
        private static int _lockCount = 0;

        // 获取防抖锁。配合 using 使用，Token被Dispose时解锁
        public static IDisposable LockUI(string message = "Loading...")
        {
            _lockCount++;
            if (_lockCount == 1)
            {
                MDEN.Managers.ClientLogManager.Msg($"Global UI locked: {message}");
            }

            return new UIToken(() =>
            {
                _lockCount--;
                if (_lockCount <= 0)
                {
                    _lockCount = 0;
                    MDEN.Managers.ClientLogManager.Msg("Global UI unlocked.");
                }
            });
        }

        public static void ForceUnlock()
        {
            if (_lockCount <= 0) return;

            _lockCount = 0;
            MDEN.Managers.ClientLogManager.Warning("Global UI lock force released.");
        }

        // 关闭顶部窗口并开新窗，确保栈中只有一个活跃业务窗口
        public static void OpenWindow(
            MDENWindowBase newWindow,
            [CallerMemberName] string callerMemberName = null,
            [CallerFilePath] string callerFilePath = null,
            [CallerLineNumber] int callerLineNumber = 0)
        {
            MDEN.Managers.ClientLogManager.Msg(
                $"OpenWindow: {newWindow?.GetType().Name ?? "null"} from {Path.GetFileName(callerFilePath)}:{callerLineNumber} {callerMemberName}");
            CloseCurrentWindow();
            _windowStack.Push(newWindow);
            newWindow.Show();
        }

        public static void NotifyWindowCompleted(MDENWindowBase window)
        {
            if (window == null) return;

            if (_windowStack.Count > 0 && ReferenceEquals(_windowStack.Peek(), window))
            {
                _windowStack.Pop();
            }

            window.Dispose();
        }

        // 销毁并关闭当前窗口
        public static void CloseCurrentWindow()
        {
            if (_windowStack.Count > 0)
            {
                var top = _windowStack.Pop();
                top.Close();
                top.Dispose();
            }
        }

    }
}
