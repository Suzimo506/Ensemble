using MDEN.Protocol.Enums;

namespace MDEN.Protocol.Rules
{
    public static class LobbyPlayModeRules
    {
        public static LobbyPlayMode Normalize(byte mode)
        {
            var value = (LobbyPlayMode)mode;
            return value == LobbyPlayMode.Rookie ||
                   value == LobbyPlayMode.Fearless ||
                   value == LobbyPlayMode.Tenzi
                ? value
                : LobbyPlayMode.Normal;
        }

        public static bool IsRookie(byte mode)
        {
            return Normalize(mode) == LobbyPlayMode.Rookie;
        }

        public static bool IsFearless(byte mode)
        {
            return Normalize(mode) == LobbyPlayMode.Fearless;
        }

        public static bool IsTenzi(byte mode)
        {
            return Normalize(mode) == LobbyPlayMode.Tenzi;
        }
    }
}
