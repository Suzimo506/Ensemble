using System;
using UnityEngine;
using PopupLib.UI.Windows;
using PopupLib.UI.Components;
using LocalizeLib;
using MDEN.UI.Core;
using MDEN.Managers;

namespace MDEN.UI.Windows
{
    // 左右分栏的大厅主菜单，遵守防错窗与生命周期管理规范
    public class MainMenuWindow : MDENWindowBase
    {
        private const string SupportUsUrl = "https://afdian.com/a/szm520";

        private ForumWindow _window;
        private ForumObject _btnProfile;
        private ForumObject _btnFriends;
        private ForumObject _btnLobbies;
        private ForumObject _btnSettings;
        private ForumObject _btnSupportUs;
        private ForumObject _btnAbout;

        public override void Show()
        {
            _window = new ForumWindow();
            _window.AutoReset = true;
            
            _btnAbout = new ForumObject(new LocalString(I18nManager.T("main.about")), new LocalString(GetCreditsText()));
            _btnAbout.Texture = ResourceManager.GetRandomBannerTexture() ?? ResourceManager.GetSprite("HomePanel.png")?.texture;
            _window.ForumObjects.Add(_btnAbout);

            _btnProfile = new ForumObject(new LocalString(I18nManager.T("main.profile")), new LocalString(I18nManager.T("main.profile.desc")));
            _btnProfile.Texture = ResourceManager.GetRandomBannerTexture() ?? ResourceManager.GetSprite("PlayerCard.png")?.texture;
            _window.ForumObjects.Add(_btnProfile);

            _btnFriends = new ForumObject(new LocalString(I18nManager.T("main.friends")), new LocalString(I18nManager.T("main.friends.desc")));
            _btnFriends.Texture = ResourceManager.GetRandomBannerTexture() ?? ResourceManager.GetSprite("SocialNetwork.png")?.texture;
            _window.ForumObjects.Add(_btnFriends);

            _btnLobbies = new ForumObject(new LocalString(I18nManager.T("main.lobbies")), new LocalString(I18nManager.T("main.lobbies.desc")));
            _btnLobbies.Texture = ResourceManager.GetRandomBannerTexture() ?? ResourceManager.GetSprite("RoomList.png")?.texture;
            _window.ForumObjects.Add(_btnLobbies);

            _btnSettings = new ForumObject(new LocalString(I18nManager.T("main.settings")), new LocalString(I18nManager.T("main.settings.desc")));
            _btnSettings.Texture = ResourceManager.GetRandomBannerTexture() ?? ResourceManager.GetSprite("OptionsPanel.png")?.texture;
            _window.ForumObjects.Add(_btnSettings);

            _btnSupportUs = new ForumObject(new LocalString(I18nManager.T("main.support")), new LocalString(I18nManager.T("main.support.desc")));
            _btnSupportUs.Texture = ResourceManager.GetRandomBannerTexture() ?? ResourceManager.GetSprite("HomePanel.png")?.texture;
            _window.ForumObjects.Add(_btnSupportUs);

            _window.OnSelectionChanged += OnSelectionChanged;

            _window.OnInternalShow += OnInternalShowInjectTitle;

            _window.Show();
            _lastSelectedIndex = -1; // 在 Show 之后重置

            // 核心规范：所有按键绑定必须交由基类回收，防止幽灵按键和 UI 污染
            RegisterEventCleanup(() => 
            {
                if (_window != null)
                {
                    _window.OnSelectionChanged -= OnSelectionChanged;
                    _window.OnInternalShow -= OnInternalShowInjectTitle;
                }
                
                var panel = GameObject.Find("UI/Forward/Tips/PnlBulletinNew");
                if (panel != null)
                {

                    var titleTrans = panel.transform.Find("ImgBase/ScrollView/MDENTitle");
                    if (titleTrans == null) titleTrans = panel.transform.Find("ImgBase/MDENTitle");
                    if (titleTrans != null) UnityEngine.Object.Destroy(titleTrans.gameObject);
                }
            });
        }

        private void OnInternalShowInjectTitle(PopupLib.UI.Windows.Abstract.BaseWindow w)
        {
            // 动态注入界面增强元素至弹窗
            var uiForward = GameObject.Find("UI/Forward");
            if (uiForward != null)
            {
                var pnlBulletin = uiForward.transform.Find("Tips/PnlBulletinNew");
                if (pnlBulletin != null)
                {
                    var imgBase = pnlBulletin.Find("ImgBase");
                    if (imgBase != null)
                    {
                        // 1. 注入标题
                        var oldTitle = imgBase.Find("MDENTitle");
                        if (oldTitle != null) UnityEngine.Object.Destroy(oldTitle.gameObject);
                        var oldTitleInScroll = imgBase.Find("ScrollView/MDENTitle");
                        if (oldTitleInScroll != null) UnityEngine.Object.Destroy(oldTitleInScroll.gameObject);

                        var txtTittleObj = pnlBulletin.Find("TxtTittle");
                        if (txtTittleObj != null)
                        {
                            var newTitle = GameObject.Instantiate(txtTittleObj.gameObject, imgBase);
                            newTitle.name = "MDENTitle";
                            newTitle.SetActive(true);

                            var loc = newTitle.GetComponent<Il2CppAssets.Scripts.PeroTools.GeneralLocalization.Localization>();
                            if (loc != null) UnityEngine.Object.Destroy(loc);

                            var txt = newTitle.GetComponent<UnityEngine.UI.Text>();
                            if (txt != null)
                            {
                                txt.text = I18nManager.T("main.title");
                                txt.alignment = UnityEngine.TextAnchor.MiddleCenter;
                            }

                            // 恢复绝对中心位置，去除人工偏移
                            var titleRect = newTitle.GetComponent<RectTransform>();
                            if (titleRect != null)
                            {
                                titleRect.anchorMin = new Vector2(0.5f, 1f);
                                titleRect.anchorMax = new Vector2(0.5f, 1f);
                                titleRect.pivot = new Vector2(0.5f, 0.5f);
                                titleRect.anchoredPosition = new Vector2(0f, 12f);
                            }
                        }
                    }
                }
            }
        }

        private int _lastSelectedIndex = -1;

        private static string GetCreditsText()
        {
            return I18nManager.Tf(
                "main.credits.text",
                Constants.ColorGreen,
                Constants.ColorCyan,
                Constants.ColorPink,
                Constants.ColorLavender);
        }

        private void OnSelectionChanged(PopupLib.UI.Windows.Interfaces.IListWindow window, int objectIndex)
        {
            if (objectIndex < 0 || objectIndex >= _window.ForumObjects.Count) return;

            // 第一次点击只会选中并且展示右侧描述文本，第二次点击才真正生效
            if (_lastSelectedIndex != objectIndex)
            {
                _lastSelectedIndex = objectIndex;
                return;
            }

            var button = _window.ForumObjects[objectIndex];

            if (button == _btnProfile)
            {
                Close();
                WindowStackController.OpenWindow(new ProfileWindow());
            }
            else if (button == _btnLobbies)
            {
                if (NavigationButton.IsMultiplayerBlockedByOutdatedVersion())
                {
                    Il2CppAssets.Scripts.UI.Controls.ShowText.ShowInfo(I18nManager.T("version.multiplayer_blocked"));
                    return;
                }

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
