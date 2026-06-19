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
                stage?.SelectAllTagAndJumpToAssginIndex(uid);
            }
            catch (System.Exception ex)
            {
                MDEN.Managers.ClientLogManager.Warning($"Jump to multiplayer chart failed: {ex.Message}");
            }
        }
    }
}
