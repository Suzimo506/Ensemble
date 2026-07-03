using MDEN.Managers;
using MDEN.Protocol.Models;
using System;

namespace MDEN.UI.Core
{
    public static class PlayerInfoDescriptionFormatter
    {
        public static string Build(PlayerSyncEntry player, double? ratingLevel, bool ratingLevelLoaded)
        {
            return $"{Colorize(EscapeRichText(GetDisplayBio(player)), Constants.ColorProfilePink)}\n\n\n" +
                   $"{Colorize(I18nManager.Tf("player.total_multiplayer_games", GetTotalMultiplayerGames(player)), Constants.ColorBlue)}\n" +
                   $"{Colorize($"RL：{GetRatingLevelText(ratingLevel, ratingLevelLoaded)}", Constants.ColorYellow)}\n" +
                   $"{Colorize($"ping：{GetRealtimePing(player)} ms", GetPingColor(player))}";
        }

        public static ushort GetRealtimePing(PlayerSyncEntry player)
        {
            var uid = player?.Uid;
            if (!string.IsNullOrWhiteSpace(uid) && LobbyManager.CurrentLobby?.PlayerDetails != null)
            {
                foreach (var lobbyPlayer in LobbyManager.CurrentLobby.PlayerDetails)
                {
                    if (string.Equals(lobbyPlayer?.Uid, uid, StringComparison.Ordinal))
                    {
                        return lobbyPlayer.PingMS;
                    }
                }
            }

            return player?.PingMS ?? 0;
        }

        private static int GetTotalMultiplayerGames(PlayerSyncEntry player)
        {
            if (player?.Uid == PlayerManager.CurrentUid)
            {
                return Math.Max(
                    player.TotalMultiplayerGames,
                    PlayerManager.CurrentProfile?.TotalMultiplayerGames ?? 0);
            }

            return player?.TotalMultiplayerGames ?? 0;
        }

        private static string GetDisplayBio(PlayerSyncEntry player)
        {
            if (player?.Uid == PlayerManager.CurrentUid && !string.IsNullOrWhiteSpace(PlayerManager.CurrentProfile?.Bio))
            {
                return PlayerManager.CurrentProfile.Bio;
            }

            return string.IsNullOrWhiteSpace(player?.Bio) ? I18nManager.T("player.bio.empty") : player.Bio;
        }

        private static string GetRatingLevelText(double? ratingLevel, bool ratingLevelLoaded)
        {
            if (!ratingLevelLoaded) return I18nManager.T("common.loading");
            return ratingLevel.HasValue ? MuseDashMoeProfileManager.FormatRatingLevel(ratingLevel.Value) : I18nManager.T("common.unknown");
        }

        private static string GetPingColor(PlayerSyncEntry player)
        {
            var ping = GetRealtimePing(player);
            if (ping <= 80) return Constants.ColorSoftGreen;
            if (ping <= 180) return Constants.ColorYellow;
            return Constants.ColorRed;
        }

        private static string Colorize(string value, string color)
        {
            return $"<color={color}>{value}</color>";
        }

        private static string EscapeRichText(string value)
        {
            return value?.Replace("<", "＜").Replace(">", "＞") ?? string.Empty;
        }
    }
}
