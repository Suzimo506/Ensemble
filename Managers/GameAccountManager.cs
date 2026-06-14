using System;
using Il2CppAssets.Scripts.Database;

namespace MDEN.Managers
{
    public static class GameAccountManager
    {
        public static GameAccountInfo GetCurrentAccount()
        {
            var uid = ReadPeroUid();
            if (string.IsNullOrWhiteSpace(uid))
            {
                throw new InvalidOperationException("PeroUid is not available. Please log in to the game account first.");
            }

            var nickname = ReadNickname();
            if (string.IsNullOrWhiteSpace(nickname))
            {
                ModConfigManager.LoadConfig();
                nickname = ModConfigManager.PlayerName;
            }

            if (string.IsNullOrWhiteSpace(nickname))
            {
                nickname = $"Player{uid.Substring(0, Math.Min(6, uid.Length))}";
            }

            return new GameAccountInfo(uid, nickname);
        }

        private static string ReadPeroUid()
        {
            try
            {
                return DataHelper.PeroUid?.Trim();
            }
            catch
            {
                return null;
            }
        }

        private static string ReadNickname()
        {
            try
            {
                return DataHelper.nickname?.Trim('\n', '\r', ' ');
            }
            catch
            {
                return null;
            }
        }
    }

    public readonly struct GameAccountInfo
    {
        public GameAccountInfo(string uid, string nickname)
        {
            Uid = uid;
            Nickname = nickname;
        }

        public string Uid { get; }
        public string Nickname { get; }
    }
}
