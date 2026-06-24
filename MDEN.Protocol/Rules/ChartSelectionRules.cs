using System;
using System.Collections.Generic;

namespace MDEN.Protocol.Rules
{
    public static class ChartSelectionRules
    {
        private static readonly HashSet<string> UnsupportedChartUidSet = new(StringComparer.Ordinal)
        {
            "95-0",
            "93-0",
            "84-0",
            "72-0",
            "41-0"
        };

        public static IReadOnlyCollection<string> UnsupportedChartUids => UnsupportedChartUidSet;

        public static bool IsValidChartKey(string chartKey)
        {
            return IsOfficialChartKey(chartKey) || IsCustomChartKey(chartKey);
        }

        public static bool IsCustomChartKey(string chartKey)
        {
            if (string.IsNullOrWhiteSpace(chartKey) || chartKey.Length != 32) return false;

            for (var i = 0; i < chartKey.Length; i++)
            {
                var ch = chartKey[i];
                if (!((ch >= '0' && ch <= '9') ||
                      (ch >= 'a' && ch <= 'f') ||
                      (ch >= 'A' && ch <= 'F')))
                {
                    return false;
                }
            }

            return true;
        }

        public static bool IsFearlessAllowedChart(string chartKey, int difficulty)
        {
            return IsCustomChartIdentity(chartKey) ||
                   DifficultyDisplayRules.IsFearlessDifficulty(difficulty);
        }

        public static bool IsUnsupportedChartKey(string chartKey)
        {
            return !string.IsNullOrWhiteSpace(chartKey) &&
                   (!IsValidChartKey(chartKey) || UnsupportedChartUidSet.Contains(chartKey));
        }

        public static bool IsUnsupportedPlaylistEntry(string entry)
        {
            if (string.IsNullOrWhiteSpace(entry)) return false;

            var separatorIndex = entry.IndexOf('#');
            var chartKey = separatorIndex >= 0 ? entry.Substring(0, separatorIndex) : entry;
            return IsUnsupportedChartKey(chartKey);
        }

        private static bool IsCustomChartIdentity(string chartKey)
        {
            return IsCustomChartKey(chartKey) || IsCustomChartUid(chartKey);
        }

        private static bool IsCustomChartUid(string chartKey)
        {
            if (string.IsNullOrWhiteSpace(chartKey)) return false;

            return chartKey.StartsWith("999-", StringComparison.Ordinal) ||
                   chartKey.StartsWith("UID-999", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsOfficialChartKey(string chartKey)
        {
            if (string.IsNullOrWhiteSpace(chartKey)) return false;
            if (chartKey.StartsWith("999-", StringComparison.Ordinal)) return false;

            var separatorIndex = chartKey.IndexOf('-');
            if (separatorIndex <= 0 || separatorIndex >= chartKey.Length - 1) return false;
            if (chartKey.IndexOf('-', separatorIndex + 1) >= 0) return false;

            var variantIndex = chartKey.IndexOf('_', separatorIndex + 1);
            if (variantIndex >= 0)
            {
                if (variantIndex >= chartKey.Length - 1) return false;
                if (chartKey.IndexOf('_', variantIndex + 1) >= 0) return false;

                return IsAllDigits(chartKey, 0, separatorIndex) &&
                       IsAllDigits(chartKey, separatorIndex + 1, variantIndex) &&
                       IsAllDigits(chartKey, variantIndex + 1, chartKey.Length);
            }

            return IsAllDigits(chartKey, 0, separatorIndex) &&
                   IsAllDigits(chartKey, separatorIndex + 1, chartKey.Length);
        }

        private static bool IsAllDigits(string value, int start, int end)
        {
            for (var i = start; i < end; i++)
            {
                var ch = value[i];
                if (ch < '0' || ch > '9') return false;
            }

            return true;
        }
    }
}
