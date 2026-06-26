using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Il2CppAssets.Scripts.Database;
using Il2CppAssets.Scripts.UI.Controls;
using MDEN.Managers;

namespace MDEN.Patches
{
    internal static class SongLibraryStatePatch
    {
        [HarmonyPatch]
        [HarmonyPriority(Priority.First)]
        internal static class ChartHiddenStateGuardPatch
        {
            private static IEnumerable<MethodBase> TargetMethods()
            {
                var db = typeof(DBMusicTag);
                return new[]
                {
                    db.GetMethod(nameof(DBMusicTag.AddHide)),
                    db.GetMethod(nameof(DBMusicTag.AddHideMusicUid)),
                    db.GetMethod(nameof(DBMusicTag.RemoveHide))
                }.Where(method => method != null);
            }

            private static bool Prefix()
            {
                if (!LobbyManager.IsInLobby) return true;

                ShowText.ShowInfo(I18nManager.T("patch.song_hide_disabled"));
                return false;
            }

            private static void Postfix()
            {
                PlayerManager.SyncChartStateFireAndForget();
            }
        }

        [HarmonyPatch(typeof(DBMusicTag), nameof(DBMusicTag.CurMusicInfo))]
        internal static class PlaylistBattleMusicInfoPatch
        {
            private static bool Prefix(ref MusicInfo __result)
            {
                if (!PlaylistManager.IsPlaylistBattleActive()) return true;

                var musicInfo = PlaylistManager.GetCurrentPlaylistMusicInfo();
                if (musicInfo == null || musicInfo.Pointer == IntPtr.Zero) return true;

                __result = musicInfo;
                return false;
            }
        }

        [HarmonyPatch]
        internal static class PlaylistMasterUnlockPatch
        {
            private static IEnumerable<MethodBase> TargetMethods()
            {
                return typeof(DataHelper).GetMethods()
                    .Where(method => method.Name == nameof(DataHelper.CheckMusicUnlockMaster));
            }

            private static bool Prefix(ref bool __result)
            {
                if (!PlaylistManager.IsPlaylistBattleActive()) return true;

                __result = true;
                return false;
            }
        }
    }
}
