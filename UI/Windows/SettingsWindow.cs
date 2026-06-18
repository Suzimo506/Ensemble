using LocalizeLib;
using MDEN.Managers;
using MDEN.UI.Core;
using PopupLib.UI.Components;
using PopupLib.UI.Windows;
using UnityEngine;

namespace MDEN.UI.Windows
{
    public class SettingsWindow : MDENWindowBase
    {
        private ForumWindow _window;
        private ForumObject _btnBack;
        private ForumObject _btnFavGirlDisplayForOthers;
        private ForumObject _btnHideBattleHealthBar;
        private int _lastSelectedIndex = -1;

        public override void Show()
        {
            _window = new ForumWindow();
            _window.AutoReset = true;
            BuildList();
            _window.OnSelectionChanged += OnSelectionChanged;
            _window.OnInternalShow += OnInternalShowInjectTitle;
            _window.Show();
            _lastSelectedIndex = -1;

            RegisterEventCleanup(() =>
            {
                if (_window != null)
                {
                    _window.OnSelectionChanged -= OnSelectionChanged;
                    _window.OnInternalShow -= OnInternalShowInjectTitle;
                }

                RemoveInjectedTitle();
            });
        }

        private void BuildList()
        {
            _window.ForumObjects.Clear();

            _btnBack = CreateButton("- 返回 -", "回到主菜单");
            _btnFavGirlDisplayForOthers = CreateButton(
                ModConfigManager.EnableFavGirlDisplayForOthers ? "已开启" : "已关闭",
                $"将我的 FavGirl 角色和精灵显示给其他玩家\n当前设置：{FormatSwitchState(ModConfigManager.EnableFavGirlDisplayForOthers)}");
            _btnHideBattleHealthBar = CreateButton(
                ModConfigManager.HideBattleHealthBar ? "已隐藏" : "已显示",
                $"隐藏游戏内血量条和 Fever 条\n当前设置：{FormatSwitchState(ModConfigManager.HideBattleHealthBar)}");
        }

        private ForumObject CreateButton(string title, string description)
        {
            var button = new ForumObject(new LocalString(title), new LocalString(description));
            button.Texture = ResourceManager.GetRandomBannerTexture() ?? ResourceManager.GetSprite("OptionsPanel.png")?.texture;
            _window.ForumObjects.Add(button);
            return button;
        }

        private static string FormatSwitchState(bool enabled)
        {
            return enabled
                ? "<color=00ff00ff>开启</color>"
                : "<color=ff4444ff>关闭</color>";
        }

        private void OnSelectionChanged(PopupLib.UI.Windows.Interfaces.IListWindow window, int objectIndex)
        {
            if (_window == null || objectIndex < 0 || objectIndex >= _window.ForumObjects.Count) return;

            if (_lastSelectedIndex != objectIndex)
            {
                _lastSelectedIndex = objectIndex;
                return;
            }

            var button = _window.ForumObjects[objectIndex];
            if (button == _btnBack)
            {
                Close();
                WindowStackController.OpenWindow(new MainMenuWindow());
                return;
            }

            if (button == _btnFavGirlDisplayForOthers)
            {
                ModConfigManager.SetEnableFavGirlDisplayForOthers(!ModConfigManager.EnableFavGirlDisplayForOthers);
                PlayerManager.SyncSelectionFireAndForget(GameAccountManager.RefreshSelectionSnapshot());
                RoomHudController.RequestRefresh();
                RebuildWindow();
                return;
            }

            if (button != _btnHideBattleHealthBar) return;

            ModConfigManager.SetHideBattleHealthBar(!ModConfigManager.HideBattleHealthBar);
            Patches.BattleFlowPatch.ApplyBattleHealthBarVisibility();
            RebuildWindow();
        }

        private void RebuildWindow()
        {
            if (_window == null) return;

            _window.OnSelectionChanged -= OnSelectionChanged;
            _window.OnInternalShow -= OnInternalShowInjectTitle;
            _window.ForceClose();
            _window = new ForumWindow();
            _window.AutoReset = true;
            BuildList();
            _window.OnSelectionChanged += OnSelectionChanged;
            _window.OnInternalShow += OnInternalShowInjectTitle;
            _window.Show();
            _lastSelectedIndex = -1;
        }

        private void OnInternalShowInjectTitle(PopupLib.UI.Windows.Abstract.BaseWindow w)
        {
            var uiForward = GameObject.Find("UI/Forward");
            var pnlBulletin = uiForward?.transform.Find("Tips/PnlBulletinNew");
            var imgBase = pnlBulletin?.Find("ImgBase");
            var txtTitleObj = pnlBulletin?.Find("TxtTittle");
            if (imgBase == null || txtTitleObj == null) return;

            RemoveInjectedTitle();

            var newTitle = GameObject.Instantiate(txtTitleObj.gameObject, imgBase);
            newTitle.name = "MDENTitle";
            newTitle.SetActive(true);

            var loc = newTitle.GetComponent<Il2CppAssets.Scripts.PeroTools.GeneralLocalization.Localization>();
            if (loc != null) UnityEngine.Object.Destroy(loc);

            var text = newTitle.GetComponent<UnityEngine.UI.Text>();
            if (text != null)
            {
                text.text = "设置";
                text.alignment = TextAnchor.MiddleCenter;
            }

            var rect = newTitle.GetComponent<RectTransform>();
            if (rect != null)
            {
                rect.anchorMin = new Vector2(0.5f, 1f);
                rect.anchorMax = new Vector2(0.5f, 1f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.anchoredPosition = new Vector2(0f, 12f);
            }
        }

        private void RemoveInjectedTitle()
        {
            var panel = GameObject.Find("UI/Forward/Tips/PnlBulletinNew");
            if (panel == null) return;

            var titleTrans = panel.transform.Find("ImgBase/ScrollView/MDENTitle");
            if (titleTrans == null) titleTrans = panel.transform.Find("ImgBase/MDENTitle");
            if (titleTrans != null) UnityEngine.Object.Destroy(titleTrans.gameObject);
        }

        public override void Close()
        {
            _lastSelectedIndex = -1;
            if (_window != null)
            {
                _window.ForceClose();
                _window = null;
            }
        }
    }
}
