using CustomAlbums.Data;
using CustomAlbums.Managers;
using Il2CppAssets.Scripts.Database;

namespace MDEN.Managers
{
    public sealed class PlaylistEntryViewModel
    {
        public string Entry { get; set; }
        public string ChartKey { get; set; }
        public int Difficulty { get; set; }
        public string OwnerName { get; set; }
        public string ChartName { get; set; }
        public string DisplayName => string.IsNullOrWhiteSpace(ChartName) ? $"Unknown Chart {Difficulty}" : ChartName;
    }

    public static class ChartManager
    {
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
                        var checkUid = musicInfo.uid;
                        if (checkUid.StartsWith("999-"))
                        {
                            var md5 = GetMd5(checkUid);
                            if (md5 != null)
                            {
                                checkUid = $"{AlbumManager.Uid}-{md5}";
                            }
                        }

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

            return new PlaylistEntryViewModel
            {
                Entry = entry,
                ChartKey = parts[0],
                Difficulty = difficulty,
                OwnerName = parts.Length > 2 && !string.IsNullOrWhiteSpace(parts[2]) ? parts[2] : "Unknown",
                ChartName = parts.Length > 3 && !string.IsNullOrWhiteSpace(parts[3])
                    ? string.Join("#", parts, 3, parts.Length - 3)
                    : $"Unknown Chart {difficulty}"
            };
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

        private static string GetNiceChartName(MusicInfo musicInfo, int difficulty)
        {
            if (musicInfo == null) return $"Unknown Chart {difficulty}";

            var level = musicInfo.GetMusicLevelStringByDiff(difficulty);
            if (difficulty == 4 && musicInfo.uid.StartsWith($"{AlbumManager.Uid}-"))
            {
                var album = AlbumManager.GetByUid(musicInfo.uid);
                if (album != null && !string.IsNullOrEmpty(album.Info.HideBmsDifficulty) && album.Info.HideBmsDifficulty != "0")
                {
                    level = album.Info.HideBmsDifficulty;
                }
            }

            var chartName = musicInfo.name;
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
    }
}
