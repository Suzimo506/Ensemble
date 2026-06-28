using System;
using System.Collections;
using System.Reflection;
using Il2CppAssets.Scripts.Database;

namespace MDEN.Managers
{
    internal static class SpecialChartVariantResolver
    {
        public static bool IsKnownVariantPair(string chartKey)
        {
            return GetBaseUid(chartKey) != null;
        }

        public static bool IsHiddenUnlockSong(string chartKey)
        {
            return chartKey == "0-53" ||
                   chartKey == "0-55" ||
                   chartKey == "33-12" ||
                   chartKey == "39-8" ||
                   chartKey == "0-57" ||
                   chartKey == "0-59";
        }

        public static string GetBaseUid(string chartKey)
        {
            return chartKey switch
            {
                "0-54" => "0-54",
                "0-53" => "0-54",
                "0-56" => "0-56",
                "0-55" => "0-56",
                "33-4" => "33-4",
                "33-12" => "33-4",
                "39-0" => "39-0",
                "39-8" => "39-0",
                "0-58" => "0-58",
                "0-57" => "0-58",
                "0-60" => "0-60",
                "0-59" => "0-60",
                _ => null
            };
        }

        public static MusicInfo ResolveMusicInfo(string chartKey, MusicInfo fallback)
        {
            if (!TryGetSpecialUnlockPair(chartKey, out var pair))
            {
                return fallback;
            }

            if (chartKey == pair.BaseUid && pair.BaseInfo != null) return pair.BaseInfo;
            if (chartKey == pair.HiddenUid && pair.HiddenInfo != null) return pair.HiddenInfo;
            return fallback;
        }

        public static void SyncSelection(string chartKey)
        {
            if (!IsKnownVariantPair(chartKey)) return;

            var dbMusicTag = GlobalDataBase.dbMusicTag;
            if (dbMusicTag != null && !string.IsNullOrEmpty(chartKey))
            {
                dbMusicTag.pnlSelectMusicUid = chartKey;
            }
        }

        private static bool TryGetSpecialUnlockPair(string chartKey, out SpecialUnlockPair pair)
        {
            pair = null;
            if (!IsKnownVariantPair(chartKey)) return false;

            try
            {
                foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    var managerType = assembly.GetType("HiddenQol.Managers.SpecialMusicManager");
                    if (managerType == null) continue;

                    var property = managerType.GetProperty(
                        "SpecialMusics",
                        BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                    var specialMusics = property?.GetValue(null) as IEnumerable;
                    if (specialMusics == null) continue;

                    foreach (var item in specialMusics)
                    {
                        var candidate = CreatePair(item);
                        if (candidate == null) continue;
                        if (chartKey != candidate.BaseUid && chartKey != candidate.HiddenUid) continue;

                        pair = candidate;
                        return true;
                    }
                }
            }
            catch
            {
            }

            return false;
        }

        private static SpecialUnlockPair CreatePair(object item)
        {
            if (item == null) return null;

            var type = item.GetType();
            return new SpecialUnlockPair
            {
                BaseUid = GetStringProperty(type, item, "BaseUid"),
                HiddenUid = GetStringProperty(type, item, "HiddenUid"),
                BaseInfo = GetMusicInfoProperty(type, item, "BaseInfo"),
                HiddenInfo = GetMusicInfoProperty(type, item, "HiddenInfo")
            };
        }

        private static string GetStringProperty(Type type, object item, string name)
        {
            return type.GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                ?.GetValue(item) as string;
        }

        private static MusicInfo GetMusicInfoProperty(Type type, object item, string name)
        {
            return type.GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                ?.GetValue(item) as MusicInfo;
        }

        private sealed class SpecialUnlockPair
        {
            public string BaseUid { get; set; }
            public string HiddenUid { get; set; }
            public MusicInfo BaseInfo { get; set; }
            public MusicInfo HiddenInfo { get; set; }
        }
    }
}
