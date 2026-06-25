using MDEN.Managers;
using MDEN.UI.Core;
using UnityEngine;

namespace MDEN.UI.Windows
{
    public class SettingsWindow : MDENWindowBase
    {
        private NativeListWindow _window;
        private NativeListItem _btnBack;
        private NativeListItem _btnFavGirlDisplayForOthers;
        private NativeListItem _btnHideBattleHealthBar;
        private NativeListItem _btnVerboseLogs;
        private int _lastSelectedIndex = -1;

        public override void Show()
        {
            _window = new NativeListWindow();
            _window.AutoReset = true;
            BuildList();
            _window.OnSelectionChanged += OnSelectionChanged;
            _window.Title = "设置";
            _window.Show();
            _lastSelectedIndex = -1;

            RegisterEventCleanup(() =>
            {
                if (_window != null)
                {
                    _window.OnSelectionChanged -= OnSelectionChanged;
                }
            });
        }

        private void BuildList()
        {
            _window.Items.Clear();

            _btnBack = CreateButton("- 返回 -", "回到主菜单");
            _btnFavGirlDisplayForOthers = CreateButton(
                "FavGirl",
                $"将我的 FavGirl 角色和精灵显示给其他玩家\n当前设置：{FormatSwitchState(ModConfigManager.EnableFavGirlDisplayForOthers)}");
            _btnHideBattleHealthBar = CreateButton(
                "隐藏血量",
                $"隐藏游戏内血量条和 Fever 条\n当前设置：{FormatSwitchState(ModConfigManager.HideBattleHealthBar)}");
            _btnVerboseLogs = CreateButton(
                "Debug日志",
                $"显示客户端调试日志和警告日志\n当前设置：{FormatSwitchState(ModConfigManager.EnableVerboseLogs)}");
        }

        private NativeListItem CreateButton(string title, string description)
        {
            var button = new NativeListItem(title, description);
            button.Texture = ResourceManager.GetRandomBannerTexture() ?? ResourceManager.GetSprite("OptionsPanel.png")?.texture;
            _window.Items.Add(button);
            return button;
        }

        private static string FormatSwitchState(bool enabled)
        {
            return enabled
                ? "<color=00ff00ff>开启</color>"
                : "<color=ff4444ff>关闭</color>";
        }

        private void OnSelectionChanged(INativeListWindow window, int objectIndex)
        {
            if (_window == null || objectIndex < 0 || objectIndex >= _window.Items.Count) return;

            if (_lastSelectedIndex != objectIndex)
            {
                _lastSelectedIndex = objectIndex;
                return;
            }

            var button = _window.Items[objectIndex];
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

            if (button == _btnHideBattleHealthBar)
            {
                ModConfigManager.SetHideBattleHealthBar(!ModConfigManager.HideBattleHealthBar);
                BattleHealthBarController.ApplyVisibility();
                RebuildWindow();
                return;
            }

            if (button != _btnVerboseLogs) return;

            ModConfigManager.SetEnableVerboseLogs(!ModConfigManager.EnableVerboseLogs);
            RebuildWindow();
        }

        private void RebuildWindow()
        {
            if (_window == null) return;

            _window.OnSelectionChanged -= OnSelectionChanged;
            _window.ForceClose();
            _window = new NativeListWindow();
            _window.AutoReset = true;
            _window.Title = "设置";
            BuildList();
            _window.OnSelectionChanged += OnSelectionChanged;
            _window.Show();
            _lastSelectedIndex = -1;
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
