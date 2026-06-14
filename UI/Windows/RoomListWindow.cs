using System;
using System.Threading.Tasks;
using LocalizeLib;
using MDEN.Managers;
using MDEN.Protocol.Models;
using MDEN.UI.Core;
using MelonLoader;
using PopupLib.UI.Components;
using PopupLib.UI.Windows;
using UnityEngine;

namespace MDEN.UI.Windows
{
    public class RoomListWindow : MDENWindowBase
    {
        private ForumWindow _window;
        private ForumObject _btnBack;
        private ForumObject _btnRefresh;
        private ForumObject _btnCreateRoom;
        private LobbyListEntry[] _lobbies = new LobbyListEntry[0];
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

            await RefreshLobbiesAsync();
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
                txt.text = "选择房间";
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
            _lastSelectedIndex = -1;

            _btnBack = new ForumObject(new LocalString("返回"), new LocalString("回到节点列表"));
            _btnBack.Texture = ResourceManager.GetRandomBannerTexture() ?? ResourceManager.GetSprite("OptionsPanel.png")?.texture;
            _window.ForumObjects.Add(_btnBack);

            _btnRefresh = new ForumObject(new LocalString("刷新"), new LocalString("重新获取当前服务器的房间列表"));
            _btnRefresh.Texture = ResourceManager.GetRandomBannerTexture() ?? ResourceManager.GetSprite("RoomList.png")?.texture;
            _window.ForumObjects.Add(_btnRefresh);

            _btnCreateRoom = new ForumObject(new LocalString("创建房间"), new LocalString("创建新的联机房间"));
            _btnCreateRoom.Texture = ResourceManager.GetRandomBannerTexture() ?? ResourceManager.GetSprite("HomePanel.png")?.texture;
            _window.ForumObjects.Add(_btnCreateRoom);

            if (_lobbies.Length == 0)
            {
                var empty = new ForumObject(new LocalString("暂无房间"), new LocalString("当前服务器没有公开房间，可以刷新或创建房间"));
                empty.Texture = ResourceManager.GetRandomBannerTexture() ?? ResourceManager.GetSprite("PlayerCard.png")?.texture;
                _window.ForumObjects.Add(empty);
                return;
            }

            foreach (var lobby in _lobbies)
            {
                var status = lobby.IsPlaying ? "游戏中" : lobby.Locked ? "已锁定" : "等待中";
                var name = $"<color={Constants.ColorYellow}>{lobby.Name}</color>";
                var desc = $"房主: {lobby.HostName ?? lobby.HostUid}\n人数: <color={Constants.ColorCyan}>{lobby.PlayerCount}/{lobby.MaxPlayers}</color>\n状态: {status}";
                var item = new ForumObject(new LocalString(name), new LocalString(desc));
                item.Texture = ResourceManager.GetRandomBannerTexture() ?? ResourceManager.GetSprite("RoomList.png")?.texture;
                _window.ForumObjects.Add(item);
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
            if (button == _btnBack)
            {
                Close();
                UIManager.OpenWindow(LobbyManager.IsInLobby
                    ? new MyRoomWindow()
                    : new ServerSelectionWindow());
            }
            else if (button == _btnRefresh)
            {
                await RefreshLobbiesAsync();
            }
            else if (button == _btnCreateRoom)
            {
                Close();
                UIManager.OpenWindow(new CreateRoomWindow());
            }
            else if (_lobbies.Length == 0)
            {
                MelonLogger.Msg("No lobby selected because lobby list is empty.");
            }
            else
            {
                var lobbyIndex = objectIndex - 3;
                if (lobbyIndex >= 0 && lobbyIndex < _lobbies.Length)
                {
                    var selectedLobby = _lobbies[lobbyIndex];
                    if (LobbyManager.IsInLobby && LobbyManager.CurrentLobby?.Id != selectedLobby.Id)
                    {
                        NativeConfirmDialog.Show(
                            "切换房间",
                            $"确认离开当前房间并加入「{selectedLobby.Name}」吗？",
                            confirmed =>
                            {
                                if (confirmed)
                                {
                                    _ = JoinLobbyAsync(selectedLobby);
                                }
                            });
                        return;
                    }

                    await JoinLobbyAsync(selectedLobby);
                }
            }
        }

        private async Task RefreshLobbiesAsync()
        {
            using var _ = UIManager.LockUI("Fetching lobby list...");

            try
            {
                _lobbies = await LobbyManager.RefreshLobbiesAsync();
                MainThreadDispatcher.Enqueue(RebuildWindow);
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Fetch lobby list failed: {ex.Message}");
            }
        }

        private async Task JoinLobbyAsync(LobbyListEntry lobby)
        {
            using var _ = UIManager.LockUI("Joining lobby...");

            try
            {
                await LobbyManager.JoinLobbyAsync(lobby.Id);
                MelonLogger.Msg($"Joined lobby: {lobby.Id}");
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Join lobby failed: {ex.Message}");
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
