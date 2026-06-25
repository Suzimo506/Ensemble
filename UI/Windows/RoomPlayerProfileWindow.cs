using System;
using System.Threading.Tasks;
using MDEN.Managers;
using MDEN.Protocol.Enums;
using MDEN.Protocol.Messages.Player;
using MDEN.Protocol.Models;
using MDEN.UI.Core;
using MelonLoader;
using UnityEngine;
using UnityEngine.UI;

namespace MDEN.UI.Windows
{
    public class RoomPlayerProfileWindow : MDENWindowBase
    {
        private readonly PlayerSyncEntry _player;
        private NativeListWindow _window;
        private NativeListItem _btnAddFriend;
        private GetPlayerResponse _profile;
        private int _lastSelectedIndex = -1;

        public RoomPlayerProfileWindow(PlayerSyncEntry player)
        {
            _player = player ?? new PlayerSyncEntry();
        }

        public override async void Show()
        {
            await LoadProfileForInitialShowAsync();
            if (IsDisposed) return;

            MainThreadDispatcher.Enqueue(ShowLoadedWindow);
        }

        private void ShowLoadedWindow()
        {
            if (IsDisposed) return;

            _window = new NativeListWindow();
            _window.AutoReset = true;
            BuildList();
            _window.OnSelectionChanged += OnSelectionChanged;
            _window.Title = EscapeRichText(GetDisplayName());
            _window.Show();

            RegisterEventCleanup(() =>
            {
                UnbindWindowEvents();
            });
        }

        private async Task LoadProfileForInitialShowAsync()
        {
            if (string.IsNullOrWhiteSpace(_player.Uid)) return;

            try
            {
                _profile = _player.Uid == PlayerManager.CurrentUid
                    ? await PlayerManager.GetMyProfileAsync()
                    : await PlayerManager.GetProfileAsync(_player.Uid);
            }
            catch (Exception ex)
            {
                MDEN.Managers.ClientLogManager.Warning($"Load player profile failed: {_player.Uid}, {ex.Message}");
            }
        }

        private void BuildList()
        {
            _window.Items.Clear();
            _btnAddFriend = AddButton("- 添加好友 -", BuildDetails());
        }

        private NativeListItem AddButton(string title, string description)
        {
            var button = new NativeListItem(title, description);
            button.Texture = GetPlayerAvatarTexture();
            _window.Items.Add(button);
            return button;
        }

        private void OnSelectionChanged(INativeListWindow window, int objectIndex)
        {
            if (_window == null || objectIndex < 0 || objectIndex >= _window.Items.Count) return;

            if (_lastSelectedIndex != objectIndex)
            {
                _lastSelectedIndex = objectIndex;
                return;
            }

            var button = _window.Items[objectIndex];
            if (button == _btnAddFriend)
            {
                if (string.IsNullOrWhiteSpace(_player.Uid) || _player.Uid == PlayerManager.CurrentUid)
                {
                    Il2CppAssets.Scripts.UI.Controls.ShowText.ShowInfo("目标玩家无效");
                    return;
                }

                _ = SendFriendRequestAsync();
            }
        }

        private async Task SendFriendRequestAsync()
        {
            IDisposable uiLock = WindowStackController.LockUI("处理中...");
            try
            {
                var response = await SocialManager.SendFriendRequestAsync(_player.Uid);
                MainThreadDispatcher.Enqueue(() =>
                    Il2CppAssets.Scripts.UI.Controls.ShowText.ShowInfo(GetFriendActionMessage(response?.Action ?? 0)));
            }
            catch (Exception ex)
            {
                MDEN.Managers.ClientLogManager.Warning($"Friend action failed: {_player.Uid}, {ex.Message}");
                MainThreadDispatcher.Enqueue(() => Il2CppAssets.Scripts.UI.Controls.ShowText.ShowInfo(ex.Message));
            }
            finally
            {
                await MainThreadDispatcher.InvokeAsync(() => uiLock?.Dispose());
            }
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

        private void UnbindWindowEvents()
        {
            if (_window == null) return;
            _window.OnSelectionChanged -= OnSelectionChanged;
        }

        private string BuildDetails()
        {
            return $"头衔\n{EscapeRichText(GetDisplayTitle())}\n\n" +
                $"UID\n{EscapeRichText(_player.Uid)}\n\n" +
                $"状态\n{GetStatusText(_player.Status)}\n\n" +
                $"名字颜色\n{EscapeRichText(GetDisplayColor())}\n\n" +
                $"入场提示\n{EscapeRichText(GetDisplayEntranceMessage())}\n\n" +
                $"个人介绍\n{EscapeRichText(GetDisplayBio())}";
        }

        private string GetDisplayName()
        {
            if (!string.IsNullOrWhiteSpace(_profile?.Name)) return _profile.Name;
            return string.IsNullOrWhiteSpace(_player.Name) ? _player.Uid ?? "玩家资料" : _player.Name;
        }

        private string GetDisplayTitle()
        {
            if (!string.IsNullOrWhiteSpace(_profile?.Title)) return _profile.Title;
            return string.IsNullOrWhiteSpace(_player.Title) ? "暂无头衔" : _player.Title;
        }

        private string GetDisplayBio()
        {
            return string.IsNullOrWhiteSpace(_profile?.Bio) ? "暂无介绍" : _profile.Bio;
        }

        private string GetDisplayEntranceMessage()
        {
            return string.IsNullOrWhiteSpace(_profile?.EntranceMessage) ? "暂无入场提示" : _profile.EntranceMessage;
        }

        private string GetDisplayColor()
        {
            var color = _profile?.ChatColor?.Trim().TrimStart('#');
            return string.IsNullOrWhiteSpace(color) ? "ffffff" : color;
        }

        private Texture2D GetPlayerAvatarTexture()
        {
            if (_profile != null)
            {
                return AvatarManager.GetAvatarTexture(_profile.Uid, _profile.AvatarName, _profile.AvatarData);
            }

            return AvatarManager.GetAvatarTexture(_player?.Uid, _player?.AvatarName, _player?.AvatarData);
        }

        private static string GetStatusText(byte status)
        {
            switch ((PlayerStatus)status)
            {
                case PlayerStatus.Online:
                    return "在线";
                case PlayerStatus.InLobby:
                    return "房间中";
                case PlayerStatus.InBattle:
                    return "游戏中";
                default:
                    return "未知";
            }
        }

        private static string EscapeRichText(string value)
        {
            return value?.Replace("<", "＜").Replace(">", "＞") ?? string.Empty;
        }

        private void RebuildWindow()
        {
            if (_window == null) return;

            _window.OnSelectionChanged -= OnSelectionChanged;
            _window.ForceClose();
            _window = new NativeListWindow();
            _window.AutoReset = true;
            BuildList();
            _window.Title = EscapeRichText(GetDisplayName());
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
    }
}
