using CustomAlbums.Managers;
using Il2Cpp;
using Il2CppArcadeController.UI.Panel.PnlHome;
using Il2CppAssets.Scripts.Database;
using Il2CppAssets.Scripts.UI.Panels;
using MelonLoader;
using UnityEngine;
using UnityEngine.UI;

namespace MDEN.UI.Core
{
    internal static class NativeChartNavigator
    {
        public static void JumpToChart(MusicInfo musicInfo)
        {
            if (musicInfo == null || string.IsNullOrEmpty(musicInfo.uid)) return;
            JumpToChart(musicInfo.uid);
        }

        public static void JumpToChart(string uid)
        {
            if (string.IsNullOrEmpty(uid)) return;

            try
            {
                NativeChartSelectionSync.SyncDataHelperSelectedUid(uid);

                var pnlMenu = GameObject.Find("UI/Standerd/PnlMenu")?.GetComponent<PnlMenu>();
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
                if (TryJumpToCustomAlbumChart(stage, uid))
                {
                    return;
                }

                stage?.SelectAllTagAndJumpToAssginIndex(uid);
            }
            catch (System.Exception ex)
            {
                MDEN.Managers.ClientLogManager.Warning($"Jump to multiplayer chart failed: {ex.Message}");
            }
        }

        private static bool TryJumpToCustomAlbumChart(PnlStage stage, string uid)
        {
            if (stage == null || !IsCustomAlbumUid(uid)) return false;

            var dbMusicTag = GlobalDataBase.dbMusicTag;
            if (dbMusicTag == null) return false;

            var musicInfo = dbMusicTag.GetMusicInfoFromAll(uid);
            if (musicInfo == null)
            {
                MDEN.Managers.ClientLogManager.Warning($"Custom chart jump skipped: music info not found, uid={uid}");
                return false;
            }

            var customUids = BuildCustomAlbumUidList(uid);
            if (!TryFindIndex(customUids, uid, out _))
            {
                MDEN.Managers.ClientLogManager.Warning($"Custom chart jump skipped: uid not in custom album list, uid={uid}");
                return false;
            }

            dbMusicTag.selectedTagIndex = AlbumManager.Uid;
            dbMusicTag.RefreshShowMusicUids(customUids);

            var showList = dbMusicTag.stageShowMusicList;
            if (!TryFindIndex(showList, uid, out var targetIndex))
            {
                MDEN.Managers.ClientLogManager.Warning($"Custom chart jump skipped: refreshed stage list does not contain uid={uid}");
                return false;
            }

            dbMusicTag.pnlSelectMusicUid = uid;
            dbMusicTag.m_CurSelectedMusicInfo = musicInfo;
            NativeChartSelectionSync.SyncDataHelperSelectedUid(uid);
            dbMusicTag.SetSelectedMusic(musicInfo);
            dbMusicTag.curSelectedMusicIdx = targetIndex;
            stage.musicFancyScrollView?.ScrollToDataIndex(targetIndex, 0f, true);
            return true;
        }

        private static bool IsCustomAlbumUid(string uid)
        {
            return !string.IsNullOrEmpty(uid) && uid.StartsWith($"{AlbumManager.Uid}-");
        }

        private static Il2CppSystem.Collections.Generic.List<string> BuildCustomAlbumUidList(string targetUid)
        {
            var result = new Il2CppSystem.Collections.Generic.List<string>();
            foreach (var albumUid in AlbumManager.GetAllUid())
            {
                if (!string.IsNullOrEmpty(albumUid) && !result.Contains(albumUid))
                {
                    result.Add(albumUid);
                }
            }

            if (!string.IsNullOrEmpty(targetUid) && !result.Contains(targetUid))
            {
                result.Add(targetUid);
            }

            return result;
        }

        private static bool TryFindIndex(Il2CppSystem.Collections.Generic.List<string> list, string uid, out int index)
        {
            index = -1;
            if (list == null || string.IsNullOrEmpty(uid)) return false;

            for (var i = 0; i < list.Count; i++)
            {
                if (list[i] != uid) continue;

                index = i;
                return true;
            }

            return false;
        }
    }
}
