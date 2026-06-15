using System;
using Il2CppAssets.Scripts.Database;

namespace MDEN.Managers
{
    public static class GameAccountManager
    {
        private static GameAccountInfo _cachedAccount;
        private static GameSelectionInfo _cachedSelection = new GameSelectionInfo(0, -1);
        private static bool _hasCachedAccount;

        public static void RefreshSnapshot()
        {
            var uid = ReadPeroUid();
            var nickname = ReadNickname();

            if (!string.IsNullOrWhiteSpace(uid))
            {
                if (string.IsNullOrWhiteSpace(nickname))
                {
                    ModConfigManager.LoadConfig();
                    nickname = ModConfigManager.PlayerName;
                }

                if (string.IsNullOrWhiteSpace(nickname))
                {
                    nickname = $"Player{uid.Substring(0, Math.Min(6, uid.Length))}";
                }

                _cachedAccount = new GameAccountInfo(uid, nickname);
                _hasCachedAccount = true;
            }

            _cachedSelection = new GameSelectionInfo(ReadSelectedRoleIndex(), ReadSelectedElfinIndex());
        }

        public static GameSelectionInfo RefreshSelectionSnapshot()
        {
            _cachedSelection = new GameSelectionInfo(ReadSelectedRoleIndex(), ReadSelectedElfinIndex());
            return _cachedSelection;
        }

        public static GameAccountInfo GetCurrentAccount()
        {
            if (!_hasCachedAccount || string.IsNullOrWhiteSpace(_cachedAccount.Uid))
            {
                throw new InvalidOperationException("PeroUid is not available. Please log in to the game account first.");
            }

            return _cachedAccount;
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
            return _cachedSelection;
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

    public readonly struct GameSelectionInfo : IEquatable<GameSelectionInfo>
    {
        public GameSelectionInfo(int girlIndex, int elfinIndex)
        {
            GirlIndex = girlIndex;
            ElfinIndex = elfinIndex;
        }

        public int GirlIndex { get; }
        public int ElfinIndex { get; }

        public bool Equals(GameSelectionInfo other)
        {
            return GirlIndex == other.GirlIndex && ElfinIndex == other.ElfinIndex;
        }

        public override bool Equals(object obj)
        {
            return obj is GameSelectionInfo other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (GirlIndex * 397) ^ ElfinIndex;
            }
        }
    }
}
