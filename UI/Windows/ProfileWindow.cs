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
        private ForumObject _btnAvatar;
        private int _lastSelectedIndex = -1;

        public override async void Show()
        {
            ModConfigManager.LoadConfig();
            _window = new ForumWindow();
            _window.AutoReset = true;
            BuildList();
            _window.OnSelectionChanged += OnSelectionChanged;
            _window.OnInternalShow += OnInternalShowInjectTitle;
            _window.Show();
            _lastSelectedIndex = -1;

            RegisterEventCleanup(() =>
            {
                UnbindWindowEvents();
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
                txt.text = I18nManager.T("profile.title");
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
                ? I18nManager.T("profile.local.saved")
                : I18nManager.Tf("profile.summary", profile.Name, SanitizeColor(profile.ChatColor), profile.Bio ?? "", profile.EntranceMessage ?? "", profile.Title ?? "");

            _btnBack = new ForumObject(new LocalString(I18nManager.T("common.back.button")), new LocalString(I18nManager.T("common.back.main_menu")));
            _btnBack.Texture = GetProfileBannerTexture("HomePanel.png");
            _window.ForumObjects.Add(_btnBack);

            _btnRefresh = new ForumObject(new LocalString(I18nManager.T("profile.local.title")), new LocalString(summary));
            _btnRefresh.Texture = GetProfileBannerTexture("PlayerCard.png");
            _window.ForumObjects.Add(_btnRefresh);

            _btnAvatar = new ForumObject(
                new LocalString(I18nManager.T("profile.avatar.edit")),
                new LocalString(BuildAvatarGuideText()));
            _btnAvatar.Texture = GetProfileBannerTexture("OptionsPanel.png");
            _window.ForumObjects.Add(_btnAvatar);

            _btnName = new ForumObject(
                new LocalString(I18nManager.T("profile.name.edit")),
                new LocalString(WithCurrentSetting(I18nManager.T("profile.name.desc"), profile?.Name)));
            _btnName.Texture = GetProfileBannerTexture("PlayerCard.png");
            _window.ForumObjects.Add(_btnName);

            _btnNameColor = new ForumObject(
                new LocalString(I18nManager.T("profile.color.edit")),
                new LocalString(WithCurrentSetting(I18nManager.T("profile.color.desc"), SanitizeColor(profile?.ChatColor))));
            _btnNameColor.Texture = GetProfileBannerTexture("OptionsPanel.png");
            _window.ForumObjects.Add(_btnNameColor);

            _btnBio = new ForumObject(
                new LocalString(I18nManager.T("profile.bio.edit")),
                new LocalString(WithCurrentSetting(I18nManager.T("profile.bio.desc"), profile?.Bio)));
            _btnBio.Texture = GetProfileBannerTexture("SocialNetwork.png");
            _window.ForumObjects.Add(_btnBio);

            _btnEntranceMessage = new ForumObject(
                new LocalString(I18nManager.T("profile.entrance.edit")),
                new LocalString(WithCurrentSetting(I18nManager.T("profile.entrance.desc"), profile?.EntranceMessage)));
            _btnEntranceMessage.Texture = GetProfileBannerTexture("HomePanel.png");
            _window.ForumObjects.Add(_btnEntranceMessage);

            _btnTitle = new ForumObject(
                new LocalString(I18nManager.T("profile.title.edit")),
                new LocalString(WithCurrentSetting(I18nManager.T("profile.title.desc"), profile?.Title)));
            _btnTitle.Texture = GetProfileBannerTexture("PlayerCard.png");
            _window.ForumObjects.Add(_btnTitle);
        }

        private static Texture2D GetProfileBannerTexture(string fallbackSpriteName)
        {
            return ResourceManager.GetRandomBannerTexture() ??
                   ResourceManager.GetSprite(fallbackSpriteName)?.texture;
        }

        private static string WithCurrentSetting(string description, string value)
        {
            return $"{description}\n{I18nManager.Tf("common.current_setting", Constants.ColorYellow, EscapeRichText(GetDisplayValue(value)))}";
        }

        private static string BuildAvatarGuideText()
        {
            return I18nManager.Tf("profile.avatar.guide", Constants.ColorGreen, EscapeRichText(AvatarManager.GetAvatarLibraryFolder()));
        }

        private static string GetDisplayValue(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? I18nManager.T("profile.unset") : value.Trim();
        }

        private static string SanitizeColor(string color)
        {
            if (string.IsNullOrWhiteSpace(color)) return "ffffff";
            return color.Trim().TrimStart('#');
        }

        private static string EscapeRichText(string value)
        {
            return value?.Replace("<", "＜").Replace(">", "＞") ?? string.Empty;
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
                WindowStackController.OpenWindow(new MainMenuWindow());
            }
            else if (button == _btnRefresh)
            {
                await TryRefreshProfileAsync();
            }
            else if (button == _btnAvatar)
            {
                Close();
                WindowStackController.OpenWindow(new AvatarPickerWindow());
            }
            else if (button == _btnName)
            {
                OpenInput(I18nManager.T("profile.name.edit"), 16, value => !string.IsNullOrWhiteSpace(value), value => PlayerManager.UpdateNameAsync(value), I18nManager.T("profile.validation.name"));
            }
            else if (button == _btnNameColor)
            {
                OpenInput(I18nManager.T("profile.color.edit"), 8, value => ColorRegex.IsMatch(value), value => PlayerManager.UpdateChatColorAsync(value), I18nManager.T("profile.validation.color"));
            }
            else if (button == _btnBio)
            {
                OpenInput(I18nManager.T("profile.bio.edit"), 30, value => true, value => PlayerManager.UpdateBioAsync(value), I18nManager.T("profile.validation.bio"));
            }
            else if (button == _btnEntranceMessage)
            {
                OpenInput(I18nManager.T("profile.entrance.edit"), 12, value => true, value => PlayerManager.UpdateEntranceMessageAsync(value), I18nManager.T("profile.validation.entrance"));
            }
            else if (button == _btnTitle)
            {
                OpenInput(I18nManager.T("profile.title.edit"), 12, value => true, value => PlayerManager.UpdateTitleAsync(value), I18nManager.T("profile.validation.title"));
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
                    MDEN.Managers.ClientLogManager.Warning(validationError);
                    MainThreadDispatcher.Enqueue(RebuildWindow);
                    return;
                }

                await SaveProfileFieldAsync(fieldName, value, saveAction);
                MainThreadDispatcher.Enqueue(RebuildWindow);
            };
            input.Show();
        }

        private async Task SaveProfileFieldAsync(string fieldName, string value, Func<string, Task> saveAction)
        {
            IDisposable uiLock = WindowStackController.LockUI(I18nManager.T("common.saving"));

            try
            {
                await saveAction.Invoke(value);
                await TrySyncLocalProfileToServerAsync();
                MDEN.Managers.ClientLogManager.Msg($"Local profile field saved: {fieldName}");
            }
            catch (Exception ex)
            {
                MDEN.Managers.ClientLogManager.Warning($"Save local profile field failed: {fieldName}, {ex.Message}");
            }
            finally
            {
                await MainThreadDispatcher.InvokeAsync(() => uiLock?.Dispose());
            }
        }

        private static async Task TrySyncLocalProfileToServerAsync()
        {
            if (!ConnectionManager.CanSendRequests) return;

            try
            {
                await PlayerManager.SyncLocalProfileToServerAsync();
            }
            catch (Exception ex)
            {
                if (ConnectionManager.CanSendRequests)
                {
                    MDEN.Managers.ClientLogManager.Warning($"Sync local profile after save failed: {ex.Message}");
                }
            }
        }

        private async Task TryRefreshProfileAsync()
        {
            try
            {
                ModConfigManager.LoadConfig();
                await PlayerManager.GetMyProfileAsync();
                if (IsDisposed) return;
                MainThreadDispatcher.Enqueue(() =>
                {
                    if (IsDisposed) return;
                    RebuildWindow();
                });
            }
            catch (Exception ex)
            {
                MDEN.Managers.ClientLogManager.Warning($"Refresh profile failed: {ex.Message}");
            }
        }

        private void RebuildWindow()
        {
            if (_window == null) return;

            UnbindWindowEvents();
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
                UnbindWindowEvents();
                _window.ForceClose();
                _window = null;
            }
        }

        private void UnbindWindowEvents()
        {
            if (_window == null) return;

            _window.OnSelectionChanged -= OnSelectionChanged;
            _window.OnInternalShow -= OnInternalShowInjectTitle;
        }
    }
}
