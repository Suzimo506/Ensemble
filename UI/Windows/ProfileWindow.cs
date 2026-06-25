using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using UnityEngine;
using MDEN.Managers;
using MDEN.UI.Core;
using MelonLoader;

namespace MDEN.UI.Windows
{
    public class ProfileWindow : MDENWindowBase
    {
        private static readonly Regex ColorRegex = new Regex("^[0-9a-fA-F]{3,8}$", RegexOptions.Compiled);
        private NativeListWindow _window;
        private NativeListItem _btnBack;
        private NativeListItem _btnRefresh;
        private NativeListItem _btnName;
        private NativeListItem _btnNameColor;
        private NativeListItem _btnBio;
        private NativeListItem _btnEntranceMessage;
        private NativeListItem _btnTitle;
        private NativeListItem _btnAvatar;
        private int _lastSelectedIndex = -1;

        public override async void Show()
        {
            ModConfigManager.LoadConfig();
            _window = new NativeListWindow();
            _window.AutoReset = true;
            BuildList();
            _window.OnSelectionChanged += OnSelectionChanged;
            _window.Title = "个人信息";
            _window.Show();
            _lastSelectedIndex = -1;

            RegisterEventCleanup(() =>
            {
                UnbindWindowEvents();
            });

            await TryRefreshProfileAsync();
        }

        private void BuildList()
        {
            _window.Items.Clear();

            var profile = PlayerManager.CurrentProfile;
            var summary = profile == null
                ? "个人信息会保存到本地 Ensemble.json"
                : $"名字: {profile.Name}\n颜色: {SanitizeColor(profile.ChatColor)}\n介绍: {profile.Bio ?? ""}\n入场提示: {profile.EntranceMessage ?? ""}\n头衔: {profile.Title ?? ""}";

            _btnBack = new NativeListItem("- 返回 -", "回到主菜单");
            _btnBack.Texture = GetProfileBannerTexture("HomePanel.png");
            _window.Items.Add(_btnBack);

            _btnRefresh = new NativeListItem("本地资料", summary);
            _btnRefresh.Texture = GetProfileBannerTexture("PlayerCard.png");
            _window.Items.Add(_btnRefresh);

            _btnAvatar = new NativeListItem(
                "修改头像",
                BuildAvatarGuideText());
            _btnAvatar.Texture = GetProfileBannerTexture("OptionsPanel.png");
            _window.Items.Add(_btnAvatar);

            _btnName = new NativeListItem(
                "修改名字",
                WithCurrentSetting("修改自己的名字，16字上限", profile?.Name));
            _btnName.Texture = GetProfileBannerTexture("PlayerCard.png");
            _window.Items.Add(_btnName);

            _btnNameColor = new NativeListItem(
                "修改名字颜色",
                WithCurrentSetting("输入十六进制颜色，不要带#，例如 ff00ff", SanitizeColor(profile?.ChatColor)));
            _btnNameColor.Texture = GetProfileBannerTexture("OptionsPanel.png");
            _window.Items.Add(_btnNameColor);

            _btnBio = new NativeListItem(
                "修改个人介绍",
                WithCurrentSetting("别人在房间点击你的卡片时显示的介绍，30字上限", profile?.Bio));
            _btnBio.Texture = GetProfileBannerTexture("SocialNetwork.png");
            _window.Items.Add(_btnBio);

            _btnEntranceMessage = new NativeListItem(
                "修改入场提示语",
                WithCurrentSetting("进入房间时显示的提示语，12字上限", profile?.EntranceMessage));
            _btnEntranceMessage.Texture = GetProfileBannerTexture("HomePanel.png");
            _window.Items.Add(_btnEntranceMessage);

            _btnTitle = new NativeListItem(
                "修改头衔",
                WithCurrentSetting("显示在个人信息中的头衔，12字上限", profile?.Title));
            _btnTitle.Texture = GetProfileBannerTexture("PlayerCard.png");
            _window.Items.Add(_btnTitle);
        }

        private static Texture2D GetProfileBannerTexture(string fallbackSpriteName)
        {
            return ResourceManager.GetRandomBannerTexture() ??
                   ResourceManager.GetSprite(fallbackSpriteName)?.texture;
        }

        private static string WithCurrentSetting(string description, string value)
        {
            return $"{description}\n当前设置：<color={Constants.ColorYellow}>{EscapeRichText(GetDisplayValue(value))}</color>";
        }

        private static string BuildAvatarGuideText()
        {
            return $"<color={Constants.ColorGreen}>添加头像教程：\n" +
                   $"1. 将 png、jpg 或 jpeg 图片放入头像文件夹\n" +
                   $"2. 再次点击左侧“修改头像”进入头像列表\n" +
                   $"3. 选择图片后会自动裁成圆形并长期保存\n" +
                   $"当前头像文件夹：{EscapeRichText(AvatarManager.GetAvatarLibraryFolder())}</color>";
        }

        private static string GetDisplayValue(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? "未设置" : value.Trim();
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

        private async void OnSelectionChanged(INativeListWindow window, int objectIndex)
        {
            if (objectIndex < 0 || objectIndex >= _window.Items.Count) return;

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

            var input = new NativeInputDialog();
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
            IDisposable uiLock = WindowStackController.LockUI("Saving profile...");

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
            _window = new NativeListWindow();
            _window.AutoReset = true;
            _window.Title = "个人信息";
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
                UnbindWindowEvents();
                _window.ForceClose();
                _window = null;
            }
        }

        private void UnbindWindowEvents()
        {
            if (_window == null) return;

            _window.OnSelectionChanged -= OnSelectionChanged;
        }
    }
}
