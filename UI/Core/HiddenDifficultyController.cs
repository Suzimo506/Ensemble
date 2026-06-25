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
        public static int Sync(MusicInfo musicInfo, int difficulty)
        {
            if (musicInfo == null) return difficulty;

            var specialSongManager = Singleton<SpecialSongManager>.instance;
            if (specialSongManager == null) return difficulty;

            var checkUid = ChartManager.GetHiddenCheckUid(musicInfo);
            if (string.IsNullOrEmpty(checkUid)) return difficulty;

            var hiddenUnlocked = specialSongManager.IsInvokeHideBms(checkUid);

            if (difficulty == 4)
            {
                if (!hiddenUnlocked)
                {
                    specialSongManager.InvokeHideBms(musicInfo, true);
                }

                ActivateCustomAlbumHiddenIfNeeded(musicInfo, checkUid, specialSongManager);
                return ActivateNativeHiddenDifficulty(musicInfo, checkUid, specialSongManager);
            }

            if (hiddenUnlocked || specialSongManager.m_IsInvokeHideDic.ContainsKey(checkUid))
            {
                DeactivateNativeHiddenDifficulty(musicInfo, checkUid, specialSongManager);
            }

            return difficulty;
        }

        public static bool IsHiddenDifficultySelected(MusicInfo musicInfo, int nativeDifficulty)
        {
            if (musicInfo == null) return false;

            var specialSongManager = Singleton<SpecialSongManager>.instance;
            if (specialSongManager == null) return false;

            var checkUid = ChartManager.GetHiddenCheckUid(musicInfo);
            return !string.IsNullOrEmpty(checkUid) &&
                   specialSongManager.IsInvokeHideBms(checkUid) &&
                   nativeDifficulty == GetHiddenTriggerDifficulty(checkUid, specialSongManager);
        }

        public static bool IsHiddenInvoked(MusicInfo musicInfo)
        {
            if (musicInfo == null) return false;

            var specialSongManager = Singleton<SpecialSongManager>.instance;
            if (specialSongManager == null) return false;

            var checkUid = ChartManager.GetHiddenCheckUid(musicInfo);
            return !string.IsNullOrEmpty(checkUid) &&
                   specialSongManager.IsInvokeHideBms(checkUid);
        }

        private static int ActivateNativeHiddenDifficulty(
            MusicInfo musicInfo,
            string checkUid,
            SpecialSongManager specialSongManager)
        {
            var triggerDifficulty = GetHiddenTriggerDifficulty(checkUid, specialSongManager);
            if (triggerDifficulty <= 0) return 3;

            try
            {
                if (specialSongManager.m_HideBmsInfos != null &&
                    specialSongManager.m_HideBmsInfos.ContainsKey(checkUid))
                {
                    var hideBmsInfo = specialSongManager.m_HideBmsInfos[checkUid];
                    musicInfo.AddMaskValue($"difficulty{triggerDifficulty}", GetDifficultyText(musicInfo, hideBmsInfo.m_HideDiff));
                    musicInfo.AddMaskValue($"levelDesigner{triggerDifficulty}", GetLevelDesignerText(musicInfo, hideBmsInfo.m_HideDiff));
                    musicInfo.SetDifficulty(triggerDifficulty, hideBmsInfo.m_HideDiff);
                }
            }
            catch
            {
            }

            return triggerDifficulty;
        }

        private static void DeactivateNativeHiddenDifficulty(
            MusicInfo musicInfo,
            string checkUid,
            SpecialSongManager specialSongManager)
        {
            try
            {
                if (specialSongManager.m_IsInvokeHideDic.ContainsKey(checkUid))
                {
                    specialSongManager.m_IsInvokeHideDic.Remove(checkUid);
                }

                var triggerDifficulty = GetHiddenTriggerDifficulty(checkUid, specialSongManager);
                if (triggerDifficulty > 0)
                {
                    musicInfo.RemoveMaskValue($"difficulty{triggerDifficulty}");
                    musicInfo.RemoveMaskValue($"levelDesigner{triggerDifficulty}");
                }
            }
            catch
            {
            }
        }

        private static int GetHiddenTriggerDifficulty(string checkUid, SpecialSongManager specialSongManager)
        {
            try
            {
                if (string.IsNullOrEmpty(checkUid) ||
                    specialSongManager?.m_HideBmsInfos == null ||
                    !specialSongManager.m_HideBmsInfos.ContainsKey(checkUid))
                {
                    return 3;
                }

                var hideBmsInfo = specialSongManager.m_HideBmsInfos[checkUid];
                return hideBmsInfo.triggerDiff > 0 ? hideBmsInfo.triggerDiff : 3;
            }
            catch
            {
                return 3;
            }
        }

        private static string GetDifficultyText(MusicInfo musicInfo, int difficulty)
        {
            return difficulty switch
            {
                1 => musicInfo.difficulty1,
                2 => musicInfo.difficulty2,
                3 => musicInfo.difficulty3,
                4 => musicInfo.difficulty4,
                5 => musicInfo.difficulty5,
                _ => null
            };
        }

        private static string GetLevelDesignerText(MusicInfo musicInfo, int difficulty)
        {
            return difficulty switch
            {
                1 => musicInfo.levelDesigner1 ?? musicInfo.levelDesigner,
                2 => musicInfo.levelDesigner2 ?? musicInfo.levelDesigner,
                3 => musicInfo.levelDesigner3 ?? musicInfo.levelDesigner,
                4 => musicInfo.levelDesigner4 ?? musicInfo.levelDesigner,
                5 => musicInfo.levelDesigner5 ?? musicInfo.levelDesigner,
                _ => musicInfo.levelDesigner
            };
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
