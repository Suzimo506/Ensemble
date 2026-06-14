using Il2Cpp;
using Il2CppArcadeController.UI.Panel.PnlHome;
using Il2CppAssets.Scripts.Database;
using Il2CppAssets.Scripts.UI.Panels;
using CustomAlbums.Managers;
using MelonLoader;
using UnityEngine;
using UnityEngine.UI;

namespace MDEN.UI.Core
{
    public static class MultiplayerBattleController
    {
        private static int _startedLobbyId;
        private static ushort _startedPlaylistIndex;

        public static void OnLobbyChanged()
        {
            var lobby = Managers.LobbyManager.CurrentLobby;
            if (lobby == null || !lobby.IsPlaying)
            {
                Reset();
                Managers.BattleManager.Reset();
                return;
            }

            if (lobby.Playlist == null || lobby.Playlist.Length == 0) return;
            if (_startedLobbyId == lobby.Id && _startedPlaylistIndex == lobby.CurrentPlaylistEntry) return;

            _startedLobbyId = lobby.Id;
            _startedPlaylistIndex = lobby.CurrentPlaylistEntry;
            StartCurrentPlaylistEntry();
        }

        public static void Reset()
        {
            _startedLobbyId = 0;
            _startedPlaylistIndex = 0;
        }

        private static void StartCurrentPlaylistEntry()
        {
            var lobby = Managers.LobbyManager.CurrentLobby;
            var entryText = lobby?.Playlist?[0];
            var entry = Managers.ChartManager.ParseEntry(entryText);
            if (entry == null)
            {
                MelonLogger.Warning("Cannot start multiplayer battle: playlist entry is missing.");
                return;
            }

            var musicInfo = GetMusicInfo(entry.ChartKey);
            if (musicInfo == null)
            {
                MelonLogger.Warning($"Cannot start multiplayer battle: chart {entry.ChartKey} not found locally.");
                return;
            }

            JumpToChart(musicInfo.uid);
            GlobalDataBase.dbMusicTag.selectedDiffTglIndex = entry.Difficulty == 4 ? 3 : entry.Difficulty;
            GlobalDataBase.dbMusicTag.pnlSelectMusicUid = musicInfo.uid;
            GlobalDataBase.dbMusicTag.m_CurSelectedMusicInfo = musicInfo;
            BattleHelper.GameBattleStart(new Il2CppSystem.Object());
        }

        private static MusicInfo GetMusicInfo(string chartKey)
        {
            if (string.IsNullOrEmpty(chartKey)) return null;
            if (chartKey.Length >= 16 && !chartKey.StartsWith($"{AlbumManager.Uid}-"))
            {
                foreach (var pair in AlbumManager.LoadedAlbums)
                {
                    var album = pair.Value;
                    var sheet = GetPreferredSheet(album);
                    if (sheet != null && sheet.Md5 == chartKey)
                    {
                        return GlobalDataBase.dbMusicTag.GetMusicInfoFromAll(album.Uid);
                    }
                }
            }

            return GlobalDataBase.dbMusicTag.GetMusicInfoFromAll(chartKey);
        }

        private static CustomAlbums.Data.Sheet GetPreferredSheet(CustomAlbums.Data.Album album)
        {
            if (album == null) return null;
            if (album.Sheets.TryGetValue(2, out var sheet)) return sheet;
            if (album.Sheets.TryGetValue(3, out sheet)) return sheet;
            if (album.Sheets.TryGetValue(1, out sheet)) return sheet;
            if (album.Sheets.TryGetValue(0, out sheet)) return sheet;
            return null;
        }

        private static void JumpToChart(string uid)
        {
            try
            {
                var pnlMenu = GameObject.Find("UI/Standerd/PnlMenu")?.GetComponent<Il2CppAssets.Scripts.UI.Panels.PnlMenu>();
                if (pnlMenu != null && pnlMenu.gameObject.active)
                {
                    pnlMenu.backBtn.onClick.Invoke();
                }

                var pageHome = GameObject.Find("UI/Standerd/PnlHome")?.GetComponent<PageHome>();
                if (pageHome != null && pageHome.gameObject.active)
                {
                    pageHome.m_BtnEnter.onClick.Invoke();
                }

                var pnlPreparation = GameObject.Find("UI/Standerd/PnlPreparation");
                if (pnlPreparation != null && pnlPreparation.active)
                {
                    var back = GameObject.Find("UI/Standerd/PnlNavigation/Top/BtnNavigationBack")?.GetComponent<Button>();
                    back?.onClick.Invoke();
                }

                var stage = GameObject.Find("UI/Standerd/PnlStage")?.GetComponent<PnlStage>();
                stage?.SelectAllTagAndJumpToAssginIndex(uid);
            }
            catch (System.Exception ex)
            {
                MelonLogger.Warning($"Jump to multiplayer chart failed: {ex.Message}");
            }
        }
    }
}
