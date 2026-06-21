using System;
using System.Collections.Concurrent;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using MelonLoader;

namespace MDEN.UI.Core
{
    public static class MainThreadDispatcher
    {
        private const int MaxActionsPerFrame = 64;
        private const double MaxMillisecondsPerFrame = 4.0;
        private static readonly ConcurrentQueue<QueuedAction> _executionQueue = new ConcurrentQueue<QueuedAction>();

        public static void Enqueue(
            Action action,
            [CallerMemberName] string callerMemberName = null,
            [CallerFilePath] string callerFilePath = null)
        {
            if (action == null) return;

            var actionName = PerfTrace.Enabled
                ? BuildActionName(callerMemberName, callerFilePath)
                : null;
            _executionQueue.Enqueue(new QueuedAction(action, actionName));
        }

        public static Task InvokeAsync(
            Action action,
            [CallerMemberName] string callerMemberName = null,
            [CallerFilePath] string callerFilePath = null)
        {
            if (action == null) return Task.CompletedTask;

            var source = new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);
            Enqueue(
                () =>
                {
                    try
                    {
                        action();
                        source.TrySetResult(null);
                    }
                    catch (Exception ex)
                    {
                        source.TrySetException(ex);
                    }
                },
                callerMemberName,
                callerFilePath);
            return source.Task;
        }

        public static Task<T> InvokeAsync<T>(
            Func<T> action,
            [CallerMemberName] string callerMemberName = null,
            [CallerFilePath] string callerFilePath = null)
        {
            if (action == null) return Task.FromResult(default(T));

            var source = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
            Enqueue(
                () =>
                {
                    try
                    {
                        source.TrySetResult(action());
                    }
                    catch (Exception ex)
                    {
                        source.TrySetException(ex);
                    }
                },
                callerMemberName,
                callerFilePath);
            return source.Task;
        }

        internal static void ProcessQueue()
        {
            var processed = 0;
            var startedAt = System.Diagnostics.Stopwatch.GetTimestamp();
            while (_executionQueue.TryDequeue(out var queued))
            {
                try
                {
                    if (PerfTrace.Enabled && !string.IsNullOrEmpty(queued.Name))
                    {
                        using (PerfTrace.Measure(queued.Name))
                        {
                            queued.Action();
                        }
                    }
                    else
                    {
                        queued.Action();
                    }
                }
                catch (Exception ex)
                {
                    MelonLogger.Error($"[MainThreadDispatcher] Error executing action: {ex}");
                }

                processed++;
                if (processed >= MaxActionsPerFrame || HasExceededFrameBudget(startedAt))
                {
                    break;
                }
            }
        }

        private static bool HasExceededFrameBudget(long startedAt)
        {
            var elapsedMs = (System.Diagnostics.Stopwatch.GetTimestamp() - startedAt) * 1000d /
                            System.Diagnostics.Stopwatch.Frequency;
            return elapsedMs >= MaxMillisecondsPerFrame;
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
