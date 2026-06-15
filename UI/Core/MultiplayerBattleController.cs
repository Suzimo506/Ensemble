using Il2Cpp;
using Il2CppArcadeController.UI.Panel.PnlHome;
using Il2CppAssets.Scripts.Database;
using Il2CppAssets.Scripts.PeroTools.Commons;
using Il2CppAssets.Scripts.UI.Panels;
using MelonLoader;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

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

            JumpToChart(musicInfo.uid);
            SyncHiddenDifficulty(musicInfo, entry.Difficulty);
            GlobalDataBase.dbMusicTag.selectedDiffTglIndex = entry.Difficulty == 4 ? 3 : entry.Difficulty;
            GlobalDataBase.dbMusicTag.pnlSelectMusicUid = musicInfo.uid;
            GlobalDataBase.dbMusicTag.m_CurSelectedMusicInfo = musicInfo;
            BattleHelper.GameBattleStart(new Il2CppSystem.Object());
        }

        private static void SyncHiddenDifficulty(MusicInfo musicInfo, int difficulty)
        {
            if (musicInfo == null) return;

            var specialSongManager = Singleton<SpecialSongManager>.instance;
            if (specialSongManager == null) return;

            var checkUid = Managers.ChartManager.GetHiddenCheckUid(musicInfo);
            if (string.IsNullOrEmpty(checkUid)) return;

            var hiddenUnlocked = specialSongManager.IsInvokeHideBms(checkUid);

            if (difficulty == 4 && !hiddenUnlocked)
            {
                specialSongManager.InvokeHideBms(musicInfo, true);
                ActivateCustomAlbumHiddenIfNeeded(musicInfo, checkUid, specialSongManager);
            }
            else if (difficulty == 3 && hiddenUnlocked)
            {
                if (specialSongManager.m_IsInvokeHideDic.ContainsKey(checkUid))
                {
                    specialSongManager.m_IsInvokeHideDic.Remove(checkUid);
                }
            }
        }

        private static void ActivateCustomAlbumHiddenIfNeeded(
            MusicInfo musicInfo,
            string checkUid,
            SpecialSongManager specialSongManager)
        {
            if (musicInfo == null || !musicInfo.uid.StartsWith("999-")) return;

            try
            {
                var customAlbumsAssembly = System.AppDomain.CurrentDomain
                    .GetAssemblies()
                    .FirstOrDefault(assembly => assembly.GetName().Name == "CustomAlbums");
                var patchType = customAlbumsAssembly?.GetType("CustomAlbums.Patches.HiddenSupportPatch+InvokeHideBmsPatch");
                var activateHiddenMethod = patchType?.GetMethod(
                    "ActivateHidden",
                    BindingFlags.NonPublic | BindingFlags.Static);

                if (activateHiddenMethod == null || !specialSongManager.m_HideBmsInfos.ContainsKey(checkUid)) return;

                var hideBmsInfo = specialSongManager.m_HideBmsInfos[checkUid];
                activateHiddenMethod.Invoke(null, new object[] { hideBmsInfo });
                specialSongManager.m_IsInvokeHideDic[checkUid] = true;
            }
            catch
            {
            }
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

