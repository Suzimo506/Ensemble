using LocalizeLib;
using Il2CppAssets.Scripts.UI.Controls;
using MDEN.Managers;
using MDEN.UI.Core;
using MelonLoader;
using PopupLib.UI.Components;
using PopupLib.UI.Windows;
using UnityEngine;

namespace MDEN.UI.Windows
{
    public class RoomPlaylistWindow : MDENWindowBase
    {
        private ForumWindow _window;
        private PlaylistEntryViewModel[] _items = new PlaylistEntryViewModel[0];
        private int _lastSelectedIndex = -1;

        public override void Show()
        {
            _window = new ForumWindow();
            _window.AutoReset = true;
            BuildList();
            _window.OnSelectionChanged += OnSelectionChanged;
            _window.OnInternalShow += OnInternalShowInjectTitle;
            _window.Show();

            RegisterEventCleanup(() =>
            {
                if (_window != null)
                {
                    _window.OnSelectionChanged -= OnSelectionChanged;
                    _window.OnInternalShow -= OnInternalShowInjectTitle;
                }
            });
        }

        private void BuildList()
        {
            _window.ForumObjects.Clear();

            var lobby = LobbyManager.CurrentLobby;
            _items = PlaylistManager.GetPlaylistItems();
            if (lobby?.Playlist == null || _items.Length == 0)
            {
                AddButton("暂无歌曲", "在选歌界面点击 Start 可加入歌曲列表");
                return;
            }

            for (var i = 0; i < _items.Length; i++)
            {
                var item = _items[i];
                AddButton($"#{i + 1} {item.DisplayName}", $"难度: {item.Difficulty}\n添加者: {item.OwnerName}");
            }
        }

        private ForumObject AddButton(string title, string desc)
        {
            var obj = new ForumObject(new LocalString(title), new LocalString(desc));
            obj.Texture = ResourceManager.GetRandomBannerTexture() ?? ResourceManager.GetSprite("RoomList.png")?.texture;
            _window.ForumObjects.Add(obj);
            return obj;
        }

        private async void OnSelectionChanged(PopupLib.UI.Windows.Interfaces.IListWindow window, int objectIndex)
        {
            if (_window == null || objectIndex < 0 || objectIndex >= _window.ForumObjects.Count) return;
            if (_lastSelectedIndex != objectIndex)
            {
                _lastSelectedIndex = objectIndex;
                return;
            }

            if (!PlaylistManager.CanChangePlaylist || objectIndex >= _items.Length) return;

            using var _ = UIManager.LockUI("Removing playlist entry...");
            try
            {
                await PlaylistManager.RemoveAsync(_items[objectIndex].Entry);
                if (IsDisposed) return;

                MainThreadDispatcher.Enqueue(() =>
                {
                    if (IsDisposed) return;
                    ShowText.ShowInfo("成功移除歌曲列表");
                    RebuildWindow();
                });
            }
            catch (System.Exception ex)
            {
                MelonLogger.Warning($"Remove playlist entry failed: {ex.Message}");
                MainThreadDispatcher.Enqueue(() => ShowText.ShowInfo(ex.Message));
            }
        }

        private void OnInternalShowInjectTitle(PopupLib.UI.Windows.Abstract.BaseWindow w)
        {
            var uiForward = GameObject.Find("UI/Forward");
            var pnlBulletin = uiForward?.transform.Find("Tips/PnlBulletinNew");
            var imgBase = pnlBulletin?.Find("ImgBase");
            var txtTitleObj = pnlBulletin?.Find("TxtTittle");
            if (imgBase == null || txtTitleObj == null) return;

            var oldTitle = imgBase.Find("MDENTitle");
            if (oldTitle != null) UnityEngine.Object.Destroy(oldTitle.gameObject);

            var newTitle = GameObject.Instantiate(txtTitleObj.gameObject, imgBase);
            newTitle.name = "MDENTitle";
            newTitle.SetActive(true);

            var loc = newTitle.GetComponent<Il2CppAssets.Scripts.PeroTools.GeneralLocalization.Localization>();
            if (loc != null) UnityEngine.Object.Destroy(loc);

            var text = newTitle.GetComponent<UnityEngine.UI.Text>();
            if (text != null)
            {
                text.text = "歌曲列表";
                text.alignment = TextAnchor.MiddleCenter;
            }
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
    }
}
