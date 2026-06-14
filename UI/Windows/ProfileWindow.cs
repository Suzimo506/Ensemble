using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using UnityEngine;
using LocalizeLib;
using MDEN.Managers;
using MDEN.UI.Core;
using MelonLoader;
using PopupLib.UI.Components;
using PopupLib.UI.Windows;

namespace MDEN.UI.Windows
{
    public class ProfileWindow : MDENWindowBase
    {
        private static readonly Regex ColorRegex = new Regex("^[0-9a-fA-F]{3,8}$", RegexOptions.Compiled);
        private ForumWindow _window;
        private ForumObject _btnBack;
        private ForumObject _btnRefresh;
        private ForumObject _btnName;
        private ForumObject _btnNameColor;
        private ForumObject _btnBio;
        private ForumObject _btnEntranceMessage;
        private ForumObject _btnTitle;
        private int _lastSelectedIndex = -1;

        public override async void Show()
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

            await TryRefreshProfileAsync();
        }

        private void OnInternalShowInjectTitle(PopupLib.UI.Windows.Abstract.BaseWindow w)
        {
            var uiForward = GameObject.Find("UI/Forward");
            if (uiForward == null) return;

            var pnlBulletin = uiForward.transform.Find("Tips/PnlBulletinNew");
            if (pnlBulletin == null) return;

            var imgBase = pnlBulletin.Find("ImgBase");
            if (imgBase == null) return;

            var oldTitle = imgBase.Find("MDENTitle");
            if (oldTitle != null) UnityEngine.Object.Destroy(oldTitle.gameObject);
            var oldTitleInScroll = imgBase.Find("ScrollView/MDENTitle");
            if (oldTitleInScroll != null) UnityEngine.Object.Destroy(oldTitleInScroll.gameObject);

            var txtTittleObj = pnlBulletin.Find("TxtTittle");
            if (txtTittleObj == null) return;

            var newTitle = GameObject.Instantiate(txtTittleObj.gameObject, imgBase);
            newTitle.name = "MDENTitle";
            newTitle.SetActive(true);

            var loc = newTitle.GetComponent<Il2CppAssets.Scripts.PeroTools.GeneralLocalization.Localization>();
            if (loc != null) UnityEngine.Object.Destroy(loc);

            var txt = newTitle.GetComponent<UnityEngine.UI.Text>();
            if (txt != null)
            {
                txt.text = "个人信息";
                txt.alignment = TextAnchor.MiddleCenter;
            }

            var titleRect = newTitle.GetComponent<RectTransform>();
            if (titleRect != null)
            {
                titleRect.anchorMin = new Vector2(0.5f, 1f);
                titleRect.anchorMax = new Vector2(0.5f, 1f);
                titleRect.pivot = new Vector2(0.5f, 0.5f);
                titleRect.anchoredPosition = new Vector2(0f, 12f);
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

        private void BuildList()
        {
            _window.ForumObjects.Clear();

            var profile = PlayerManager.CurrentProfile;
            var summary = profile == null
                ? "连接服务器后可查看和修改个人信息"
                : $"名字: {profile.Name}\n颜色: {profile.ChatColor ?? "ffffff"}\n介绍: {profile.Bio ?? ""}\n入场提示: {profile.EntranceMessage ?? ""}\n头衔: {profile.Title ?? ""}";

            _btnBack = new ForumObject(new LocalString("返回"), new LocalString("回到主菜单"));
            _btnBack.Texture = ResourceManager.GetRandomBannerTexture() ?? ResourceManager.GetSprite("OptionsPanel.png")?.texture;
            _window.ForumObjects.Add(_btnBack);

            _btnRefresh = new ForumObject(new LocalString("刷新信息"), new LocalString(summary));
            _btnRefresh.Texture = ResourceManager.GetRandomBannerTexture() ?? ResourceManager.GetSprite("PlayerCard.png")?.texture;
            _window.ForumObjects.Add(_btnRefresh);

            _btnName = new ForumObject(new LocalString("修改名字"), new LocalString("修改自己的名字，16字上限"));
            _btnName.Texture = ResourceManager.GetRandomBannerTexture() ?? ResourceManager.GetSprite("PlayerCard.png")?.texture;
            _window.ForumObjects.Add(_btnName);

            _btnNameColor = new ForumObject(new LocalString("修改名字颜色"), new LocalString("输入十六进制颜色，不要带#，例如 ff00ff"));
            _btnNameColor.Texture = ResourceManager.GetRandomBannerTexture() ?? ResourceManager.GetSprite("OptionsPanel.png")?.texture;
            _window.ForumObjects.Add(_btnNameColor);

            _btnBio = new ForumObject(new LocalString("修改个人介绍"), new LocalString("别人在房间点击你的卡片时显示的介绍，30字上限"));
            _btnBio.Texture = ResourceManager.GetRandomBannerTexture() ?? ResourceManager.GetSprite("HomePanel.png")?.texture;
            _window.ForumObjects.Add(_btnBio);

            _btnEntranceMessage = new ForumObject(new LocalString("修改入场提示语"), new LocalString("进入房间时显示的提示语，12字上限"));
            _btnEntranceMessage.Texture = ResourceManager.GetRandomBannerTexture() ?? ResourceManager.GetSprite("RoomList.png")?.texture;
            _window.ForumObjects.Add(_btnEntranceMessage);

            _btnTitle = new ForumObject(new LocalString("修改头衔"), new LocalString("显示在个人信息中的头衔，12字上限"));
            _btnTitle.Texture = ResourceManager.GetRandomBannerTexture() ?? ResourceManager.GetSprite("SocialNetwork.png")?.texture;
            _window.ForumObjects.Add(_btnTitle);
        }

        private async void OnSelectionChanged(PopupLib.UI.Windows.Interfaces.IListWindow window, int objectIndex)
        {
            if (objectIndex < 0 || objectIndex >= _window.ForumObjects.Count) return;

            if (_lastSelectedIndex != objectIndex)
            {
                _lastSelectedIndex = objectIndex;
                return;
            }

            var button = _window.ForumObjects[objectIndex];
            if (button == _btnBack)
            {
                Close();
                UIManager.OpenWindow(new MainMenuWindow());
            }
            else if (button == _btnRefresh)
            {
                await TryRefreshProfileAsync();
            }
            else if (button == _btnName)
            {
                OpenInput("名字", 16, value => !string.IsNullOrWhiteSpace(value), value => PlayerManager.UpdateNameAsync(value), "名字不能为空且最多16字");
            }
            else if (button == _btnNameColor)
            {
                OpenInput("名字颜色", 8, value => ColorRegex.IsMatch(value), value => PlayerManager.UpdateChatColorAsync(value), "名字颜色必须是十六进制颜色，不要带#");
            }
            else if (button == _btnBio)
            {
                OpenInput("个人介绍", 30, value => true, value => PlayerManager.UpdateBioAsync(value), "个人介绍最多30字");
            }
            else if (button == _btnEntranceMessage)
            {
                OpenInput("入场提示语", 12, value => true, value => PlayerManager.UpdateEntranceMessageAsync(value), "入场提示语最多12字");
            }
            else if (button == _btnTitle)
            {
                OpenInput("头衔", 12, value => true, value => PlayerManager.UpdateTitleAsync(value), "头衔最多12字");
            }
        }

        private void OpenInput(string fieldName, int maxLength, Func<string, bool> validator, Func<string, Task> saveAction, string validationError)
        {
            if (_window != null) _window.ForceClose();

            var input = new InputWindow();
            input.OnCompletion += async (w) =>
            {
                var value = input.Result ?? "";
                if (value.Length > maxLength || !validator(value))
                {
                    MelonLogger.Warning(validationError);
                    RebuildWindow();
                    return;
                }

                await SaveProfileFieldAsync(fieldName, value, saveAction);
                RebuildWindow();
            };
            input.Show();
        }

        private async Task SaveProfileFieldAsync(string fieldName, string value, Func<string, Task> saveAction)
        {
            using var _ = UIManager.LockUI("Saving profile...");

            try
            {
                await saveAction.Invoke(value);
                MelonLogger.Msg($"Profile field saved: {fieldName}");
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Save profile field failed: {fieldName}, {ex.Message}");
            }
        }

        private async Task TryRefreshProfileAsync()
        {
            try
            {
                await PlayerManager.GetMyProfileAsync();
                MainThreadDispatcher.Enqueue(RebuildWindow);
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Refresh profile failed: {ex.Message}");
            }
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
