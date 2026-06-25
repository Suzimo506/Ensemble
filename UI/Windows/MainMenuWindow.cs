using System;
using UnityEngine;
using MDEN.UI.Core;
using MDEN.Managers;

namespace MDEN.UI.Windows
{
    // 左右分栏的大厅主菜单，遵守防错窗与生命周期管理规范
    public class MainMenuWindow : MDENWindowBase
    {
        private const string SupportUsUrl = "https://afdian.com/a/szm520";

        private NativeListWindow _window;
        private NativeListItem _btnProfile;
        private NativeListItem _btnFriends;
        private NativeListItem _btnLobbies;
        private NativeListItem _btnSettings;
        private NativeListItem _btnSupportUs;
        private NativeListItem _btnAbout;

        public override void Show()
        {
            _window = new NativeListWindow();
            _window.AutoReset = true;

            _btnAbout = new NativeListItem("关于", Constants.CreditsText);
            _btnAbout.Texture = ResourceManager.GetRandomBannerTexture() ?? ResourceManager.GetSprite("HomePanel.png")?.texture;
            _window.Items.Add(_btnAbout);

            _btnProfile = new NativeListItem("个人信息", "更改自己的名字、个人简介、以及个性化修改");
            _btnProfile.Texture = ResourceManager.GetRandomBannerTexture() ?? ResourceManager.GetSprite("PlayerCard.png")?.texture;
            _window.Items.Add(_btnProfile);

            _btnFriends = new NativeListItem("好友列表", "查看自己的好友，与好友一起玩吧！");
            _btnFriends.Texture = ResourceManager.GetRandomBannerTexture() ?? ResourceManager.GetSprite("SocialNetwork.png")?.texture;
            _window.Items.Add(_btnFriends);

            _btnLobbies = new NativeListItem("联机大厅", "加入服务器，与服务器的其他人一起愉快的组队吧！");
            _btnLobbies.Texture = ResourceManager.GetRandomBannerTexture() ?? ResourceManager.GetSprite("RoomList.png")?.texture;
            _window.Items.Add(_btnLobbies);

            _btnSettings = new NativeListItem("设置", "更改游戏的各种设置喵");
            _btnSettings.Texture = ResourceManager.GetRandomBannerTexture() ?? ResourceManager.GetSprite("OptionsPanel.png")?.texture;
            _window.Items.Add(_btnSettings);

            _btnSupportUs = new NativeListItem("支持我们", "前往爱发电支持我们，让服务器更长久！OVO");
            _btnSupportUs.Texture = ResourceManager.GetRandomBannerTexture() ?? ResourceManager.GetSprite("HomePanel.png")?.texture;
            _window.Items.Add(_btnSupportUs);

            _window.OnSelectionChanged += OnSelectionChanged;

            _window.Title = "一起合奏吧";

            _window.Show();
            _lastSelectedIndex = -1; // 在 Show 之后重置

            // 核心规范：所有按键绑定必须交由基类回收，防止幽灵按键和 UI 污染
            RegisterEventCleanup(() =>
            {
                if (_window != null)
                {
                    _window.OnSelectionChanged -= OnSelectionChanged;
                }
            });
        }

        private int _lastSelectedIndex = -1;

        private void OnSelectionChanged(INativeListWindow window, int objectIndex)
        {
            if (objectIndex < 0 || objectIndex >= _window.Items.Count) return;

            // 第一次点击只会选中并且展示右侧描述文本，第二次点击才真正生效
            if (_lastSelectedIndex != objectIndex)
            {
                _lastSelectedIndex = objectIndex;
                return;
            }

            var button = _window.Items[objectIndex];

            if (button == _btnProfile)
            {
                Close();
                WindowStackController.OpenWindow(new ProfileWindow());
            }
            else if (button == _btnLobbies)
            {
                Close();
                WindowStackController.OpenWindow(LobbyManager.IsInLobby
                    ? new RoomListWindow()
                    : new ServerSelectionWindow());
            }
            else if (button == _btnSettings)
            {
                Close();
                WindowStackController.OpenWindow(new SettingsWindow());
            }
            else if (button == _btnSupportUs)
            {
                MDEN.Managers.ClientLogManager.Msg("Support us button selected.");
                Application.OpenURL(SupportUsUrl);
            }
        }

        public override void Close()
        {
            _lastSelectedIndex = -1; // 关闭时重置选择状态
            if (_window != null)
            {
                _window.ForceClose();
                _window = null;
            }
        }
    }
}
