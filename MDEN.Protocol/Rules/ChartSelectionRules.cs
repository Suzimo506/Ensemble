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

        public static bool IsUnsupportedChartKey(string chartKey)
        {
            return !string.IsNullOrWhiteSpace(chartKey) && UnsupportedChartUidSet.Contains(chartKey);
        }

        public static bool IsUnsupportedPlaylistEntry(string entry)
        {
            if (string.IsNullOrWhiteSpace(entry)) return false;

            var separatorIndex = entry.IndexOf('#');
            var chartKey = separatorIndex >= 0 ? entry.Substring(0, separatorIndex) : entry;
            return IsUnsupportedChartKey(chartKey);
        }
    }
}
