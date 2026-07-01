using Il2CppAssets.Scripts.Database;
using MDEN.Managers;

namespace MDEN.UI.Core
{
    internal static class NativeChartSelectionSync
    {
        public static int Sync(MusicInfo musicInfo, int difficulty)
        {
            return Sync(musicInfo, difficulty, musicInfo?.uid);
        }

        public static int Sync(MusicInfo musicInfo, int difficulty, string selectedUid)
        {
            return Sync(musicInfo, difficulty, selectedUid, false);
        }

        public static int SyncForBattleStart(MusicInfo musicInfo, int difficulty, string selectedUid)
        {
            return Sync(musicInfo, difficulty, selectedUid, true);
        }

        private static int Sync(MusicInfo musicInfo, int difficulty, string selectedUid, bool forceSelectMusic)
        {
            if (musicInfo == null || string.IsNullOrEmpty(musicInfo.uid)) return difficulty;

            if (string.IsNullOrEmpty(selectedUid)) selectedUid = musicInfo.uid;
            if (SpecialChartVariantResolver.IsKnownVariantPair(selectedUid))
            {
                SpecialChartVariantResolver.ApplyVariantSelection(selectedUid);
            }
            else
            {
                SpecialChartVariantResolver.RestoreVariantSelectionOverride();
            }

            var nativeDifficulty = HiddenDifficultyController.Sync(musicInfo, difficulty);
            var dbMusicTag = GlobalDataBase.dbMusicTag;
            if (dbMusicTag != null)
            {
                nativeDifficulty = SpecialDifficultyController.Sync(musicInfo, nativeDifficulty);
                if (forceSelectMusic)
                {
                    dbMusicTag.SetSelectedMusic(musicInfo, false, true);
                }

                dbMusicTag.selectedDiffTglIndex = nativeDifficulty;
                dbMusicTag.pnlSelectMusicUid = selectedUid;
                dbMusicTag.m_CurSelectedMusicInfo = musicInfo;
                SpecialChartVariantResolver.SyncSelection(selectedUid);
            }

            SyncDataHelperSelectedUid(selectedUid);
            ClientLogManager.Msg($"Synced native chart selection: uid={musicInfo.uid}, selectedUid={selectedUid}, difficulty={difficulty}, nativeDifficulty={nativeDifficulty}");
            return nativeDifficulty;
        }

        public static void SyncDataHelperSelectedUid(string uid)
        {
            if (string.IsNullOrEmpty(uid)) return;

            try
            {
                DataHelper.selectedMusicUidFromInfoList = uid;
            }
            catch (System.Exception ex)
            {
                ClientLogManager.Warning($"Failed to sync DataHelper selected uid={uid}: {ex.Message}");
            }
        }
    }
}
