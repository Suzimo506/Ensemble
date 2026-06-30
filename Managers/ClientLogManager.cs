using MelonLoader;

namespace MDEN.Managers
{
    public static class ClientLogManager
    {
        public static bool EnableVerboseLogs => ModConfigManager.EnableVerboseLogs;

        public static void Msg(string message)
        {
            if (EnableVerboseLogs)
            {
                MelonLogger.Msg(message);
            }
        }

        public static void Warning(string message)
        {
            if (EnableVerboseLogs)
            {
                MelonLogger.Warning(message);
            }
        }

        public static void SlowOperation(string message)
        {
            MelonLogger.Warning(message);
        }
    }
}
