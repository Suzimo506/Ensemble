using System;
using System.Threading;
using MelonLoader;

namespace MDEN.UI.Core
{
    internal static class MainThreadWatchdog
    {
        private const int CheckIntervalMs = 2000;
        private const double StallThresholdSeconds = 5.0;
        private const double WarningCooldownSeconds = 20.0;
        private static readonly object SyncRoot = new object();
        private static Timer _timer;
        private static long _lastHeartbeatTicks;
        private static long _lastWarningTicks;
        private static volatile string _lastStage = "not-started";
        private static volatile string _currentScene = "unknown";

        public static void Initialize()
        {
            lock (SyncRoot)
            {
                _lastHeartbeatTicks = DateTime.UtcNow.Ticks;
                _lastWarningTicks = 0;
                _lastStage = "initialized";
                _currentScene = "unknown";
                _timer?.Dispose();
                _timer = new Timer(Check, null, CheckIntervalMs, CheckIntervalMs);
            }
        }

        public static void Shutdown()
        {
            lock (SyncRoot)
            {
                _timer?.Dispose();
                _timer = null;
            }
        }

        public static void Heartbeat(string stage)
        {
            _lastStage = string.IsNullOrWhiteSpace(stage) ? "unknown" : stage;
            Interlocked.Exchange(ref _lastHeartbeatTicks, DateTime.UtcNow.Ticks);
        }

        public static void SetScene(string sceneName)
        {
            _currentScene = string.IsNullOrWhiteSpace(sceneName) ? "unknown" : sceneName;
            Heartbeat("SceneLoaded." + _currentScene);
        }

        private static void Check(object state)
        {
            var nowTicks = DateTime.UtcNow.Ticks;
            var heartbeatTicks = Interlocked.Read(ref _lastHeartbeatTicks);
            if (heartbeatTicks <= 0) return;

            var stalledSeconds = TimeSpan.FromTicks(nowTicks - heartbeatTicks).TotalSeconds;
            if (stalledSeconds < StallThresholdSeconds) return;

            var lastWarningTicks = Interlocked.Read(ref _lastWarningTicks);
            if (lastWarningTicks > 0 &&
                TimeSpan.FromTicks(nowTicks - lastWarningTicks).TotalSeconds < WarningCooldownSeconds)
            {
                return;
            }

            Interlocked.Exchange(ref _lastWarningTicks, nowTicks);
            MelonLogger.Warning(
                $"[MDEN.Watchdog] Main thread stalled for {stalledSeconds:F1}s, scene={_currentScene}, lastStage={_lastStage}");
        }
    }
}
