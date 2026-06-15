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
    public class RoomPlayerWindow : MDENWindowBase
    {
        private readonly PlayerSyncEntry _player;
        private ForumWindow _window;
        private ForumObject _btnBack;
        private ForumObject _btnKick;
        private ForumObject _btnTransferHost;
        private ForumObject _btnBanChartSelect;
        private ForumObject _btnMute;
        private ForumObject _btnInfo;
        private int _lastSelectedIndex = -1;

        public RoomPlayerWindow(PlayerSyncEntry player)
        {
            _player = player;
        }

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

            _btnBack = CreateButton("返回", "回到我的房间");
            _btnKick = CreateButton("踢出", "将该玩家踢出房间");
            _btnTransferHost = CreateButton("移交房主", "将房主权限移交给该玩家");
            _btnBanChartSelect = CreateButton("禁止选谱", "禁止该玩家选择谱面");
            _btnMute = CreateButton("禁言", "禁止该玩家发送聊天消息");

            _btnInfo = CreateButton(
                string.IsNullOrEmpty(_player?.Name) ? "玩家信息" : _player.Name,
                $"UID: {_player?.Uid}\nPing: {_player?.PingMS ?? 0}ms\n状态: {_player?.Status ?? 0}");
        }

        private ForumObject CreateButton(string title, string description)
        {
            var button = new ForumObject(new LocalString(title), new LocalString(description));
            button.Texture = ResourceManager.GetRandomBannerTexture() ?? ResourceManager.GetSprite("PlayerCard.png")?.texture;
            _window.ForumObjects.Add(button);
            return button;
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
                UIManager.OpenWindow(new MyRoomWindow());
                return;
            }

            if (button == _btnInfo) return;

            if (!IsHost())
            {
                MelonLogger.Warning("No permission for room owner action.");
                return;
            }

            if (button == _btnKick)
            {
                await ExecuteOwnerActionAsync("Kick player", () => LobbyManager.KickPlayerAsync(_player.Uid));
            }
            else if (button == _btnTransferHost)
            {
                await ExecuteOwnerActionAsync("Transfer host", () => LobbyManager.TransferHostAsync(_player.Uid));
            }
            else if (button == _btnBanChartSelect)
            {
                await ExecuteOwnerActionAsync("Ban chart select", () => LobbyManager.SetChartSelectBannedAsync(_player.Uid, true));
            }
            else if (button == _btnMute)
            {
                await ExecuteOwnerActionAsync("Mute player", () => LobbyManager.SetMutedAsync(_player.Uid, true));
            }
        }

        private static bool IsHost()
        {
            return LobbyManager.CurrentLobby?.HostUid == PlayerManager.CurrentUid;
        }

        private async Task ExecuteOwnerActionAsync(string actionName, Func<Task> action)
        {
            if (string.IsNullOrEmpty(_player?.Uid)) return;

            using var _ = UIManager.LockUI(actionName);
            try
            {
                await action.Invoke();
                if (IsDisposed) return;

                Close();
                UIManager.OpenWindow(new MyRoomWindow());
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"{actionName} failed: {ex.Message}");
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
                text.text = "玩家信息";
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

        private void RemoveInjectedTitle()
        {
            var title = GameObject.Find("UI/Forward/Tips/PnlBulletinNew/ImgBase/MDENTitle");
            if (title != null) UnityEngine.Object.Destroy(title);
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
