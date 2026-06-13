using System;

namespace MDEN.UI.Core
{
    // 全局锁解锁令牌，保证异常时自动释放UI锁
    public class UIToken : IDisposable
    {
        private readonly Action _onUnlock;
        private bool _isDisposed;

        public UIToken(Action onUnlock)
        {
            _onUnlock = onUnlock;
        }

        public void Dispose()
        {
            if (_isDisposed) return;
            _isDisposed = true;
            _onUnlock?.Invoke();
        }
    }
}
