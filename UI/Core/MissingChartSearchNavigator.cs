using System.Reflection;
using Il2Cpp;
using Il2CppAssets.Scripts.UI.Controls;
using Il2CppAssets.Scripts.UI.Panels;
using Il2CppAssets.Scripts.UI.Panels.PnlMusicTag;
using UnityEngine;
using UnityEngine.UI;

namespace MDEN.UI.Core
{
    internal enum MissingChartFallbackStatus
    {
        NotFound,
        Multiple,
        Error
    }

    internal static class MissingChartSearchNavigator
    {
        private const BindingFlags InstanceFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        public static void ShowAlreadyAvailable(string chartName)
        {
            ShowText.ShowInfo($"谱面已在本地: {chartName}");
        }

        public static void ShowActivated(string chartName)
        {
            ShowText.ShowInfo($"已从候选区导入谱面: {chartName}");
        }

        public static void ShowFallback(string chartName, MissingChartFallbackStatus status)
        {
            GUIUtility.systemCopyBuffer = chartName;
            var searchOpened = TryOpenNativeSearch(chartName);

            if (status == MissingChartFallbackStatus.Multiple)
            {
                ShowText.ShowInfo(searchOpened
                    ? $"候选区有多个可能结果，已打开搜索并复制: {chartName}"
                    : $"候选区有多个可能结果，已复制谱面名: {chartName}");
                return;
            }

            if (status == MissingChartFallbackStatus.Error)
            {
                ShowText.ShowInfo(searchOpened
                    ? $"自动导入失败，已打开搜索并复制: {chartName}"
                    : $"自动导入失败，已复制谱面名: {chartName}");
                return;
            }

            ShowText.ShowInfo(searchOpened
                ? $"候选区未找到，已打开搜索并复制: {chartName}"
                : $"候选区未找到，已复制谱面名: {chartName}");
        }

        public static bool TryOpenNativeSearch(string chartName)
        {
            try
            {
                OpenStageIfNeeded();

                var stage = GameObject.Find("UI/Standerd/PnlStage")?.GetComponent<PnlStage>();
                var musicTag = GetMusicTagPanel(stage);
                if (musicTag == null) return false;

                musicTag.Show();
                musicTag.SetTabIndex(PnlMusicTag.tab_search);

                var searchItem = GetSearchItem(musicTag);
                return searchItem != null && SetSearchText(searchItem, chartName);
            }
            catch (System.Exception ex)
            {
                MDEN.Managers.ClientLogManager.Warning($"Native search open failed: {ex.Message}");
                return false;
            }
        }

        private static void OpenStageIfNeeded()
        {
            var pnlMenu = GameObject.Find("UI/Standerd/PnlMenu")?.GetComponent<PnlMenu>();
            if (pnlMenu != null && pnlMenu.gameObject.active)
            {
                pnlMenu.backBtn.onClick.Invoke();
            }

            var pageHome = GameObject.Find("UI/Standerd/PnlHome")?.GetComponent<Il2CppArcadeController.UI.Panel.PnlHome.PageHome>();
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
        }

        private static PnlMusicTag GetMusicTagPanel(PnlStage pnlStage)
        {
            var field = typeof(PnlStage).GetField("m_PnlMusicTag", InstanceFlags);
            return field?.GetValue(pnlStage) as PnlMusicTag;
        }

        private static PnlMusicSearchItem GetSearchItem(PnlMusicTag musicTag)
        {
            var component = musicTag.GetComponentInChildren<PnlMusicSearchItem>(true);
            if (component != null) return component;

            var items = typeof(AbstractTabContainer).GetField("m_Items", InstanceFlags)?.GetValue(musicTag) as System.Collections.IEnumerable;
            if (items == null) return null;

            var index = 0;
            foreach (var item in items)
            {
                if (index == PnlMusicTag.tab_search) return item as PnlMusicSearchItem;
                index++;
            }

            return null;
        }

        private static bool SetSearchText(PnlMusicSearchItem searchItem, string chartName)
        {
            var inputField = typeof(PnlMusicSearchItem).GetField("m_InputField", InstanceFlags)?.GetValue(searchItem) as Il2CppAssets.Scripts.UI.PeroInputField;
            if (inputField == null) return false;

            inputField.text = chartName;
            inputField.onValueChanged.Invoke(chartName);
            inputField.ActivateInputField();

            typeof(PnlMusicSearchItem).GetMethod("OnTextChanged", InstanceFlags)?.Invoke(searchItem, new object[] { chartName });
            typeof(PnlMusicSearchItem).GetMethod("RefreshData", InstanceFlags)?.Invoke(searchItem, null);
            return true;
        }
    }
}
