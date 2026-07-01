using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Il2CppAssets.Scripts.Database;

namespace MDEN.Managers
{
    internal static class SpecialChartVariantResolver
    {
        private static readonly Dictionary<string, SpecialUnlockPair> KnownPairCache = new Dictionary<string, SpecialUnlockPair>();
        private static VariantSelectionOverride _activeVariantOverride;

        public static bool IsKnownVariantPair(string chartKey)
        {
            return TryGetVariantPair(chartKey, out _);
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
            if (TryGetSpecialUnlockPair(chartKey, out var pair) &&
                !string.IsNullOrEmpty(pair.BaseUid) &&
                (chartKey == pair.BaseUid || chartKey == pair.HiddenUid))
            {
                return pair.BaseUid;
            }

            return GetKnownBaseUid(chartKey);
        }

        public static string ResolveSelectedUid(MusicInfo musicInfo, string selectedUid)
        {
            var slotInfo = GetSelectedSlotMusicInfo(selectedUid);
            if (TryResolveSelectedUidFromMusicInfo(slotInfo, selectedUid, out var slotResolvedUid))
            {
                return slotResolvedUid;
            }

            if (TryResolveSelectedUidFromMusicInfo(musicInfo, selectedUid, out var musicResolvedUid))
            {
                return musicResolvedUid;
            }

            return selectedUid;
        }

        private static bool TryResolveSelectedUidFromMusicInfo(MusicInfo musicInfo, string selectedUid, out string resolvedUid)
        {
            resolvedUid = selectedUid;
            if (musicInfo == null || string.IsNullOrEmpty(musicInfo.uid)) return false;
            if (!IsKnownVariantPair(musicInfo.uid)) return false;

            if (string.IsNullOrEmpty(selectedUid) || !IsKnownVariantPair(selectedUid))
            {
                resolvedUid = musicInfo.uid;
                return true;
            }

            var musicBaseUid = GetBaseUid(musicInfo.uid);
            var selectedBaseUid = GetBaseUid(selectedUid);
            if (string.IsNullOrEmpty(musicBaseUid) || musicBaseUid != selectedBaseUid)
            {
                return false;
            }

            if (!IsHiddenUnlockSong(musicInfo.uid)) return false;

            resolvedUid = musicInfo.uid;
            return true;
        }

        private static MusicInfo GetSelectedSlotMusicInfo(string selectedUid)
        {
            if (string.IsNullOrEmpty(selectedUid) || !IsKnownVariantPair(selectedUid)) return null;

            try
            {
                return GlobalDataBase.dbMusicTag?.GetMusicInfoFromAll(selectedUid);
            }
            catch
            {
                return null;
            }
        }

        private static string GetKnownBaseUid(string chartKey)
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
            if (!TryGetVariantPair(chartKey, out var pair))
            {
                return fallback;
            }

            var musicInfo = GetPairMusicInfo(chartKey, pair);
            if (musicInfo != null) return musicInfo;
            return fallback;
        }

        public static void ApplyVariantSelection(string chartKey)
        {
            if (!TryGetVariantPair(chartKey, out var pair)) return;

            var musicInfo = GetPairMusicInfo(chartKey, pair);
            if (musicInfo == null) return;

            var dbMusicTag = GlobalDataBase.dbMusicTag;
            if (dbMusicTag == null) return;

            try
            {
                if (_activeVariantOverride != null &&
                    (_activeVariantOverride.BaseUid != pair.BaseUid ||
                     _activeVariantOverride.HiddenUid != pair.HiddenUid))
                {
                    RestoreVariantSelectionOverride();
                }

                if (_activeVariantOverride == null)
                {
                    _activeVariantOverride = new VariantSelectionOverride
                    {
                        BaseUid = pair.BaseUid,
                        HiddenUid = pair.HiddenUid,
                        BaseInfo = string.IsNullOrEmpty(pair.BaseUid)
                            ? null
                            : dbMusicTag.GetMusicInfoFromAll(pair.BaseUid),
                        HiddenInfo = string.IsNullOrEmpty(pair.HiddenUid)
                            ? null
                            : dbMusicTag.GetMusicInfoFromAll(pair.HiddenUid)
                    };
                }

                if (!string.IsNullOrEmpty(pair.BaseUid))
                {
                    dbMusicTag.m_AllMusicInfo[pair.BaseUid] = musicInfo;
                }

                if (!string.IsNullOrEmpty(pair.HiddenUid))
                {
                    dbMusicTag.m_AllMusicInfo[pair.HiddenUid] = musicInfo;
                }
            }
            catch
            {
            }
        }

        public static void RestoreVariantSelectionOverride()
        {
            var activeOverride = _activeVariantOverride;
            if (activeOverride == null) return;

            _activeVariantOverride = null;
            var dbMusicTag = GlobalDataBase.dbMusicTag;
            if (dbMusicTag == null) return;

            try
            {
                if (!string.IsNullOrEmpty(activeOverride.BaseUid) && activeOverride.BaseInfo != null)
                {
                    dbMusicTag.m_AllMusicInfo[activeOverride.BaseUid] = activeOverride.BaseInfo;
                }

                if (!string.IsNullOrEmpty(activeOverride.HiddenUid) && activeOverride.HiddenInfo != null)
                {
                    dbMusicTag.m_AllMusicInfo[activeOverride.HiddenUid] = activeOverride.HiddenInfo;
                }
            }
            catch
            {
            }
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
            if (string.IsNullOrEmpty(chartKey)) return false;

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

        private static bool TryGetVariantPair(string chartKey, out SpecialUnlockPair pair)
        {
            if (TryGetSpecialUnlockPair(chartKey, out pair)) return true;
            return TryGetKnownPair(chartKey, out pair);
        }

        private static bool TryGetKnownPair(string chartKey, out SpecialUnlockPair pair)
        {
            pair = null;
            var baseUid = GetKnownBaseUid(chartKey);
            var hiddenUid = GetKnownHiddenUid(chartKey);
            if (string.IsNullOrEmpty(baseUid) || string.IsNullOrEmpty(hiddenUid)) return false;

            if (KnownPairCache.TryGetValue(baseUid, out pair)) return true;

            var dbMusicTag = GlobalDataBase.dbMusicTag;
            var baseInfo = dbMusicTag?.GetMusicInfoFromAll(baseUid);
            var hiddenInfo = dbMusicTag?.GetMusicInfoFromAll(hiddenUid);
            pair = new SpecialUnlockPair
            {
                BaseUid = baseUid,
                HiddenUid = hiddenUid,
                BaseInfo = baseInfo?.uid == baseUid ? baseInfo : null,
                HiddenInfo = hiddenInfo?.uid == hiddenUid ? hiddenInfo : null
            };

            if (pair.BaseInfo != null || pair.HiddenInfo != null)
            {
                KnownPairCache[baseUid] = pair;
            }

            return true;
        }

        private static MusicInfo GetPairMusicInfo(string chartKey, SpecialUnlockPair pair)
        {
            if (pair == null) return null;
            if (chartKey == pair.HiddenUid) return pair.HiddenInfo;
            if (chartKey == pair.BaseUid) return pair.BaseInfo;
            return null;
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

        private static string GetKnownHiddenUid(string chartKey)
        {
            return GetKnownBaseUid(chartKey) switch
            {
                "0-54" => "0-53",
                "0-56" => "0-55",
                "33-4" => "33-12",
                "39-0" => "39-8",
                "0-58" => "0-57",
                "0-60" => "0-59",
                _ => null
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

        private sealed class VariantSelectionOverride
        {
            public string BaseUid { get; set; }
            public string HiddenUid { get; set; }
            public MusicInfo BaseInfo { get; set; }
            public MusicInfo HiddenInfo { get; set; }
        }
    }
}
