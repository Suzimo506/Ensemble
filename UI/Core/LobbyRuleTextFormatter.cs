using MDEN.Protocol.Enums;
using MDEN.Protocol.Rules;
using MDEN.Managers;

namespace MDEN.UI.Core
{
    internal static class LobbyRuleTextFormatter
    {
        public static LobbyPlayMode GetNextPlayMode(byte mode)
        {
            return LobbyPlayModeRules.Normalize(mode) switch
            {
                LobbyPlayMode.Normal => LobbyPlayMode.Rookie,
                LobbyPlayMode.Rookie => LobbyPlayMode.Fearless,
                LobbyPlayMode.Fearless => LobbyPlayMode.Tenzi,
                _ => LobbyPlayMode.Normal
            };
        }

        public static string GetPlayModeName(byte mode)
        {
            return LobbyPlayModeRules.Normalize(mode) switch
            {
                LobbyPlayMode.Rookie => I18nManager.T("mode.rookie"),
                LobbyPlayMode.Fearless => I18nManager.T("mode.fearless"),
                LobbyPlayMode.Tenzi => I18nManager.T("mode.tenzi"),
                _ => I18nManager.T("mode.normal")
            };
        }

        public static string GetPlayModeColor(byte mode)
        {
            return LobbyPlayModeRules.Normalize(mode) switch
            {
                LobbyPlayMode.Rookie => "00ff00ff",
                LobbyPlayMode.Fearless => "ff5555ff",
                LobbyPlayMode.Tenzi => "00ffd0ff",
                _ => "fff700ff"
            };
        }

        public static string FormatPlayMode(byte mode, bool includeHash)
        {
            return ColorText(GetPlayModeName(mode), GetPlayModeColor(mode), includeHash);
        }

        public static string GetDifficultyName(int difficulty)
        {
            return difficulty switch
            {
                DifficultyDisplayRules.Easy => I18nManager.T("difficulty.easy"),
                DifficultyDisplayRules.Hard => I18nManager.T("difficulty.hard"),
                DifficultyDisplayRules.Master => I18nManager.T("difficulty.master"),
                DifficultyDisplayRules.Hidden => I18nManager.T("difficulty.hidden"),
                _ => I18nManager.Tf("difficulty.unknown", difficulty)
            };
        }

        public static string GetDifficultyColor(int difficulty)
        {
            return difficulty switch
            {
                DifficultyDisplayRules.Easy => "00d45aff",
                DifficultyDisplayRules.Hard => "4564ffff",
                DifficultyDisplayRules.Master => "9b55ffff",
                DifficultyDisplayRules.Hidden => "ff5555ff",
                _ => "b8b8b8ff"
            };
        }

        public static string FormatDifficulty(int difficulty, bool includeHash)
        {
            return ColorText(GetDifficultyName(difficulty), GetDifficultyColor(difficulty), includeHash);
        }

        private static string ColorText(string value, string color, bool includeHash)
        {
            var hash = includeHash ? "#" : string.Empty;
            return $"<color={hash}{color}>{value}</color>";
        }
    }
}
