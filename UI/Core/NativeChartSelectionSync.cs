using Il2CppAssets.Scripts.Database;
using MDEN.Managers;

namespace MDEN.UI.Core
{
    internal static class NativeChartSelectionSync
    {
        public static int Sync(MusicInfo musicInfo, int difficulty)
        {
            if (musicInfo == null || string.IsNullOrEmpty(musicInfo.uid)) return difficulty;

            var nativeDifficulty = HiddenDifficultyController.Sync(musicInfo, difficulty);
            var dbMusicTag = GlobalDataBase.dbMusicTag;
            if (dbMusicTag != null)
            {
                nativeDifficulty = SpecialDifficultyController.Sync(musicInfo, nativeDifficulty);
                dbMusicTag.selectedDiffTglIndex = nativeDifficulty;
                dbMusicTag.pnlSelectMusicUid = musicInfo.uid;
                dbMusicTag.m_CurSelectedMusicInfo = musicInfo;
            }

            SyncDataHelperSelectedUid(musicInfo.uid);
            ClientLogManager.Msg($"Synced native chart selection: uid={musicInfo.uid}, difficulty={difficulty}, nativeDifficulty={nativeDifficulty}");
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
