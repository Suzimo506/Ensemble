using System;
using System.Linq;
using System.Reflection;
using Il2CppAssets.Scripts.Database;
using MelonLoader;

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

        public static GameSelectionInfo GetCurrentFavGirlSelection()
        {
            var current = GetCurrentSelection();
            return new GameSelectionInfo(
                ReadFavGirlIndex(current.GirlIndex),
                ReadFavElfinIndex(current.ElfinIndex));
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

        private static int ReadFavGirlIndex(int fallback)
        {
            return ReadFavGirlValue("FavGirl", fallback);
        }

        private static int ReadFavElfinIndex(int fallback)
        {
            return ReadFavGirlValue("FavElfin", fallback);
        }

        private static int ReadFavGirlValue(string propertyName, int fallback)
        {
            try
            {
                var assembly = AppDomain.CurrentDomain
                    .GetAssemblies()
                    .FirstOrDefault(a => string.Equals(a.GetName().Name, "FavGirl", StringComparison.OrdinalIgnoreCase));
                var saveType = assembly?.GetType("FavGirl.FavSave");
                var property = saveType?.GetProperty(propertyName, BindingFlags.Public | BindingFlags.Static);
                var value = property?.GetValue(null);
                return value is int intValue ? intValue : fallback;
            }
            catch (Exception ex)
            {
                MDEN.Managers.ClientLogManager.Warning($"Read FavGirl {propertyName} failed: {ex.Message}");
                return fallback;
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
