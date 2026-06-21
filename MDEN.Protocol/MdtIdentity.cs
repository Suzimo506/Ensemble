using System;
using System.Text;

namespace MDEN.Protocol
{
    public static class MdtIdentity
    {
        private const ulong FnvOffsetBasis = 14695981039346656037UL;
        private const ulong FnvPrime = 1099511628211UL;
        private const ulong NameModulo = 1679616UL; // 36^4
        private const string Base36Alphabet = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ";
        public const int MaxUidLength = 64;
        public const int MaxNameLength = 5;
        public const string AnonymousName = "喵斯兔";

        public static string NormalizeUid(string? value)
        {
            var text = (value ?? string.Empty)
                .Replace("\r", string.Empty)
                .Replace("\n", string.Empty)
                .Trim();

            return text;
        }

        public static string GenerateNameFromUid(string? uid)
        {
            var normalizedUid = NormalizeUid(uid);
            if (string.IsNullOrWhiteSpace(normalizedUid))
            {
                return AnonymousName;
            }

            var hash = ComputeFnv1a64(normalizedUid);
            return "兔" + ToBase36(hash % NameModulo, 4);
        }

        public static string NormalizeName(string? value)
        {
            var text = (value ?? string.Empty)
                .Replace("\r", string.Empty)
                .Replace("\n", string.Empty)
                .Trim();

            return text;
        }

        public static bool IsGeneratedNameForUid(string? uid, string? name)
        {
            var normalizedUid = NormalizeUid(uid);
            if (string.IsNullOrWhiteSpace(normalizedUid))
            {
                return false;
            }

            var normalizedName = NormalizeName(name);
            if (normalizedName.Length > MaxNameLength)
            {
                return false;
            }

            return string.Equals(normalizedName, GenerateNameFromUid(normalizedUid), StringComparison.Ordinal);
        }

        private static ulong ComputeFnv1a64(string value)
        {
            var hash = FnvOffsetBasis;
            foreach (var b in Encoding.UTF8.GetBytes(value))
            {
                hash ^= b;
                hash *= FnvPrime;
            }

            return hash;
        }

        private static string ToBase36(ulong value, int width)
        {
            var chars = new char[width];
            for (var i = width - 1; i >= 0; i--)
            {
                chars[i] = Base36Alphabet[(int)(value % 36)];
                value /= 36;
            }

            return new string(chars);
        }
    }
}
