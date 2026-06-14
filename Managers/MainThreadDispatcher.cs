using System;
using System.Collections.Concurrent;
using MelonLoader;

namespace MDEN.Managers
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
            while (_executionQueue.TryDequeue(out var action))
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
