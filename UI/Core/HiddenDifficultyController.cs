using System.Linq;
using System.Reflection;
using Il2Cpp;
using Il2CppAssets.Scripts.Database;
using Il2CppAssets.Scripts.PeroTools.Commons;
using MDEN.Managers;

namespace MDEN.UI.Core
{
    internal static class HiddenDifficultyController
    {
        public static void Sync(MusicInfo musicInfo, int difficulty)
        {
            if (musicInfo == null) return;

            var specialSongManager = Singleton<SpecialSongManager>.instance;
            if (specialSongManager == null) return;

            var checkUid = ChartManager.GetHiddenCheckUid(musicInfo);
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
    }
}
