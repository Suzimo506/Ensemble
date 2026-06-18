using System;
using System.Collections.Concurrent;
using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using MelonLoader;

namespace MDEN.Managers
{
    public static class MuseDashMoeProfileManager
    {
        private static readonly HttpClient Client = new HttpClient
        {
            BaseAddress = new Uri("https://api.musedash.moe/"),
            Timeout = TimeSpan.FromSeconds(8)
        };

        private static readonly ConcurrentDictionary<string, Task<double?>> RatingLevelCache =
            new ConcurrentDictionary<string, Task<double?>>();

        public static Task<double?> GetRatingLevelAsync(string uid)
        {
            if (string.IsNullOrWhiteSpace(uid))
            {
                return Task.FromResult<double?>(null);
            }

            return RatingLevelCache.GetOrAdd(uid.Trim(), QueryRatingLevelAsync);
        }

        private static async Task<double?> QueryRatingLevelAsync(string uid)
        {
            try
            {
                using var response = await Client.GetAsync($"player/{Uri.EscapeDataString(uid)}");
                if (!response.IsSuccessStatusCode)
                {
                    if (response.StatusCode != HttpStatusCode.NotFound)
                    {
                        RatingLevelCache.TryRemove(uid, out _);
                    }

                    return null;
                }

                var profile = await response.Content.ReadFromJsonAsync<PlayerRatingResponse>();
                return profile?.RatingLevel;
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Load musedash.moe rating level failed: {uid}, {ex.Message}");
                RatingLevelCache.TryRemove(uid, out _);
                return null;
            }
        }

        public static string FormatRatingLevel(double ratingLevel)
        {
            return ratingLevel.ToString("0.##", CultureInfo.InvariantCulture);
        }

        private sealed class PlayerRatingResponse
        {
            [JsonPropertyName("rl")]
            public double? RatingLevel { get; set; }
        }
    }
}
