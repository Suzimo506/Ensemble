using System;
using System.Threading;
using MelonLoader;

namespace MDEN.UI.Core
{
    internal static class MainThreadWatchdog
    {
        private const double StallThresholdSeconds = 5.0;
        private const double WarningCooldownSeconds = 20.0;
        private static long _lastHeartbeatTicks;
        private static long _lastWarningTicks;
        private static volatile string _lastStage = "not-started";
        private static volatile string _currentScene = "unknown";

        public static void Initialize()
        {
            Interlocked.Exchange(ref _lastHeartbeatTicks, 0);
            Interlocked.Exchange(ref _lastWarningTicks, 0);
            _lastStage = "waiting-for-first-heartbeat";
            _currentScene = "unknown";
        }

        public static void Shutdown()
        {
            Interlocked.Exchange(ref _lastHeartbeatTicks, 0);
            Interlocked.Exchange(ref _lastWarningTicks, 0);
        }

        public static void Heartbeat(string stage)
        {
            var nowTicks = DateTime.UtcNow.Ticks;
            var previousTicks = Interlocked.Exchange(ref _lastHeartbeatTicks, nowTicks);
            var previousStage = _lastStage;
            _lastStage = string.IsNullOrWhiteSpace(stage) ? "unknown" : stage;

            if (previousTicks <= 0) return;

            var stalledSeconds = TimeSpan.FromTicks(nowTicks - previousTicks).TotalSeconds;
            if (stalledSeconds < StallThresholdSeconds) return;

            var lastWarningTicks = Interlocked.Read(ref _lastWarningTicks);
            if (lastWarningTicks > 0 &&
                TimeSpan.FromTicks(nowTicks - lastWarningTicks).TotalSeconds < WarningCooldownSeconds)
            {
                return;
            }

            Interlocked.Exchange(ref _lastWarningTicks, nowTicks);
            MelonLogger.Warning(
                $"[MDEN.Watchdog] Main thread stalled for {stalledSeconds:F1}s, scene={_currentScene}, lastStage={previousStage}");
        }

        public static void SetScene(string sceneName)
        {
            _currentScene = string.IsNullOrWhiteSpace(sceneName) ? "unknown" : sceneName;
            Heartbeat("SceneLoaded." + _currentScene);
        }
    }
}
