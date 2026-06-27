using HarmonyLib;
using Il2CppAssets.Scripts.Database;
using MDEN.Managers;

namespace MDEN.Patches
{
    internal static class DataHelperSelectedMusicUidPatch
    {
        [HarmonyPatch(typeof(DataHelper), "get_selectedMusicUid")]
        [HarmonyPriority(Priority.First)]
        internal static class PlaylistBattleSelectedMusicUidPatch
        {
            private static bool Prefix(ref string __result)
            {
                if (!PlaylistManager.IsPlaylistBattleActive()) return true;

                var musicInfo = PlaylistManager.GetCurrentPlaylistMusicInfo();
                if (musicInfo == null || musicInfo.Pointer == System.IntPtr.Zero || string.IsNullOrEmpty(musicInfo.uid))
                {
                    return true;
                }

                __result = musicInfo.uid;
                return false;
            }
        }
    }
}
