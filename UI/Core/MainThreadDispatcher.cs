using System;
using System.Collections.Concurrent;
using MelonLoader;

namespace MDEN.UI.Core
{
    public static class MainThreadDispatcher
    {
        private static readonly ConcurrentQueue<Action> _executionQueue = new ConcurrentQueue<Action>();

        public static void Enqueue(Action action)
        {
            if (action != null)
                _executionQueue.Enqueue(action);
        }

        internal static void ProcessQueue()
        {
            var count = _executionQueue.Count;
            for (var i = 0; i < count && _executionQueue.TryDequeue(out var action); i++)
            {
                try
                {
                    action();
                }
                catch (Exception ex)
                {
                    MelonLogger.Error($"[MainThreadDispatcher] Error executing action: {ex}");
                }
            }
        }
    }
}
