using System;
using System.Collections.Generic;
using MelonLoader;

namespace MDEN.UI.Core
{
    // 弹窗基类，托管事件生命周期，防止幽灵按键残留
    public abstract class MDENWindowBase : IDisposable
    {
        private readonly List<Action> _eventUnsubscribers = new List<Action>();
        private bool _isDisposed;
        protected bool IsDisposed => _isDisposed;

        // 登记事件解除操作
        protected void RegisterEventCleanup(Action unsubscribeAction)
        {
            if (unsubscribeAction != null)
            {
                _eventUnsubscribers.Add(unsubscribeAction);
            }
        }

        // 向 PopupLib 提交生成 Window
        public abstract void Show();

        // 向 PopupLib 发送关闭指令
        public abstract void Close();

        // 统一销毁流程，自动解绑所有事件
        public void Dispose()
        {
            if (_isDisposed) return;
            _isDisposed = true;

            foreach (var unsubscribe in _eventUnsubscribers)
            {
                try
                {
                    unsubscribe();
                }
                catch (Exception ex)
                {
                    MelonLogger.Error($"Exception during unbinding events: {ex}");
                }
            }
            _eventUnsubscribers.Clear();
            
            OnDispose();
        }

        // 处理自定义内存清理逻辑
        protected virtual void OnDispose() { }
    }
}
