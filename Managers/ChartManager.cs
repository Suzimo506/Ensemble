using CustomAlbums.Data;
using CustomAlbums.Managers;
using Il2CppAssets.Scripts.Database;
using System.Collections.Generic;
using System.Linq;

namespace MDEN.Managers
{
    public sealed class PlaylistEntryViewModel
    {
        public string Entry { get; set; }
        public string ChartKey { get; set; }
        public int Difficulty { get; set; }
        public string OwnerName { get; set; }
        public string ChartName { get; set; }
        public string DisplayName => string.IsNullOrWhiteSpace(ChartName)
            ? $"Unknown Chart {Difficulty}"
            : ChartName;
    }

    public static class ChartManager
    {
        private static bool _initialized;
        private static readonly Dictionary<string, Album> CustomAlbumsByMd5 = new Dictionary<string, Album>();

        public static int CurrentDifficulty
        {
            get
            {
                var diff = GlobalDataBase.dbMusicTag.selectedDiffTglIndex;
                if (diff == 3)
                {
                    var musicInfo = GlobalDataBase.dbMusicTag.m_CurSelectedMusicInfo;
                    if (musicInfo != null)
                    {
                        var checkUid = GetHiddenCheckUid(musicInfo);
                        if (string.IsNullOrEmpty(checkUid)) return diff;

                        var specialSongManager = Il2CppAssets.Scripts.PeroTools.Commons.Singleton<Il2Cpp.SpecialSongManager>.instance;
                        if (specialSongManager != null && specialSongManager.IsInvokeHideBms(checkUid))
                        {
                            return 4;
                        }
                    }
                }

                return diff;
            }
        }

        public static MusicInfo CurrentMusicInfo => GlobalDataBase.dbMusicTag.CurMusicInfo();

        public static void Initialize()
        {
            if (_initialized) return;
            _initialized = true;

            RebuildCustomAlbumIndex();
            CustomAlbums.ModExtensions.Events.OnAlbumLoaded += OnAlbumLoaded;
        }

        public static string GetCurrentEntry()
        {
            var musicInfo = CurrentMusicInfo;
            if (musicInfo == null) return null;
            return GetEntry(musicInfo, CurrentDifficulty);
        }

        public static string GetEntry(MusicInfo musicInfo, int difficulty)
        {
            return $"{GetEntryKey(musicInfo)}#{difficulty}#{GetLocalPlayerName()}#{GetNiceChartName(musicInfo, difficulty)}";
        }

        public static string GetHiddenCheckUid(MusicInfo musicInfo)
        {
            if (musicInfo == null || string.IsNullOrWhiteSpace(musicInfo.uid))
            {
                return musicInfo?.uid;
            }

            if (!musicInfo.uid.StartsWith("999-"))
            {
                return musicInfo.uid;
            }

            var md5 = GetMd5(musicInfo.uid);
            if (!string.IsNullOrEmpty(md5) &&
                CustomAlbumsByMd5.TryGetValue(md5, out var album) &&
                album != null)
            {
                return album.Uid;
            }

            var fallbackAlbum = AlbumManager.GetByUid(musicInfo.uid);
            return fallbackAlbum?.Uid ?? musicInfo.uid;
        }

        public static PlaylistEntryViewModel ParseEntry(string entry)
        {
            if (string.IsNullOrWhiteSpace(entry)) return null;

            var parts = entry.Split('#');
            if (parts.Length < 2 || !int.TryParse(parts[1], out var difficulty))
            {
                return new PlaylistEntryViewModel
                {
                    Entry = entry,
                    ChartKey = entry,
                    Difficulty = 0,
                    OwnerName = "Unknown",
                    ChartName = entry
                };
            }

            var chartName = parts.Length > 3 ? string.Join("#", parts, 3, parts.Length - 3) : null;
            var parsed = new PlaylistEntryViewModel
            {
                Entry = entry,
                ChartKey = parts[0],
                Difficulty = difficulty,
                OwnerName = parts.Length > 2 && !string.IsNullOrWhiteSpace(parts[2]) ? parts[2] : "Unknown",
                ChartName = !string.IsNullOrWhiteSpace(chartName)
                    ? chartName
                    : $"Unknown Chart {difficulty}"
            };
            TryRepairUnknownChartName(parsed);
            return parsed;
        }

        public static bool IsSameChart(string leftEntry, string rightEntry)
        {
            var left = ParseEntry(leftEntry);
            var right = ParseEntry(rightEntry);
            return left != null &&
                right != null &&
                left.ChartKey == right.ChartKey &&
                left.Difficulty == right.Difficulty;
        }

        public static string GetCustomChartMd5(string uid)
        {
            return GetMd5(uid);
        }

        public static string GetEntryKey(string uid)
        {
            var md5 = GetMd5(uid);
            return md5 ?? uid;
        }

        public static MusicInfo GetMusicInfo(string chartKey)
        {
            if (string.IsNullOrEmpty(chartKey)) return null;

            if (IsCustomChartKey(chartKey))
            {
                if (CustomAlbumsByMd5.TryGetValue(chartKey, out var cachedAlbum))
                {
                    if (AlbumManager.GetByUid(cachedAlbum.Uid) != null)
                    {
                        return GlobalDataBase.dbMusicTag.GetMusicInfoFromAll(cachedAlbum.Uid);
                    }

                    CustomAlbumsByMd5.Remove(chartKey);
                }

                foreach (var pair in AlbumManager.LoadedAlbums)
                {
                    var album = pair.Value;
                    var sheet = GetPreferredSheet(album);
                    if (sheet != null && sheet.Md5 == chartKey)
                    {
                        CustomAlbumsByMd5[chartKey] = album;
                        return GlobalDataBase.dbMusicTag.GetMusicInfoFromAll(album.Uid);
                    }
                }

                return null;
            }

            return GlobalDataBase.dbMusicTag.GetMusicInfoFromAll(chartKey);
        }

        private static string GetNiceChartName(MusicInfo musicInfo, int difficulty)
        {
            if (musicInfo == null) return $"Unknown Chart {difficulty}";

            var level = musicInfo.GetMusicLevelStringByDiff(difficulty);
            var album = GetCustomAlbum(musicInfo);
            if (difficulty == 4 && musicInfo.uid.StartsWith($"{AlbumManager.Uid}-"))
            {
                if (album != null && !string.IsNullOrEmpty(album.Info.HideBmsDifficulty) && album.Info.HideBmsDifficulty != "0")
                {
                    level = album.Info.HideBmsDifficulty;
                }
            }

            var chartName = GetBestChartName(musicInfo, album);
            if (!musicInfo.uid.StartsWith("999-"))
            {
                try
                {
                    var local = musicInfo.GetLocal(0);
                    if (local != null && !string.IsNullOrEmpty(local.name))
                    {
                        chartName = local.name;
                    }
                }
                catch
                {
                    chartName = musicInfo.name;
                }
            }

            return $"{chartName} {level}★";
        }

        private static void TryRepairUnknownChartName(PlaylistEntryViewModel entry)
        {
            if (entry == null || !IsUnknownChartName(entry.ChartName)) return;

            var musicInfo = GetMusicInfo(entry.ChartKey);
            if (musicInfo == null) return;

            var repaired = GetNiceChartName(musicInfo, entry.Difficulty);
            if (!IsUnknownChartName(repaired))
            {
                entry.ChartName = repaired;
            }
        }

        private static bool IsUnknownChartName(string chartName)
        {
            return string.IsNullOrWhiteSpace(chartName) ||
                   chartName.TrimStart().StartsWith("Unknown Chart", System.StringComparison.OrdinalIgnoreCase);
        }

        private static string GetBestChartName(MusicInfo musicInfo, Album album)
        {
            if (!string.IsNullOrWhiteSpace(album?.Info?.Name))
            {
                return album.Info.Name;
            }

            if (!string.IsNullOrWhiteSpace(musicInfo?.name))
            {
                return musicInfo.name;
            }

            return "Unknown Chart";
        }

        private static Album GetCustomAlbum(MusicInfo musicInfo)
        {
            if (musicInfo == null || string.IsNullOrWhiteSpace(musicInfo.uid)) return null;

            var album = AlbumManager.GetByUid(musicInfo.uid);
            if (album != null) return album;

            var md5 = GetMd5(musicInfo.uid);
            if (!string.IsNullOrEmpty(md5) && CustomAlbumsByMd5.TryGetValue(md5, out album))
            {
                return album;
            }

            return null;
        }

        private static string GetEntryKey(MusicInfo musicInfo)
        {
            var md5 = GetMd5(musicInfo);
            return md5 ?? musicInfo?.uid;
        }

        private static string GetMd5(MusicInfo musicInfo)
        {
            if (musicInfo == null || musicInfo.albumIndex != AlbumManager.Uid) return null;
            return GetMd5(musicInfo.uid);
        }

        private static string GetMd5(string uid)
        {
            if (string.IsNullOrEmpty(uid) || !uid.StartsWith(AlbumManager.Uid.ToString())) return null;

            Album album = AlbumManager.GetByUid(uid);
            var sheet = GetPreferredSheet(album);
            if (sheet != null)
            {
                CustomAlbumsByMd5[sheet.Md5] = album;
            }

            return sheet?.Md5;
        }

        private static Sheet GetPreferredSheet(Album album)
        {
            if (album == null) return null;
            if (album.Sheets.TryGetValue(2, out var sheet)) return sheet;
            if (album.Sheets.TryGetValue(3, out sheet)) return sheet;
            if (album.Sheets.TryGetValue(1, out sheet)) return sheet;
            if (album.Sheets.TryGetValue(0, out sheet)) return sheet;
            return null;
        }

        private static string GetLocalPlayerName()
        {
            return PlayerManager.CurrentProfile?.Name ?? PlayerManager.CurrentUid ?? "Unknown";
        }

        private static bool IsCustomChartKey(string chartKey)
        {
            return !string.IsNullOrWhiteSpace(chartKey) && chartKey.Length >= 16 && !chartKey.StartsWith($"{AlbumManager.Uid}-");
        }

        private static void OnAlbumLoaded(object sender, CustomAlbums.ModExtensions.AlbumEventArgs e)
        {
            if (e?.Album == null) return;

            var oldKeys = CustomAlbumsByMd5
                .Where(pair => pair.Value != null && pair.Value.AlbumName == e.Album.AlbumName)
                .Select(pair => pair.Key)
                .ToArray();
            foreach (var key in oldKeys)
            {
                CustomAlbumsByMd5.Remove(key);
            }

            var sheet = GetPreferredSheet(e.Album);
            if (sheet != null)
            {
                CustomAlbumsByMd5[sheet.Md5] = e.Album;
            }

            PlayerManager.SyncChartStateFireAndForget();
        }

        private static void RebuildCustomAlbumIndex()
        {
            CustomAlbumsByMd5.Clear();
            foreach (var pair in AlbumManager.LoadedAlbums)
            {
                var album = pair.Value;
                var sheet = GetPreferredSheet(album);
                if (sheet != null)
                {
                    CustomAlbumsByMd5[sheet.Md5] = album;
                }
            }
        }
    }
}

