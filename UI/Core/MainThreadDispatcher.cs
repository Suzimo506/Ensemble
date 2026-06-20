using System;
using System.Collections.Concurrent;
using System.IO;
using System.Runtime.CompilerServices;
using MelonLoader;

namespace MDEN.UI.Core
{
    public static class MainThreadDispatcher
    {
        private static readonly ConcurrentQueue<QueuedAction> _executionQueue = new ConcurrentQueue<QueuedAction>();

        public static void Enqueue(
            Action action,
            [CallerMemberName] string callerMemberName = null,
            [CallerFilePath] string callerFilePath = null)
        {
            if (action != null)
                _executionQueue.Enqueue(new QueuedAction(
                    action,
                    BuildActionName(callerMemberName, callerFilePath)));
        }

        internal static void ProcessQueue()
        {
            var count = _executionQueue.Count;
            for (var i = 0; i < count && _executionQueue.TryDequeue(out var queued); i++)
            {
                try
                {
                    using (PerfTrace.Measure(queued.Name))
                    {
                        queued.Action();
                    }
                }
                catch (Exception ex)
                {
                    MelonLogger.Error($"[MainThreadDispatcher] Error executing action: {ex}");
                }
            }
        }

        private static string BuildActionName(string callerMemberName, string callerFilePath)
        {
            var typeName = string.IsNullOrWhiteSpace(callerFilePath)
                ? "Unknown"
                : Path.GetFileNameWithoutExtension(callerFilePath);
            var memberName = string.IsNullOrWhiteSpace(callerMemberName)
                ? "Unknown"
                : callerMemberName;
            return $"MDEN.Queue.{typeName}.{memberName}";
        }

        private readonly struct QueuedAction
        {
            public QueuedAction(Action action, string name)
            {
                Action = action;
                Name = name;
            }

            public readonly Action Action;
            public readonly string Name;
        }
    }
}
