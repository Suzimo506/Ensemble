using CustomAlbums.Data;
using CustomAlbums.Managers;
using Il2CppAssets.Scripts.Database;
using MDEN.Protocol.Rules;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;

namespace MDEN.Managers
{
    public sealed class PlaylistEntryViewModel
    {
        public string Entry { get; set; }
        public string ChartKey { get; set; }
        public int Difficulty { get; set; }
        public string OwnerName { get; set; }
        public string ChartName { get; set; }
        public string Artist { get; set; }
        public string Charter { get; set; }
        public string DisplayName => string.IsNullOrWhiteSpace(ChartName)
            ? I18nManager.Tf("chart.unknown_with_difficulty", Difficulty)
            : ChartName;
    }

    public static class ChartManager
    {
        private static bool _initialized;
        private static int _albumLoadedRefreshQueued;
        private static readonly object CustomAlbumsByMd5Lock = new object();
        private static readonly Dictionary<string, Album> CustomAlbumsByMd5 = new Dictionary<string, Album>();

        public static int CurrentDifficulty
        {
            get
            {
                var diff = GlobalDataBase.dbMusicTag.selectedDiffTglIndex;
                var musicInfo = GlobalDataBase.dbMusicTag.m_CurSelectedMusicInfo;
                var selectedUid = GlobalDataBase.dbMusicTag.pnlSelectMusicUid;
                selectedUid = SpecialChartVariantResolver.ResolveSelectedUid(musicInfo, selectedUid);
                if (SpecialChartVariantResolver.IsKnownVariantPair(selectedUid))
                {
                    musicInfo = GetMusicInfo(selectedUid) ?? musicInfo;
                }

                if (musicInfo != null && HiddenDifficultyController.IsHiddenDifficultySelected(musicInfo, diff))
                {
                    return 4;
                }

                return SpecialDifficultyController.ResolveCurrentDifficulty(musicInfo, diff);
            }
        }

        public static MusicInfo CurrentMusicInfo => GlobalDataBase.dbMusicTag.CurMusicInfo();

        public static MusicInfo CurrentSelectionMusicInfo
        {
            get
            {
                var musicInfo = CurrentMusicInfo;
                var selectedUid = GlobalDataBase.dbMusicTag?.pnlSelectMusicUid;
                return ResolveSelectionMusicInfo(musicInfo, selectedUid);
            }
        }

        public static void Initialize()
        {
            if (_initialized) return;
            _initialized = true;

            ClearCustomAlbumIndex();
            CustomAlbums.ModExtensions.Events.OnAlbumLoaded += OnAlbumLoaded;
            TrySubscribeOptionalAlbumEvent("OnAlbumRemoved", new CustomAlbums.ModExtensions.Events.LoadAlbumEvent(OnAlbumRemoved));
        }

        public static string GetCurrentEntry()
        {
            var musicInfo = CurrentMusicInfo;
            if (musicInfo == null) return null;

            var selectedUid = GlobalDataBase.dbMusicTag?.pnlSelectMusicUid;
            selectedUid = SpecialChartVariantResolver.ResolveSelectedUid(musicInfo, selectedUid);
            if (IsSpecialUnlockSelection(musicInfo, selectedUid))
            {
                var selectedMusicInfo = ResolveSelectionMusicInfo(musicInfo, selectedUid);
                var difficulty = CurrentDifficulty;
                if (!IsNativeDifficultyAvailable(selectedMusicInfo, difficulty)) return null;

                return GetEntry(selectedUid, selectedMusicInfo, difficulty);
            }

            return GetEntry(musicInfo, CurrentDifficulty);
        }

        public static string GetEntry(MusicInfo musicInfo, int difficulty)
        {
            var entryKey = GetEntryKey(musicInfo, difficulty);
            if (string.IsNullOrWhiteSpace(entryKey)) return null;

            return BuildEntry(entryKey, musicInfo, difficulty);
        }

        public static string GetEntry(string chartKey, MusicInfo musicInfo, int difficulty)
        {
            if (string.IsNullOrWhiteSpace(chartKey)) return null;

            var entryKey = GetEntryKey(chartKey);
            if (string.IsNullOrWhiteSpace(entryKey)) return null;

            return BuildEntry(entryKey, musicInfo, difficulty);
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
                TryGetCachedCustomAlbum(md5, out var album) &&
                IsAlbumAvailable(album))
            {
                return album.Uid;
            }

            var fallbackAlbum = AlbumManager.GetByUid(musicInfo.uid);
            return fallbackAlbum?.Uid ?? musicInfo.uid;
        }

        public static bool IsCurrentSelectionPlayableDifficulty(MusicInfo musicInfo, int difficulty)
        {
            if (!DifficultyDisplayRules.IsKnownDifficulty(difficulty)) return false;

            var nativeDifficulty = GlobalDataBase.dbMusicTag?.selectedDiffTglIndex ?? 0;
            if (difficulty == DifficultyDisplayRules.Hidden)
            {
                return HiddenDifficultyController.IsHiddenDifficultySelected(musicInfo, nativeDifficulty);
            }

            if (difficulty == DifficultyDisplayRules.Spell)
            {
                return SpecialDifficultyController.IsSpecialDifficultySelected(musicInfo, nativeDifficulty);
            }

            return IsNativeDifficultyAvailable(musicInfo, difficulty);
        }

        public static bool TryGetCurrentReadyDifficultyForEntry(PlaylistEntryViewModel entry, out int difficulty)
        {
            difficulty = 0;
            if (entry == null || !IsCurrentSelectedChart(entry)) return false;

            difficulty = CurrentDifficulty;
            return IsEntryDifficultyPlayable(entry, difficulty) &&
                   IsCurrentSelectionPlayableDifficulty(CurrentSelectionMusicInfo, difficulty);
        }

        public static bool IsEntryDifficultyPlayable(PlaylistEntryViewModel entry, int difficulty)
        {
            return entry != null && IsEntryDifficultyPlayable(entry.ChartKey, difficulty);
        }

        public static bool IsEntryDifficultyPlayable(string chartKey, int difficulty)
        {
            return IsMusicInfoDifficultyPlayable(GetMusicInfo(chartKey), difficulty);
        }

        public static PlaylistEntryViewModel ParseEntry(string entry)
        {
            if (string.IsNullOrWhiteSpace(entry)) return null;

            var parts = SplitEntry(entry);
            if (parts.Length < 2 || !int.TryParse(parts[1], out var difficulty))
            {
                return new PlaylistEntryViewModel
                {
                    Entry = entry,
                    ChartKey = entry,
                    Difficulty = 0,
                    OwnerName = I18nManager.T("common.unknown"),
                    ChartName = entry
                };
            }

            var chartName = parts.Length > 3 ? CleanEntryChartName(DecodeEntryPart(parts[3])) : null;
            var parsed = new PlaylistEntryViewModel
            {
                Entry = entry,
                ChartKey = parts[0],
                Difficulty = difficulty,
                OwnerName = parts.Length > 2 && !string.IsNullOrWhiteSpace(parts[2]) ? DecodeEntryPart(parts[2]) : I18nManager.T("common.unknown"),
                ChartName = !string.IsNullOrWhiteSpace(chartName)
                    ? chartName
                    : I18nManager.Tf("chart.unknown_with_difficulty", difficulty),
                Artist = parts.Length > 4 ? DecodeEntryPart(parts[4]) : null,
                Charter = parts.Length > 5 ? DecodeEntryPart(parts[5]) : null
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

        public static bool IsCurrentSelectedChart(PlaylistEntryViewModel entry)
        {
            if (entry == null) return false;

            var musicInfo = CurrentMusicInfo;
            var selectedUid = GlobalDataBase.dbMusicTag?.pnlSelectMusicUid;
            selectedUid = SpecialChartVariantResolver.ResolveSelectedUid(musicInfo, selectedUid);
            if (SpecialChartVariantResolver.IsKnownVariantPair(entry.ChartKey) ||
                SpecialChartVariantResolver.IsKnownVariantPair(selectedUid))
            {
                return selectedUid == entry.ChartKey &&
                       IsSpecialUnlockSelection(musicInfo, selectedUid);
            }

            if (selectedUid == entry.ChartKey && IsSpecialUnlockSelection(musicInfo, selectedUid))
            {
                return true;
            }

            var currentKey = GetEntryKey(musicInfo, entry.Difficulty);
            return !string.IsNullOrWhiteSpace(currentKey) &&
                   currentKey == entry.ChartKey;
        }

        public static bool IsCurrentSelectionLockedByPlaylistVariant(PlaylistEntryViewModel entry)
        {
            if (entry == null || !SpecialChartVariantResolver.IsKnownVariantPair(entry.ChartKey)) return false;

            var selectedUid = GlobalDataBase.dbMusicTag?.pnlSelectMusicUid;
            if (string.IsNullOrEmpty(selectedUid) || selectedUid == entry.ChartKey) return false;
            if (!SpecialChartVariantResolver.IsKnownVariantPair(selectedUid)) return false;

            return SpecialChartVariantResolver.GetBaseUid(selectedUid) ==
                   SpecialChartVariantResolver.GetBaseUid(entry.ChartKey);
        }

        private static MusicInfo ResolveSelectionMusicInfo(MusicInfo musicInfo, string selectedUid)
        {
            return SpecialChartVariantResolver.IsKnownVariantPair(selectedUid)
                ? GetMusicInfo(selectedUid) ?? musicInfo
                : musicInfo;
        }

        private static bool IsSpecialUnlockSelection(MusicInfo musicInfo, string selectedUid)
        {
            if (musicInfo == null || string.IsNullOrEmpty(selectedUid)) return false;
            if (!SpecialChartVariantResolver.IsKnownVariantPair(selectedUid)) return false;

            var currentUid = musicInfo.uid;
            return currentUid == selectedUid ||
                   SpecialChartVariantResolver.GetBaseUid(currentUid) == SpecialChartVariantResolver.GetBaseUid(selectedUid);
        }

        private static bool IsMusicInfoDifficultyPlayable(MusicInfo musicInfo, int difficulty)
        {
            if (!DifficultyDisplayRules.IsKnownDifficulty(difficulty)) return false;
            if (difficulty == DifficultyDisplayRules.Spell)
            {
                return SpecialDifficultyController.HasSpecialDifficulty(musicInfo);
            }

            if (difficulty == DifficultyDisplayRules.Hidden)
            {
                return HiddenDifficultyController.HasHiddenDifficulty(musicInfo);
            }

            return IsNativeDifficultyAvailable(musicInfo, difficulty);
        }

        private static bool IsNativeDifficultyAvailable(MusicInfo musicInfo, int difficulty)
        {
            return musicInfo != null &&
                   DifficultyDisplayRules.IsKnownDifficulty(difficulty) &&
                   musicInfo.GetDifficulty(difficulty) > 0;
        }

        public static string GetCustomChartMd5(string uid)
        {
            return GetMd5(uid);
        }

        public static void CacheCustomChartMd5s(IEnumerable<KeyValuePair<string, Album>> entries)
        {
            if (entries == null) return;

            foreach (var entry in entries)
            {
                if (string.IsNullOrEmpty(entry.Key) || entry.Value == null) continue;
                CacheCustomChartMd5(entry.Key, entry.Value);
            }
        }

        public static IEnumerable<string> GetCustomChartMd5s(string uid)
        {
            var album = AlbumManager.GetByUid(uid);
            if (!IsAlbumAvailable(album) || album.Sheets == null) yield break;

            foreach (var sheet in album.Sheets.Values)
            {
                if (sheet == null) continue;

                var md5 = sheet.Md5;
                if (string.IsNullOrEmpty(md5)) continue;

                CacheCustomChartMd5(md5, album);
                yield return md5;
            }
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
                if (TryGetCachedCustomAlbum(chartKey, out var cachedAlbum))
                {
                    if (IsAlbumAvailable(cachedAlbum) && AlbumManager.GetByUid(cachedAlbum.Uid) != null)
                    {
                        return GlobalDataBase.dbMusicTag.GetMusicInfoFromAll(cachedAlbum.Uid);
                    }

                    RemoveCachedCustomAlbum(chartKey);
                }

                foreach (var pair in AlbumManager.LoadedAlbums)
                {
                    var album = pair.Value;
                    if (!IsAlbumAvailable(album)) continue;

                    if (AlbumHasSheetMd5(album, chartKey))
                    {
                        CacheCustomChartMd5(chartKey, album);
                        return GlobalDataBase.dbMusicTag.GetMusicInfoFromAll(album.Uid);
                    }
                }

                return null;
            }

            return SpecialChartVariantResolver.ResolveMusicInfo(
                chartKey,
                GlobalDataBase.dbMusicTag.GetMusicInfoFromAll(chartKey));
        }

        private static string GetNiceChartName(MusicInfo musicInfo, int difficulty)
        {
            if (musicInfo == null) return I18nManager.Tf("chart.unknown_with_difficulty", difficulty);

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

        private static string BuildEntry(string entryKey, MusicInfo musicInfo, int difficulty)
        {
            var chartName = GetNiceChartName(musicInfo, difficulty);
            var artist = GetBestChartArtist(musicInfo);
            var charter = GetBestChartCharter(musicInfo, difficulty);
            var entry = $"{entryKey}#{difficulty}#{EncodeEntryPart(GetLocalPlayerName())}#{EncodeEntryPart(chartName)}";

            return string.IsNullOrWhiteSpace(artist) && string.IsNullOrWhiteSpace(charter)
                ? entry
                : $"{entry}#{EncodeEntryPart(artist)}#{EncodeEntryPart(charter)}";
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
                   chartName.TrimStart().StartsWith("Unknown Chart", System.StringComparison.OrdinalIgnoreCase) ||
                   chartName.TrimStart().StartsWith(I18nManager.T("chart.unknown"), System.StringComparison.OrdinalIgnoreCase);
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

            return I18nManager.T("chart.unknown");
        }

        private static string GetBestChartArtist(MusicInfo musicInfo)
        {
            var album = GetCustomAlbum(musicInfo);
            if (!string.IsNullOrWhiteSpace(album?.Info?.Author))
            {
                return album.Info.Author.Trim();
            }

            return GetMusicInfoString(musicInfo, "author", "musicAuthor", "composer");
        }

        private static string GetBestChartCharter(MusicInfo musicInfo, int difficulty)
        {
            if (musicInfo == null) return string.Empty;

            var value = difficulty switch
            {
                1 => musicInfo.levelDesigner1 ?? musicInfo.levelDesigner,
                2 => musicInfo.levelDesigner2 ?? musicInfo.levelDesigner,
                3 => musicInfo.levelDesigner3 ?? musicInfo.levelDesigner,
                4 => musicInfo.levelDesigner4 ?? musicInfo.levelDesigner,
                5 => musicInfo.levelDesigner5 ?? musicInfo.levelDesigner,
                _ => musicInfo.levelDesigner
            };

            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        }

        private static string GetMusicInfoString(MusicInfo musicInfo, params string[] names)
        {
            if (musicInfo == null) return string.Empty;

            var type = musicInfo.GetType();
            foreach (var name in names)
            {
                var field = type.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.IgnoreCase);
                if (field?.GetValue(musicInfo) is string fieldValue && !string.IsNullOrWhiteSpace(fieldValue))
                {
                    return fieldValue.Trim();
                }

                var property = type.GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.IgnoreCase);
                if (property?.GetValue(musicInfo) is string propertyValue && !string.IsNullOrWhiteSpace(propertyValue))
                {
                    return propertyValue.Trim();
                }
            }

            return string.Empty;
        }

        private static Album GetCustomAlbum(MusicInfo musicInfo)
        {
            if (musicInfo == null || string.IsNullOrWhiteSpace(musicInfo.uid)) return null;

            var album = AlbumManager.GetByUid(musicInfo.uid);
            if (IsAlbumAvailable(album)) return album;

            var md5 = GetMd5(musicInfo.uid);
            if (!string.IsNullOrEmpty(md5) &&
                TryGetCachedCustomAlbum(md5, out album) &&
                IsAlbumAvailable(album))
            {
                return album;
            }

            return null;
        }

        private static string GetEntryKey(MusicInfo musicInfo)
        {
            var md5 = GetMd5(musicInfo, 0);
            if (musicInfo?.albumIndex == AlbumManager.Uid) return md5;
            return md5 ?? musicInfo?.uid;
        }

        private static string GetEntryKey(MusicInfo musicInfo, int difficulty)
        {
            var md5 = GetMd5(musicInfo, difficulty);
            if (musicInfo?.albumIndex == AlbumManager.Uid) return md5;
            return md5 ?? musicInfo?.uid;
        }

        private static string GetMd5(MusicInfo musicInfo, int difficulty)
        {
            if (musicInfo == null || musicInfo.albumIndex != AlbumManager.Uid) return null;
            return GetMd5(musicInfo.uid, difficulty);
        }

        private static string GetMd5(string uid)
        {
            return GetMd5(uid, 0);
        }

        private static string GetMd5(string uid, int difficulty)
        {
            if (string.IsNullOrEmpty(uid) || !uid.StartsWith(AlbumManager.Uid.ToString())) return null;

            Album album = AlbumManager.GetByUid(uid);
            if (!IsAlbumAvailable(album)) return null;

            var sheet = GetSheet(album, difficulty);
            if (sheet != null)
            {
                CacheCustomChartMd5(sheet.Md5, album);
            }

            return sheet?.Md5;
        }

        private static Sheet GetSheet(Album album, int difficulty)
        {
            if (album == null) return null;
            if (difficulty > 0 && album.Sheets.TryGetValue(difficulty, out var sheet)) return sheet;
            return GetPreferredSheet(album);
        }

        private static Sheet GetPreferredSheet(Album album)
        {
            if (album == null) return null;
            if (album.Sheets.TryGetValue(2, out var sheet)) return sheet;
            if (album.Sheets.TryGetValue(3, out sheet)) return sheet;
            if (album.Sheets.TryGetValue(1, out sheet)) return sheet;
            if (album.Sheets.TryGetValue(4, out sheet)) return sheet;
            if (album.Sheets.TryGetValue(5, out sheet)) return sheet;
            if (album.Sheets.TryGetValue(0, out sheet)) return sheet;
            return null;
        }

        private static bool AlbumHasSheetMd5(Album album, string md5)
        {
            if (!IsAlbumAvailable(album) || album.Sheets == null || string.IsNullOrEmpty(md5)) return false;

            foreach (var sheet in album.Sheets.Values)
            {
                if (sheet == null) continue;
                if (sheet.Md5 != md5) continue;

                CacheCustomChartMd5(md5, album);
                return true;
            }

            return false;
        }

        private static string GetLocalPlayerName()
        {
            return PlayerManager.CurrentProfile?.Name ?? PlayerManager.CurrentUid ?? I18nManager.T("common.unknown");
        }

        private static bool IsCustomChartKey(string chartKey)
        {
            return ChartSelectionRules.IsCustomChartKey(chartKey);
        }

        private static string EncodeEntryPart(string value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            return "__mden_uri__" + Uri.EscapeDataString(value);
        }

        private static string DecodeEntryPart(string value)
        {
            if (string.IsNullOrEmpty(value)) return value;
            const string prefix = "__mden_uri__";
            if (!value.StartsWith(prefix, StringComparison.Ordinal)) return value;

            try
            {
                return Uri.UnescapeDataString(value.Substring(prefix.Length));
            }
            catch
            {
                return value;
            }
        }

        private static string CleanEntryChartName(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return value;

            var metadataIndex = value.IndexOf("#__mden_uri__", StringComparison.Ordinal);
            return metadataIndex >= 0
                ? value.Substring(0, metadataIndex).TrimEnd()
                : value;
        }

        private static string[] SplitEntry(string entry)
        {
            return entry.Split('#');
        }

        private static void OnAlbumLoaded(object sender, CustomAlbums.ModExtensions.AlbumEventArgs e)
        {
            QueueAlbumLoadedRefresh();
        }

        private static void OnAlbumRemoved(object sender, CustomAlbums.ModExtensions.AlbumEventArgs e)
        {
            QueueAlbumLoadedRefresh();
        }

        private static void TrySubscribeOptionalAlbumEvent(string eventName, Delegate handler)
        {
            try
            {
                var eventInfo = typeof(CustomAlbums.ModExtensions.Events).GetEvent(
                    eventName,
                    BindingFlags.Public | BindingFlags.Static);
                if (eventInfo == null)
                {
                    ClientLogManager.Msg($"CustomAlbums event {eventName} is unavailable; album index will refresh on album load.");
                    return;
                }

                eventInfo.AddEventHandler(null, handler);
            }
            catch (Exception ex)
            {
                ClientLogManager.Warning($"Subscribe CustomAlbums event {eventName} failed: {ex.Message}");
            }
        }

        private static void QueueAlbumLoadedRefresh()
        {
            if (Interlocked.Exchange(ref _albumLoadedRefreshQueued, 1) == 1)
            {
                return;
            }

            MDEN.UI.Core.MainThreadDispatcher.Enqueue(() =>
            {
                Interlocked.Exchange(ref _albumLoadedRefreshQueued, 0);
                ClearCustomAlbumIndex();
                PlayerManager.InvalidateChartStateCache();
                PlayerManager.SyncChartStateFireAndForget(true);
                MDEN.UI.Core.ChartPreviewController.RetryCurrentPreviewAfterChartRefresh();
            });
        }

        private static void ClearCustomAlbumIndex()
        {
            lock (CustomAlbumsByMd5Lock)
            {
                CustomAlbumsByMd5.Clear();
            }
        }

        private static void CacheCustomChartMd5(string md5, Album album)
        {
            if (string.IsNullOrEmpty(md5) || album == null) return;

            lock (CustomAlbumsByMd5Lock)
            {
                CustomAlbumsByMd5[md5] = album;
            }
        }

        private static bool TryGetCachedCustomAlbum(string md5, out Album album)
        {
            lock (CustomAlbumsByMd5Lock)
            {
                return CustomAlbumsByMd5.TryGetValue(md5, out album);
            }
        }

        private static void RemoveCachedCustomAlbum(string md5)
        {
            lock (CustomAlbumsByMd5Lock)
            {
                CustomAlbumsByMd5.Remove(md5);
            }
        }

        private static bool IsAlbumAvailable(Album album)
        {
            if (album == null || string.IsNullOrWhiteSpace(album.Path)) return false;
            return album.IsPackaged ? File.Exists(album.Path) : Directory.Exists(album.Path);
        }
    }
}

