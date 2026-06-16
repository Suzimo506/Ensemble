using Il2CppAssets.Scripts.Database;
using MDEN.Managers;
using MDEN.Protocol.Messages.Lobby;
using MelonLoader;

namespace MDEN.UI.Core
{
    internal static class ChartPreviewController
    {
        private static int _previewLobbyId;
        private static string _previewEntry;

        public static void OnLobbyChanged(LobbySyncPush lobby)
        {
            if (lobby == null || !lobby.Locked || lobby.IsPlaying)
            {
                Reset();
                return;
            }

            var entry = PlaylistManager.GetCurrentPlaylistEntry();
            if (entry == null || string.IsNullOrWhiteSpace(entry.Entry))
            {
                Reset();
                return;
            }

            if (_previewLobbyId == lobby.Id && _previewEntry == entry.Entry) return;

            _previewLobbyId = lobby.Id;
            _previewEntry = entry.Entry;
            Preview(entry);
        }

        public static void Reset()
        {
            _previewLobbyId = 0;
            _previewEntry = null;
        }

        private static void Preview(PlaylistEntryViewModel entry)
        {
            var musicInfo = ChartManager.GetMusicInfo(entry.ChartKey);
            if (musicInfo == null)
            {
                MelonLogger.Warning($"Cannot preview multiplayer chart: chart {entry.ChartKey} not found locally.");
                return;
            }

            NativeChartNavigator.JumpToChart(musicInfo);
            HiddenDifficultyController.Sync(musicInfo, entry.Difficulty);
            SyncSelectedChart(musicInfo, entry.Difficulty);
        }

        private static void SyncSelectedChart(MusicInfo musicInfo, int difficulty)
        {
            GlobalDataBase.dbMusicTag.selectedDiffTglIndex = difficulty == 4 ? 3 : difficulty;
            GlobalDataBase.dbMusicTag.pnlSelectMusicUid = musicInfo.uid;
            GlobalDataBase.dbMusicTag.m_CurSelectedMusicInfo = musicInfo;
        }
    }
}
