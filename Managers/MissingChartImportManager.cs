using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using CustomAlbums.Data;
using CustomAlbums.Managers;
using MDEN.Protocol.Rules;
using MDEN.UI.Core;

namespace MDEN.Managers
{
    internal static class MissingChartImportManager
    {
        private static readonly SemaphoreSlim ImportLock = new SemaphoreSlim(1, 1);

        internal static void HandleMissingChartClick(string chartName, string chartKey = null, int difficulty = 0)
        {
            chartName = CleanChartName(chartName);
            if (string.IsNullOrWhiteSpace(chartName)) return;

            System.Threading.Tasks.Task.Run(() => FindAndActivate(chartName, chartKey, difficulty));
        }

        private static void FindAndActivate(string chartName, string chartKey, int difficulty)
        {
            var locked = false;
            try
            {
                ImportLock.Wait();
                locked = true;

                LibraryManager.RefreshIndex();
                var match = FindMatch(chartName, chartKey);
                if (match.Status != MissingChartMatchStatus.Unique)
                {
                    LogMatchFailure(chartName, match);
                    if (TryHandleMuseDashToolSearch(chartName, chartKey, difficulty))
                    {
                        return;
                    }

                    MainThreadDispatcher.Enqueue(() => ShowFallbackStatus(chartName, match.Status));
                    return;
                }

                MainThreadDispatcher.Enqueue(() => ActivateMatch(chartName, match.Entry));
            }
            catch (Exception ex)
            {
                ClientLogManager.Warning($"Missing chart library search failed: {ex.Message}");
                MainThreadDispatcher.Enqueue(() => ShowFallbackStatus(chartName, MissingChartMatchStatus.Error));
            }
            finally
            {
                if (locked) ImportLock.Release();
            }
        }

        private static bool TryHandleMuseDashToolSearch(string chartName, string chartKey, int difficulty)
        {
            if (!IsCustomChartKey(chartKey))
            {
                return false;
            }

            if (MuseDashToolBridge.OpenGlobalSearch(chartName, chartKey, difficulty))
            {
                MainThreadDispatcher.Enqueue(() => UiNotificationManager.RequestToast(I18nManager.T("missing_chart.musedashtool_opened")));
                return true;
            }

            MainThreadDispatcher.Enqueue(() => UiNotificationManager.RequestToast(I18nManager.T("missing_chart.musedashtool_not_running")));
            return true;
        }

        private static void ActivateMatch(string chartName, LibraryAlbumEntry entry)
        {
            if (entry == null)
            {
                ShowFallbackStatus(chartName, MissingChartMatchStatus.NotFound);
                return;
            }

            try
            {
                if (entry.IsActive)
                {
                    MissingChartSearchNavigator.ShowAlreadyAvailable(entry.Info.Name);
                    return;
                }

                if (!LibraryManager.Activate(entry))
                {
                    ShowFallbackStatus(chartName, MissingChartMatchStatus.Error);
                    return;
                }

                PlayerManager.SyncChartStateFireAndForget();
                MissingChartSearchNavigator.ShowActivated(entry.Info.Name);
            }
            catch (Exception ex)
            {
                ClientLogManager.Warning($"Missing chart activate failed: {ex.Message}");
                ShowFallbackStatus(chartName, MissingChartMatchStatus.Error);
            }
        }

        private static MissingChartMatch FindMatch(string chartName, string chartKey)
        {
            var entries = LibraryManager.Entries.ToList();
            if (entries.Count == 0) return MissingChartMatch.NotFound;

            var keys = BuildSearchKeys(chartName);
            if (IsCustomChartKey(chartKey))
            {
                var md5Matches = entries
                    .Where(entry => LibraryManager.HasChartMd5(entry, chartKey))
                    .ToList();
                if (md5Matches.Count == 1) return MissingChartMatch.Unique(md5Matches[0]);
                if (md5Matches.Count > 1) return MissingChartMatch.Multiple;
            }

            var exact = entries.Where(entry => keys.Any(key => IsEntrySame(entry, key))).ToList();
            if (exact.Count == 1) return MissingChartMatch.Unique(exact[0]);
            if (exact.Count > 1) return MissingChartMatch.Multiple;

            var searched = keys
                .Where(key => key.Length >= 3)
                .SelectMany(key => LibraryManager.Search(key))
                .Distinct()
                .Where(entry => keys.Any(key => IsStrongSearchMatch(entry, key)))
                .ToList();

            if (searched.Count == 1) return MissingChartMatch.Unique(searched[0]);
            if (searched.Count > 1) return MissingChartMatch.Multiple;

            var samples = entries
                .Where(entry => keys.Any(key => HasPartialMatch(entry, key)))
                .Take(5)
                .Select(entry => entry.Info.Name)
                .ToList();
            return samples.Count > 0 ? MissingChartMatch.NotFoundWithSamples(samples) : MissingChartMatch.NotFound;
        }

        private static List<string> BuildSearchKeys(string chartName)
        {
            var keys = new List<string>();
            AddKey(chartName);
            AddKey(RemoveDifficultySuffix(chartName));

            return keys
                .Where(key => !string.IsNullOrWhiteSpace(key))
                .Select(key => key.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            void AddKey(string value)
            {
                if (string.IsNullOrWhiteSpace(value)) return;
                keys.Add(value.Trim());
                var normalized = NormalizeForMatch(value);
                if (!string.IsNullOrWhiteSpace(normalized)) keys.Add(normalized);
            }
        }

        private static bool IsEntrySame(LibraryAlbumEntry entry, string key)
        {
            return IsSame(entry.Info.Name, key) ||
                   IsSame(entry.Info.NameRomanized, key) ||
                   IsSame(System.IO.Path.GetFileNameWithoutExtension(entry.FileName), key) ||
                   IsSame(System.IO.Path.GetFileNameWithoutExtension(RemoveLeadingCategory(entry.FileName)), key);
        }

        private static bool IsStrongSearchMatch(LibraryAlbumEntry entry, string key)
        {
            var normalizedKey = NormalizeForMatch(key);
            if (normalizedKey.Length < 3) return false;

            return GetEntryMatchValues(entry).Any(value =>
            {
                var normalizedValue = NormalizeForMatch(value);
                return normalizedValue.Length >= 3 &&
                       (normalizedValue == normalizedKey ||
                        normalizedValue.Contains(normalizedKey) ||
                        normalizedKey.Contains(normalizedValue));
            });
        }

        private static bool HasPartialMatch(LibraryAlbumEntry entry, string key)
        {
            var normalizedKey = NormalizeForMatch(key);
            return normalizedKey.Length >= 3 &&
                   GetEntryMatchValues(entry).Any(value => NormalizeForMatch(value).Contains(normalizedKey));
        }

        private static IEnumerable<string> GetEntryMatchValues(LibraryAlbumEntry entry)
        {
            yield return entry.Info.Name;
            yield return entry.Info.NameRomanized;
            yield return entry.Info.Author;
            yield return System.IO.Path.GetFileNameWithoutExtension(entry.FileName);
            yield return System.IO.Path.GetFileNameWithoutExtension(RemoveLeadingCategory(entry.FileName));
        }

        private static void ShowFallbackStatus(string chartName, MissingChartMatchStatus status)
        {
            MissingChartSearchNavigator.ShowFallback(chartName, MapFallbackStatus(status));
        }

        private static MissingChartFallbackStatus MapFallbackStatus(MissingChartMatchStatus status)
        {
            return status switch
            {
                MissingChartMatchStatus.Multiple => MissingChartFallbackStatus.Multiple,
                MissingChartMatchStatus.Error => MissingChartFallbackStatus.Error,
                _ => MissingChartFallbackStatus.NotFound
            };
        }

        private static void LogMatchFailure(string chartName, MissingChartMatch match)
        {
            var sample = match.Samples.Count == 0 ? string.Empty : $" Samples: {string.Join(" | ", match.Samples)}";
            ClientLogManager.Warning($"Missing chart auto import fallback. Query='{chartName}', Status={match.Status}.{sample}");
        }

        private static string CleanChartName(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return string.Empty;
            value = Regex.Replace(value, "<.*?>", string.Empty);
            value = RemoveLeadingCategory(value);
            value = RemoveDifficultySuffix(value);
            return value.Trim();
        }

        private static string RemoveLeadingCategory(string value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? string.Empty
                : Regex.Replace(value, @"^([【\[].*?[\]】]\s*)", string.Empty).Trim();
        }

        private static string RemoveDifficultySuffix(string value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? string.Empty
                : Regex.Replace(value, @"\s+\d+(\.\d+)?\s*(★|\*)\s*$", string.Empty).Trim();
        }

        private static string NormalizeForMatch(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return string.Empty;

            value = CleanChartName(value).ToLowerInvariant();
            value = value
                .Replace('（', '(')
                .Replace('）', ')')
                .Replace('！', '!')
                .Replace('？', '?')
                .Replace('：', ':')
                .Replace('，', ',')
                .Replace('。', '.')
                .Replace('　', ' ')
                .Replace("☆", string.Empty)
                .Replace("★", string.Empty);

            return Regex.Replace(value, @"[\s\-_·・~～'""`.,:;!?()\[\]【】]+", string.Empty);
        }

        private static bool IsSame(string left, string right)
        {
            return !string.IsNullOrWhiteSpace(left) &&
                   !string.IsNullOrWhiteSpace(right) &&
                   (string.Equals(left.Trim(), right.Trim(), StringComparison.OrdinalIgnoreCase) ||
                    NormalizeForMatch(left) == NormalizeForMatch(right));
        }

        private static bool IsCustomChartKey(string value)
        {
            return ChartSelectionRules.IsCustomChartKey(value);
        }

        private readonly struct MissingChartMatch
        {
            internal static readonly MissingChartMatch NotFound = new MissingChartMatch(MissingChartMatchStatus.NotFound, null);
            internal static readonly MissingChartMatch Multiple = new MissingChartMatch(MissingChartMatchStatus.Multiple, null);

            internal MissingChartMatchStatus Status { get; }
            internal LibraryAlbumEntry Entry { get; }
            internal IReadOnlyList<string> Samples { get; }

            private MissingChartMatch(MissingChartMatchStatus status, LibraryAlbumEntry entry, IReadOnlyList<string> samples = null)
            {
                Status = status;
                Entry = entry;
                Samples = samples ?? Array.Empty<string>();
            }

            internal static MissingChartMatch Unique(LibraryAlbumEntry entry)
            {
                return new MissingChartMatch(MissingChartMatchStatus.Unique, entry);
            }

            internal static MissingChartMatch NotFoundWithSamples(IReadOnlyList<string> samples)
            {
                return new MissingChartMatch(MissingChartMatchStatus.NotFound, null, samples);
            }
        }

        private enum MissingChartMatchStatus
        {
            Unique,
            NotFound,
            Multiple,
            Error
        }
    }
}
