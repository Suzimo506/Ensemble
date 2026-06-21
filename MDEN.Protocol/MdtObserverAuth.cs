using System;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace MDEN.Protocol
{
    public static class MdtObserverAuth
    {
        private const string SignatureVersion = "MDT_OBSERVER_V1";
        public const int MaxNonceLength = 64;

        public const string DefaultSharedSecret =
            "MuseDashTOOL-MDT-Observer-Auth-v1";

        public static string NormalizeNonce(string? value)
        {
            return (value ?? string.Empty)
                .Replace("\r", string.Empty)
                .Replace("\n", string.Empty)
                .Trim();
        }

        public static string NormalizeSignature(string? value)
        {
            return (value ?? string.Empty)
                .Replace("\r", string.Empty)
                .Replace("\n", string.Empty)
                .Trim();
        }

        public static string CreateSignature(
            string? uid,
            string? clientName,
            long timestampUnixMs,
            string? nonce,
            string sharedSecret)
        {
            if (string.IsNullOrWhiteSpace(sharedSecret))
            {
                return string.Empty;
            }

            var key = Encoding.UTF8.GetBytes(sharedSecret);
            var payload = Encoding.UTF8.GetBytes(BuildPayload(uid, clientName, timestampUnixMs, nonce));
            using var hmac = new HMACSHA256(key);
            return Convert.ToBase64String(hmac.ComputeHash(payload));
        }

        public static bool VerifySignature(
            string? uid,
            string? clientName,
            long timestampUnixMs,
            string? nonce,
            string? signature,
            string sharedSecret)
        {
            var normalizedSignature = NormalizeSignature(signature);
            if (string.IsNullOrWhiteSpace(sharedSecret) || string.IsNullOrEmpty(normalizedSignature))
            {
                return false;
            }

            var expected = CreateSignature(uid, clientName, timestampUnixMs, nonce, sharedSecret);
            var expectedBytes = Encoding.UTF8.GetBytes(expected);
            var actualBytes = Encoding.UTF8.GetBytes(normalizedSignature);
            return CryptographicOperations.FixedTimeEquals(expectedBytes, actualBytes);
        }

        private static string BuildPayload(
            string? uid,
            string? clientName,
            long timestampUnixMs,
            string? nonce)
        {
            return string.Join(
                "\n",
                SignatureVersion,
                MdtIdentity.NormalizeUid(uid),
                MdtIdentity.NormalizeName(clientName),
                timestampUnixMs.ToString(CultureInfo.InvariantCulture),
                NormalizeNonce(nonce));
        }
    }
}
