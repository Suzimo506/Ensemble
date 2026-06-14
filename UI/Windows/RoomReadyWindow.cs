using System;
using System.Linq;
using LocalizeLib;
using MDEN.Managers;
using MDEN.UI.Core;
using MelonLoader;
using PopupLib.UI.Components;
using PopupLib.UI.Windows;
using UnityEngine;

namespace MDEN.UI.Windows
{
    public class RoomReadyWindow : MDENWindowBase
    {
        private ForumWindow _window;
        private ForumObject _btnMain;
        private ForumObject _btnContinue;
        private ForumObject _btnBack;
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
            _lastSelectedIndex = -1;

            var lobby = LobbyManager.CurrentLobby;
            var item = lobby?.Playlist?.FirstOrDefault();
            var entry = ChartManager.ParseEntry(item);
            var readyCount = lobby?.ReadyPlayers?.Length ?? 0;
            var playerCount = lobby?.Players?.Length ?? 0;
            var description = entry == null
                ? "歌曲列表为空"
                : $"{entry.DisplayName}\n难度: {entry.Difficulty}\n准备: {readyCount}/{playerCount}";

            _btnMain = new ForumObject(new LocalString(GetMainButtonText()), new LocalString(description));
            _btnMain.Texture = ResourceManager.GetRandomBannerTexture() ?? ResourceManager.GetSprite("RoomList.png")?.texture;
            _window.ForumObjects.Add(_btnMain);

            if (lobby?.HostUid == PlayerManager.CurrentUid && lobby.IsPlaying)
            {
                _btnContinue = new ForumObject(new LocalString("继续下一首"), new LocalString("移除已完成歌曲并进入下一轮准备"));
                _btnContinue.Texture = ResourceManager.GetRandomBannerTexture() ?? ResourceManager.GetSprite("HomePanel.png")?.texture;
                _window.ForumObjects.Add(_btnContinue);
            }

            _btnBack = new ForumObject(new LocalString("返回"), new LocalString("关闭准备面板"));
            _btnBack.Texture = ResourceManager.GetRandomBannerTexture() ?? ResourceManager.GetSprite("OptionsPanel.png")?.texture;
            _window.ForumObjects.Add(_btnBack);
        }

        private string GetMainButtonText()
        {
            var lobby = LobbyManager.CurrentLobby;
            if (lobby == null) return "未进入房间";
            if (lobby.HostUid == PlayerManager.CurrentUid && !lobby.Locked) return "开始准备";
            if (lobby.IsPlaying) return "游戏中";
            return PlaylistManager.IsLocalPlayerReady() ? "取消准备" : "准备";
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
                Close();
                return;
            }

            if (button == _btnMain)
            {
                await HandleMainActionAsync();
            }
            else if (button == _btnContinue)
            {
                await ExecuteAsync("Continuing playlist...", PlaylistManager.ContinueAsync);
            }
        }

        private async System.Threading.Tasks.Task HandleMainActionAsync()
        {
            var lobby = LobbyManager.CurrentLobby;
            if (lobby == null) return;

            if (lobby.HostUid == PlayerManager.CurrentUid && !lobby.Locked)
            {
                await ExecuteAsync("Starting lobby...", PlaylistManager.StartPrepareAsync);
                return;
            }

            if (!lobby.IsPlaying)
            {
                await ExecuteAsync("Setting ready...", () => PlaylistManager.SetReadyAsync(!PlaylistManager.IsLocalPlayerReady()));
            }
        }

        private async System.Threading.Tasks.Task ExecuteAsync(string lockText, Func<System.Threading.Tasks.Task> action)
        {
            using var _ = UIManager.LockUI(lockText);
            try
            {
                await action();
                MainThreadDispatcher.Enqueue(RebuildWindow);
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Room ready action failed: {ex.Message}");
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
                text.text = "准备";
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
    }
}
