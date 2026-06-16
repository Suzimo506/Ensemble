using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Il2CppAssets.Scripts.Database;
using Il2CppAssets.Scripts.PeroTools.Commons;
using Il2CppAssets.Scripts.PeroTools.Managers;
using MelonLoader;

namespace MDEN.Managers
{
    public static class RecommendedConfigManager
    {
        private static readonly HttpClient Client = new HttpClient();
        private static readonly object SyncRoot = new object();
        private static RecommendedConfig _current;
        private static string _currentEntryKey;
        private static string _loadingEntryKey;
        private static string _lastRequestedEntryKey;

        public static RecommendedConfig Current
        {
            get
            {
                lock (SyncRoot)
                {
                    return _current;
                }
            }
        }

        public static bool IsLoading(PlaylistEntryViewModel entry)
        {
            if (entry == null) return false;
            lock (SyncRoot)
            {
                return _loadingEntryKey == GetRecommendationKey(entry);
            }
        }

        public static void Request(PlaylistEntryViewModel entry)
        {
            if (entry == null) return;

            var key = GetRecommendationKey(entry);
            lock (SyncRoot)
            {
                if (_lastRequestedEntryKey == key) return;
                _lastRequestedEntryKey = key;
                if (_currentEntryKey == key || _loadingEntryKey == key) return;
                _loadingEntryKey = key;
                _currentEntryKey = null;
                _current = null;
            }

            _ = LoadAsync(entry, key);
        }

        public static bool IsCurrentEquipped()
        {
            var current = Current;
            if (current == null) return false;

            var selection = GameAccountManager.RefreshSelectionSnapshot();
            return selection.GirlIndex == current.GirlIndex &&
                   selection.ElfinIndex == current.ElfinIndex;
        }

        public static async Task ApplyCurrentAsync()
        {
            var current = Current;
            if (current == null) return;

            DataHelper.selectedRoleIndex = current.GirlIndex;
            DataHelper.selectedElfinIndex = current.ElfinIndex;

            var selection = GameAccountManager.RefreshSelectionSnapshot();
            await PlayerManager.SyncSelectionAsync(selection);
        }

        public static string GetDisplayText()
        {
            var current = Current;
            if (current == null) return "查询中...";
            return $"{GetGirlName(current.GirlIndex)} / {GetElfinName(current.ElfinIndex)}";
        }

        public static string GetDisplayText(PlaylistEntryViewModel entry)
        {
            var current = Current;
            if (current != null) return GetDisplayText();
            return IsLoading(entry) ? "查询中..." : "暂无推荐";
        }

        private static async Task LoadAsync(PlaylistEntryViewModel entry, string key)
        {
            try
            {
                var config = await QueryTopConfigAsync(entry);
                lock (SyncRoot)
                {
                    if (_loadingEntryKey != key) return;
                    _current = config;
                    _currentEntryKey = key;
                    _loadingEntryKey = null;
                }
            }
            catch (Exception ex)
            {
                lock (SyncRoot)
                {
                    if (_loadingEntryKey == key)
                    {
                        _currentEntryKey = key;
                        _loadingEntryKey = null;
                    }
                }

                MelonLogger.Warning($"Load recommended config failed: {ex.Message}");
            }
        }

        private static async Task<RecommendedConfig> QueryTopConfigAsync(PlaylistEntryViewModel entry)
        {
            if (IsCustomEntry(entry))
            {
                using var response = await Client.GetAsync($"https://api.mdmc.moe/v3/sheets/{entry.ChartKey}/scores?limit=1");
                if (!response.IsSuccessStatusCode) return null;

                var body = await response.Content.ReadFromJsonAsync<Dictionary<string, JsonElement>>();
                if (body == null || !body.TryGetValue("scores", out var scoresElement)) return null;

                var scores = scoresElement.Deserialize<List<Dictionary<string, JsonElement>>>();
                if (scores == null || scores.Count == 0) return null;

                var topScore = scores[0];
                return new RecommendedConfig(
                    topScore["characterId"].GetInt32(),
                    topScore["elfinId"].GetInt32());
            }

            var musicInfo = ChartManager.GetMusicInfo(entry.ChartKey);
            if (musicInfo == null) return null;

            var difficulty = Math.Max(0, entry.Difficulty - 1);
            using var rankResponse = await Client.GetAsync($"https://api.musedash.moe/rank/{musicInfo.uid}/{difficulty}/all");
            if (!rankResponse.IsSuccessStatusCode) return null;

            var ranks = await rankResponse.Content.ReadFromJsonAsync<List<List<JsonElement>>>();
            if (ranks == null || ranks.Count == 0 || ranks[0].Count < 8) return null;

            return new RecommendedConfig(
                int.Parse(ranks[0][6].GetString()),
                int.Parse(ranks[0][7].GetString()));
        }

        private static bool IsCustomEntry(PlaylistEntryViewModel entry)
        {
            var musicInfo = ChartManager.GetMusicInfo(entry.ChartKey);
            if (musicInfo != null && !string.IsNullOrEmpty(musicInfo.uid))
            {
                return musicInfo.uid.StartsWith("999-");
            }

            return !string.IsNullOrWhiteSpace(entry.ChartKey) &&
                   entry.ChartKey.Length >= 16 &&
                   !entry.ChartKey.Contains("-");
        }

        private static string GetRecommendationKey(PlaylistEntryViewModel entry)
        {
            return $"{entry.ChartKey}#{entry.Difficulty}";
        }

        private static string GetGirlName(int girlId)
        {
            if (girlId < 0) return string.Empty;

            try
            {
                var configManager = Singleton<ConfigManager>.instance;
                var character = configManager.GetJson("character", true)[girlId];
                var characterType = configManager
                    .GetConfigObject<DBConfigCharacter>()
                    .GetCharacterInfoByIndex(girlId)
                    .characterType;

                return string.Equals(characterType, "Special")
                    ? character["characterName"].ToString()
                    : character["cosName"].ToString();
            }
            catch
            {
                return $"角色 {girlId}";
            }
        }

        private static string GetElfinName(int elfinId)
        {
            if (elfinId < 0) return string.Empty;

            try
            {
                return Singleton<ConfigManager>.instance
                    .GetJson("elfin", true)[elfinId]["name"]
                    .ToString();
            }
            catch
            {
                return $"精灵 {elfinId}";
            }
        }
    }

    public sealed class RecommendedConfig
    {
        public RecommendedConfig(int girlIndex, int elfinIndex)
        {
            GirlIndex = girlIndex;
            ElfinIndex = elfinIndex;
        }

        public int GirlIndex { get; }
        public int ElfinIndex { get; }
    }
}
