using System.Linq;
using LocalizeLib;
using MDEN.Managers;
using MDEN.UI.Core;
using PopupLib.UI.Components;
using PopupLib.UI.Windows;
using UnityEngine;

namespace MDEN.UI.Windows
{
    public class MyRoomWindow : MDENWindowBase
    {
        private ForumWindow _window;
        private ForumObject _btnLeave;
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
            _lastSelectedIndex = -1;

            var lobby = LobbyManager.CurrentLobby;
            var summary = lobby == null
                ? "Not in lobby."
                : $"房主: {lobby.HostName ?? lobby.HostUid}\n人数: {lobby.Players?.Length ?? 0}/{lobby.MaxPlayers}\n状态: {(lobby.IsPlaying ? "游戏中" : "等待中")}";

            _btnLeave = new ForumObject(new LocalString("退出房间"), new LocalString("离开当前联机房间"));
            _btnLeave.Texture = ResourceManager.GetRandomBannerTexture() ?? ResourceManager.GetSprite("HomePanel.png")?.texture;
            _window.ForumObjects.Add(_btnLeave);

            var info = new ForumObject(new LocalString(lobby?.Name ?? "我的房间"), new LocalString(summary));
            info.Texture = ResourceManager.GetRandomBannerTexture() ?? ResourceManager.GetSprite("RoomList.png")?.texture;
            _window.ForumObjects.Add(info);

            if (lobby?.PlayerDetails != null && lobby.PlayerDetails.Length > 0)
            {
                foreach (var player in lobby.PlayerDetails)
                {
                    var displayName = string.IsNullOrEmpty(player.Name) ? player.Uid : player.Name;
                    var name = player.Uid == lobby.HostUid
                        ? $"<color={Constants.ColorPink}>{displayName}</color>"
                        : displayName;
                    var item = new ForumObject(new LocalString(name), new LocalString($"UID: {player.Uid}"));
                    item.Texture = ResourceManager.GetRandomBannerTexture() ?? ResourceManager.GetSprite("PlayerCard.png")?.texture;
                    _window.ForumObjects.Add(item);
                }
            }
            else if (lobby?.Players != null)
            {
                foreach (var uid in lobby.Players.Where(uid => !string.IsNullOrEmpty(uid)))
                {
                    var displayName = uid == PlayerManager.CurrentUid && !string.IsNullOrEmpty(PlayerManager.CurrentProfile?.Name)
                        ? PlayerManager.CurrentProfile.Name
                        : uid;
                    var name = uid == lobby.HostUid ? $"<color={Constants.ColorPink}>{displayName}</color>" : displayName;
                    var item = new ForumObject(new LocalString(name), new LocalString($"UID: {uid}"));
                    item.Texture = ResourceManager.GetRandomBannerTexture() ?? ResourceManager.GetSprite("PlayerCard.png")?.texture;
                    _window.ForumObjects.Add(item);
                }
            }
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
            if (button == _btnLeave)
            {
                await LeaveLobbyAsync();
            }
        }

        private async System.Threading.Tasks.Task LeaveLobbyAsync()
        {
            using var _ = UIManager.LockUI("Leaving lobby...");

            try
            {
                await LobbyManager.LeaveLobbyAsync();
                if (IsDisposed) return;

                MainThreadDispatcher.Enqueue(() =>
                {
                    if (IsDisposed) return;
                    Close();
                    NavigationButton.RefreshRoomButton();
                    UIManager.OpenWindow(new RoomListWindow());
                });
            }
            catch (System.Exception ex)
            {
                MelonLoader.MelonLogger.Warning($"Leave lobby failed: {ex.Message}");
            }
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

            var newTitle = UnityEngine.Object.Instantiate(txtTittleObj.gameObject, imgBase);
            newTitle.name = "MDENTitle";
            newTitle.SetActive(true);

            var loc = newTitle.GetComponent<Il2CppAssets.Scripts.PeroTools.GeneralLocalization.Localization>();
            if (loc != null) UnityEngine.Object.Destroy(loc);

            var txt = newTitle.GetComponent<UnityEngine.UI.Text>();
            if (txt != null)
            {
                txt.text = "我的房间";
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
