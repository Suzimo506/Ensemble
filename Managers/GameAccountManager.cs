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

        public static GameSelectionInfo GetCurrentSelection()
        {
            return new GameSelectionInfo(ReadSelectedRoleIndex(), ReadSelectedElfinIndex());
        }

        private static int ReadSelectedRoleIndex()
        {
            try
            {
                return DataHelper.selectedRoleIndex;
            }
            catch
            {
                return 0;
            }
        }

        private static int ReadSelectedElfinIndex()
        {
            try
            {
                return DataHelper.selectedElfinIndex;
            }
            catch
            {
                return -1;
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

    public readonly struct GameSelectionInfo
    {
        public GameSelectionInfo(int girlIndex, int elfinIndex)
        {
            GirlIndex = girlIndex;
            ElfinIndex = elfinIndex;
        }

        public int GirlIndex { get; }
        public int ElfinIndex { get; }
    }
}
