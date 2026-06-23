using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using LocalizeLib;
using MDEN.Managers;
using MDEN.UI.Core;
using PopupLib.UI.Components;
using PopupLib.UI.Windows;
using ShowText = Il2CppAssets.Scripts.UI.Controls.ShowText;
using UnityEngine;

namespace MDEN.UI.Windows
{
    public class AvatarPickerWindow : MDENWindowBase
    {
        private ForumWindow _window;
        private ForumObject _btnBack;
        private readonly Dictionary<ForumObject, AvatarLibraryItem> _avatarItems =
            new Dictionary<ForumObject, AvatarLibraryItem>();
        private int _lastSelectedIndex = -1;

        public override void Show()
        {
            ModConfigManager.LoadConfig();
            EnsureAvatarLibraryFolder();

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
        }

        private void BuildList()
        {
            _window.ForumObjects.Clear();
            _avatarItems.Clear();

            _btnBack = AddButton("- 返回 -", "回到个人信息");

            var items = AvatarManager.GetAvatarLibraryItems(false);
            foreach (var item in items)
            {
                var title = item.IsDefault ? "默认头像" : item.DisplayName;
                var description = item.IsDefault
                    ? "使用 Ensemble 默认头像"
                    : $"使用这个头像\n{EscapeRichText(Path.GetFileName(item.Path))}";
                var button = AddButton(title, description);
                _avatarItems[button] = item;
            }

            if (items.Length <= 1)
            {
                AddButton("暂无头像文件", BuildEmptyGuideText());
            }
        }

        private ForumObject AddButton(string title, string description)
        {
            var button = new ForumObject(new LocalString(title), new LocalString(description));
            button.Texture = GetListBannerTexture();
            _window.ForumObjects.Add(button);
            return button;
        }

        private static Texture2D GetListBannerTexture()
        {
            return ResourceManager.GetRandomBannerTexture() ??
                   ResourceManager.GetSprite("PlayerCard.png")?.texture;
        }

        private static string BuildEmptyGuideText()
        {
            return $"<color={Constants.ColorGreen}>把 png、jpg 或 jpeg 图片放入头像文件夹后，重新进入这个页面即可选择。\n" +
                   $"当前头像文件夹：{EscapeRichText(AvatarManager.GetAvatarLibraryFolder())}</color>";
        }

        private async void OnSelectionChanged(PopupLib.UI.Windows.Interfaces.IListWindow window, int objectIndex)
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
                GoBack();
                return;
            }

            if (_avatarItems.TryGetValue(button, out var item))
            {
                await SelectAvatarAsync(item);
            }
        }

        private async Task SelectAvatarAsync(AvatarLibraryItem item)
        {
            IDisposable uiLock = WindowStackController.LockUI("Saving avatar...");
            try
            {
                var avatarName = AvatarManager.ImportAvatarFromLibraryItem(item);
                await PlayerManager.UpdateAvatarAsync(avatarName);
                await TrySyncLocalProfileToServerAsync();
                ShowText.ShowInfo("头像已更新");
                GoBack();
            }
            catch (Exception ex)
            {
                ClientLogManager.Warning($"Select avatar failed: {ex.Message}");
                ShowText.ShowInfo(ex.Message);
                MainThreadDispatcher.Enqueue(RebuildWindow);
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
                    ClientLogManager.Warning($"Sync avatar after save failed: {ex.Message}");
                }
            }
        }

        private static void EnsureAvatarLibraryFolder()
        {
            try
            {
                Directory.CreateDirectory(AvatarManager.GetAvatarLibraryFolder());
            }
            catch (Exception ex)
            {
                ClientLogManager.Warning($"Ensure avatar library folder failed: {ex.Message}");
            }
        }

        private void RebuildWindow()
        {
            if (_window == null || IsDisposed) return;

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

        private void GoBack()
        {
            Close();
            WindowStackController.OpenWindow(new ProfileWindow());
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
            newTitle.name = "MDENAvatarPickerTitle";
            newTitle.SetActive(true);

            var loc = newTitle.GetComponent<Il2CppAssets.Scripts.PeroTools.GeneralLocalization.Localization>();
            if (loc != null) UnityEngine.Object.Destroy(loc);

            var text = newTitle.GetComponent<UnityEngine.UI.Text>();
            if (text != null)
            {
                text.text = "选择头像";
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

        private static void RemoveInjectedTitle()
        {
            var panel = GameObject.Find("UI/Forward/Tips/PnlBulletinNew");
            if (panel == null) return;

            var titleTrans = panel.transform.Find("ImgBase/ScrollView/MDENAvatarPickerTitle");
            if (titleTrans == null) titleTrans = panel.transform.Find("ImgBase/MDENAvatarPickerTitle");
            if (titleTrans != null) UnityEngine.Object.Destroy(titleTrans.gameObject);
        }

        private static string EscapeRichText(string value)
        {
            return value?.Replace("<", "＜").Replace(">", "＞") ?? string.Empty;
        }

        public override void Close()
        {
            _lastSelectedIndex = -1;
            RemoveInjectedTitle();
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
