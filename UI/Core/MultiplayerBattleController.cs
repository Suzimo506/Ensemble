using Il2Cpp;
using Il2CppAssets.Scripts.Database;
using MelonLoader;

namespace MDEN.UI.Core
{
    public static class MultiplayerBattleController
    {
        private static int _startedLobbyId;
        private static string _startedBattleEntry;

        public static void OnLobbyChanged()
        {
            var lobby = Managers.LobbyManager.CurrentLobby;
            if (lobby == null || !lobby.IsPlaying)
            {
                Reset();
                Managers.BattleManager.Reset();
                return;
            }

            if (lobby.ReadyPlayers == null ||
                System.Array.IndexOf(lobby.ReadyPlayers, Managers.PlayerManager.CurrentUid) < 0)
            {
                return;
            }

            var battleEntry = !string.IsNullOrWhiteSpace(lobby.CurrentBattleEntry)
                ? lobby.CurrentBattleEntry
                : (lobby.Playlist != null && lobby.Playlist.Length > 0 ? lobby.Playlist[0] : null);
            if (string.IsNullOrWhiteSpace(battleEntry)) return;
            if (_startedLobbyId == lobby.Id && _startedBattleEntry == battleEntry) return;

            _startedLobbyId = lobby.Id;
            _startedBattleEntry = battleEntry;
            StartCurrentPlaylistEntry(battleEntry);
        }

        public static void Reset()
        {
            _startedLobbyId = 0;
            _startedBattleEntry = null;
        }

        private static void StartCurrentPlaylistEntry(string entryText)
        {
            var entry = Managers.ChartManager.ParseEntry(entryText);
            if (entry == null)
            {
                MelonLogger.Warning("Cannot start multiplayer battle: playlist entry is missing.");
                return;
            }

            var musicInfo = Managers.ChartManager.GetMusicInfo(entry.ChartKey);
            if (musicInfo == null)
            {
                MelonLogger.Warning($"Cannot start multiplayer battle: chart {entry.ChartKey} not found locally.");
                return;
            }

            NativeChartNavigator.JumpToChart(musicInfo);
            HiddenDifficultyController.Sync(musicInfo, entry.Difficulty);
            GlobalDataBase.dbMusicTag.selectedDiffTglIndex = entry.Difficulty == 4 ? 3 : entry.Difficulty;
            GlobalDataBase.dbMusicTag.pnlSelectMusicUid = musicInfo.uid;
            GlobalDataBase.dbMusicTag.m_CurSelectedMusicInfo = musicInfo;
            BattleHelper.GameBattleStart(new Il2CppSystem.Object());
        }
    }
}

