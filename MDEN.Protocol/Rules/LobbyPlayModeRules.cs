using System;
using MDEN.Protocol.Enums;

namespace MDEN.Protocol.Rules
{
    public static class LobbyPlayModeRules
    {
        public const byte DefaultTenziSongsPerPlayer = 1;
        public const byte MinTenziSongsPerPlayer = 1;
        public const byte MaxTenziSongsPerPlayer = 4;

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

        public static byte GetTenziSongsPerPlayerMax(ushort playlistSize)
        {
            return (byte)Math.Min(MaxTenziSongsPerPlayer, Math.Max(MinTenziSongsPerPlayer, playlistSize));
        }

        public static bool IsValidTenziSongsPerPlayer(byte value, ushort playlistSize)
        {
            return value >= MinTenziSongsPerPlayer && value <= GetTenziSongsPerPlayerMax(playlistSize);
        }

        public static byte NormalizeTenziSongsPerPlayer(byte value, ushort playlistSize)
        {
            if (value < MinTenziSongsPerPlayer)
            {
                return DefaultTenziSongsPerPlayer;
            }

            var max = GetTenziSongsPerPlayerMax(playlistSize);
            return value > max ? max : value;
        }
    }
}
