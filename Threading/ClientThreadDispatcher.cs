using System;

namespace MDEN.Threading
{
    internal static class ClientThreadDispatcher
    {
        private static Action<Action> _enqueue = action => action?.Invoke();

        public static void Configure(Action<Action> enqueue)
        {
            _enqueue = enqueue ?? (action => action?.Invoke());
        }

        public static void Enqueue(Action action)
        {
            if (action == null) return;

            _enqueue(action);
        }
    }
}
