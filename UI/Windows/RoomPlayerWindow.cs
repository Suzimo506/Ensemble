using LocalizeLib;
using MDEN.Managers;
using MDEN.Protocol.Models;
using MDEN.UI.Core;
using MelonLoader;
using PopupLib.UI.Components;
using PopupLib.UI.Windows;
using System;
using System.Linq;
using UnityEngine;
using Il2CppAssets.Scripts.UI.Controls;

namespace MDEN.UI.Windows
{
    public class RoomPlayerWindow : MDENWindowBase
    {
        private readonly PlayerSyncEntry _player;
        private ForumWindow _window;
        private ForumObject _btnBack;
        private ForumObject _btnAddFriend;
        private ForumObject _btnKick;
        private ForumObject _btnTransferHost;
        private ForumObject _btnBanChartSelect;
        private ForumObject _btnMute;
        private ForumObject _btnInfo;
        private int _lastSelectedIndex = -1;
        private double? _ratingLevel;
        private bool _ratingLevelLoaded;

        public RoomPlayerWindow(PlayerSyncEntry player)
        {
            _player = player;
        }

        public override async void Show()
        {
            await LoadRatingLevelForInitialShowAsync();
            if (IsDisposed) return;

            MainThreadDispatcher.Enqueue(ShowLoadedWindow);
        }

        private void ShowLoadedWindow()
        {
            if (IsDisposed) return;

            _window = new ForumWindow();
            _window.AutoReset = true;
            BuildList();
            LobbyManager.CurrentLobbyChanged += HandleCurrentLobbyChanged;
            _window.OnSelectionChanged += OnSelectionChanged;
            _window.OnInternalShow += OnInternalShowInjectTitle;
            _window.Show();
            _lastSelectedIndex = -1;

            RegisterEventCleanup(() =>
            {
                LobbyManager.CurrentLobbyChanged -= HandleCurrentLobbyChanged;
                UnbindWindowEvents();
                RemoveInjectedTitle();
            });
        }

        private void HandleCurrentLobbyChanged(MDEN.Protocol.Messages.Lobby.LobbySyncPush lobby)
        {
            MainThreadDispatcher.Enqueue(() =>
            {
                if (IsDisposed) return;

                if (lobby == null || !IsPlayerStillInLobby(lobby))
                {
                    Close();
                    WindowStackController.OpenWindow(lobby == null ? new RoomListWindow() : new MyRoomWindow());
                }
                else
                {
                    RebuildWindow();
                }
            });
        }

        private void BuildList()
        {
            _window.ForumObjects.Clear();

            _btnBack = CreateButton("- 返回 -", "回到我的房间");
            _btnInfo = CreateButton(GetDisplayName(), PlayerInfoDescriptionFormatter.Build(_player, _ratingLevel, _ratingLevelLoaded));
            _btnAddFriend = CreateButton("添加好友", $"向 {GetDisplayName()} 发送好友请求");
            _btnKick = CreateButton("踢出", "将该玩家踢出房间");
            _btnTransferHost = CreateButton("移交房主", "将房主权限移交给该玩家");
            _btnBanChartSelect = CreateButton(
                IsChartSelectBanned() ? "解除选谱限制" : "禁止选谱",
                IsChartSelectBanned() ? "允许该玩家选择谱面" : "禁止该玩家选择谱面");
            _btnMute = CreateButton(
                IsMuted() ? "解除禁言" : "禁言",
                IsMuted() ? "允许该玩家发送聊天消息" : "禁止该玩家发送聊天消息");
        }

        private ForumObject CreateButton(string title, string description)
        {
            var button = new ForumObject(new LocalString(title), new LocalString(description));
            button.Texture = ResourceManager.GetRandomBannerTexture() ?? ResourceManager.GetSprite("PlayerCard.png")?.texture;
            _window.ForumObjects.Add(button);
            return button;
        }

        private void OnSelectionChanged(PopupLib.UI.Windows.Interfaces.IListWindow window, int objectIndex)
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
                WindowStackController.OpenWindow(new MyRoomWindow());
                return;
            }

            if (button == _btnAddFriend)
            {
                if (string.IsNullOrWhiteSpace(_player?.Uid) || _player.Uid == PlayerManager.CurrentUid)
                {
                    ShowText.ShowInfo("目标玩家无效");
                    return;
                }

                ConfirmAndRun(
                    "添加好友",
                    $"确认向 {GetDisplayName()} 发送好友请求吗？",
                    () => SendFriendRequestAsync(),
                    false);
                return;
            }

            if (button == _btnInfo)
            {
                Close();
                WindowStackController.OpenWindow(new RoomPlayerProfileWindow(_player));
                return;
            }

            if (!CanUseHostAction(out var reason))
            {
                ShowText.ShowInfo(reason);
                return;
            }

            if (button == _btnKick)
            {
                ConfirmAndRun("踢出玩家", $"确认将 {GetDisplayName()} 踢出房间吗？", () => LobbyManager.KickPlayerAsync(_player.Uid));
            }
            else if (button == _btnTransferHost)
            {
                ConfirmAndRun("移交房主", $"确认将房主移交给 {GetDisplayName()} 吗？", () => LobbyManager.TransferHostAsync(_player.Uid));
            }
            else if (button == _btnBanChartSelect)
            {
                var banned = !IsChartSelectBanned();
                ConfirmAndRun(
                    banned ? "禁止选谱" : "解除选谱限制",
                    banned ? $"确认禁止 {GetDisplayName()} 选谱吗？" : $"确认允许 {GetDisplayName()} 选谱吗？",
                    () => LobbyManager.SetChartSelectBannedAsync(_player.Uid, banned));
            }
            else if (button == _btnMute)
            {
                var muted = !IsMuted();
                ConfirmAndRun(
                    muted ? "禁言" : "解除禁言",
                    muted ? $"确认禁言 {GetDisplayName()} 吗？" : $"确认解除 {GetDisplayName()} 的禁言吗？",
                    () => LobbyManager.SetMutedAsync(_player.Uid, muted));
            }
        }

        private bool CanUseHostAction(out string reason)
        {
            reason = null;
            var lobby = LobbyManager.CurrentLobby;
            if (lobby == null)
            {
                reason = "当前不在房间中";
                return false;
            }

            if (lobby.HostUid != PlayerManager.CurrentUid)
            {
                reason = "只有房主可以使用该操作";
                return false;
            }

            if (string.IsNullOrWhiteSpace(_player?.Uid) || _player.Uid == PlayerManager.CurrentUid)
            {
                reason = "不能对自己使用该操作";
                return false;
            }

            if (lobby.Locked || lobby.IsPlaying)
            {
                reason = "游戏准备或进行中，不能使用该操作";
                return false;
            }

            return true;
        }

        private void ConfirmAndRun(string title, string message, Func<System.Threading.Tasks.Task> action, bool reopenRoom = true)
        {
            NativeConfirmDialog.Show(title, message, confirmed =>
            {
                if (!confirmed) return;
                _ = RunWindowActionAsync(action, reopenRoom);
            });
        }

        private async System.Threading.Tasks.Task RunWindowActionAsync(Func<System.Threading.Tasks.Task> action, bool reopenRoom)
        {
            using var _ = WindowStackController.LockUI("处理中...");
            try
            {
                await action();
                if (reopenRoom)
                {
                    MainThreadDispatcher.Enqueue(() =>
                    {
                        if (IsDisposed) return;
                        Close();
                        WindowStackController.OpenWindow(new MyRoomWindow());
                    });
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Room player operation failed: {ex.Message}");
                MainThreadDispatcher.Enqueue(() => ShowText.ShowInfo(ex.Message));
            }
        }

        private async System.Threading.Tasks.Task SendFriendRequestAsync()
        {
            var response = await SocialManager.SendFriendRequestAsync(_player.Uid);
            MainThreadDispatcher.Enqueue(() => ShowText.ShowInfo(GetFriendActionMessage(response?.Action ?? 0)));
        }

        private static string GetFriendActionMessage(int action)
        {
            return action switch
            {
                1 => "好友请求已发送",
                2 => "已添加好友",
                3 => "已删除好友",
                4 => "已取消好友请求",
                _ => "好友状态未变化"
            };
        }

        private async System.Threading.Tasks.Task LoadRatingLevelForInitialShowAsync()
        {
            var uid = _player?.Uid;
            if (string.IsNullOrWhiteSpace(uid))
            {
                _ratingLevelLoaded = true;
                return;
            }

            try
            {
                _ratingLevel = await MuseDashMoeProfileManager.GetRatingLevelAsync(uid);
            }
            finally
            {
                _ratingLevelLoaded = true;
            }
        }

        private string GetDisplayName()
        {
            return string.IsNullOrEmpty(_player?.Name) ? "玩家信息" : _player.Name;
        }

        private bool IsMuted()
        {
            return ContainsUid(LobbyManager.CurrentLobby?.MutedPlayers, _player?.Uid);
        }

        private bool IsChartSelectBanned()
        {
            return ContainsUid(LobbyManager.CurrentLobby?.ChartSelectBannedPlayers, _player?.Uid);
        }

        private static bool ContainsUid(string[] uids, string uid)
        {
            return !string.IsNullOrWhiteSpace(uid) &&
                   uids != null &&
                   uids.Any(item => string.Equals(item, uid, StringComparison.Ordinal));
        }

        private bool IsPlayerStillInLobby(MDEN.Protocol.Messages.Lobby.LobbySyncPush lobby)
        {
            return !string.IsNullOrWhiteSpace(_player?.Uid) &&
                   lobby?.Players != null &&
                   lobby.Players.Any(uid => string.Equals(uid, _player.Uid, StringComparison.Ordinal));
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

            var titleInScroll = GameObject.Find("UI/Forward/Tips/PnlBulletinNew/ImgBase/ScrollView/MDENTitle");
            if (titleInScroll != null) UnityEngine.Object.Destroy(titleInScroll);
        }

        private void UnbindWindowEvents()
        {
            if (_window == null) return;

            _window.OnSelectionChanged -= OnSelectionChanged;
            _window.OnInternalShow -= OnInternalShowInjectTitle;
        }

        public override void Close()
        {
            _lastSelectedIndex = -1;
            UnbindWindowEvents();
            RemoveInjectedTitle();
            if (_window != null)
            {
                _window.ForceClose();
                _window = null;
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
    }
}
