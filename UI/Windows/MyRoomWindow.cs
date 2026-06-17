using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using LocalizeLib;
using MDEN.Managers;
using MDEN.Protocol.Enums;
using MDEN.Protocol.Models;
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
        private ForumObject _btnJoinLock;
        private ForumObject _btnPassword;
        private readonly Dictionary<ForumObject, PlayerSyncEntry> _playerItems = new Dictionary<ForumObject, PlayerSyncEntry>();
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
            _playerItems.Clear();
            _lastSelectedIndex = -1;

            var lobby = LobbyManager.CurrentLobby;
            var summary = lobby == null
                ? "Not in lobby."
                : BuildRoomSummary(lobby);

            _btnLeave = new ForumObject(new LocalString("- 退出房间 -"), new LocalString("离开当前联机房间"));
            _btnLeave.Texture = ResourceManager.GetRandomBannerTexture() ?? ResourceManager.GetSprite("HomePanel.png")?.texture;
            _window.ForumObjects.Add(_btnLeave);

            if (lobby?.HostUid == PlayerManager.CurrentUid)
            {
                _btnJoinLock = new ForumObject(
                    new LocalString(lobby.JoinLocked ? "- 手动解锁 -" : "- 手动上锁 -"),
                    new LocalString(lobby.JoinLocked ? "解锁后其他玩家可以加入房间" : "上锁后其他玩家不能加入房间"));
                _btnJoinLock.Texture = ResourceManager.GetRandomBannerTexture() ?? ResourceManager.GetSprite("OptionsPanel.png")?.texture;
                _window.ForumObjects.Add(_btnJoinLock);

                _btnPassword = new ForumObject(
                    new LocalString(lobby.IsPrivate ? "- 修改/清除密码 -" : "- 设置密码 -"),
                    new LocalString(lobby.IsPrivate ? "当前房间需要密码加入，输入空内容可清除密码" : "设置后房间列表会显示（私密），加入时需要输入密码"));
                _btnPassword.Texture = ResourceManager.GetRandomBannerTexture() ?? ResourceManager.GetSprite("SocialNetwork.png")?.texture;
                _window.ForumObjects.Add(_btnPassword);
            }
            else
            {
                _btnJoinLock = null;
                _btnPassword = null;
            }

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
                    _playerItems[item] = new PlayerSyncEntry
                    {
                        Uid = player.Uid,
                        Name = displayName,
                        Title = player.Title,
                        ChatColor = player.ChatColor,
                        PingMS = player.PingMS,
                        Status = player.Status
                    };
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
                    _playerItems[item] = new PlayerSyncEntry
                    {
                        Uid = uid,
                        Name = displayName,
                        Title = uid == PlayerManager.CurrentUid ? PlayerManager.CurrentProfile?.Title : null,
                        ChatColor = uid == PlayerManager.CurrentUid ? PlayerManager.CurrentProfile?.ChatColor : null
                    };
                }
            }
        }

        private static string BuildRoomSummary(MDEN.Protocol.Messages.Lobby.LobbySyncPush lobby)
        {
            var hostName = EscapeRichText(lobby.HostName ?? lobby.HostUid ?? "Unknown");
            return $"房主: {Highlight(hostName, Constants.ColorPink)}\n" +
                   $"人数: {Highlight($"{GetPlayerCount(lobby)}/{lobby.MaxPlayers}", Constants.ColorCyan)}\n" +
                   $"歌曲列表: {Highlight($"{GetPlaylistCount(lobby)}/{lobby.PlaylistSize}", Constants.ColorYellow)}\n" +
                   $"获胜方式: {Highlight(GetGoalName(lobby.Goal), Constants.ColorYellow)}\n" +
                   $"结算功能: {Highlight(lobby.SettlementEnabled ? "开启" : "关闭", Constants.ColorYellow)}\n" +
                   $"加入限制: {Highlight(lobby.JoinLocked ? "已上锁" : "开放", lobby.JoinLocked ? Constants.ColorYellow : RoomListWindow.WaitingStatusColor)}\n" +
                   $"密码: {Highlight(lobby.IsPrivate ? "已设置" : "无", lobby.IsPrivate ? Constants.ColorYellow : RoomListWindow.WaitingStatusColor)}\n" +
                   $"状态: {RoomListWindow.GetColoredLobbyStatus(lobby.IsPlaying, lobby.Locked, lobby.JoinLocked)}";
        }

        private static int GetPlayerCount(MDEN.Protocol.Messages.Lobby.LobbySyncPush lobby)
        {
            if (lobby.PlayerDetails != null && lobby.PlayerDetails.Length > 0)
            {
                return lobby.PlayerDetails
                    .Where(player => !string.IsNullOrEmpty(player?.Uid))
                    .Select(player => player.Uid)
                    .Distinct()
                    .Count();
            }

            return lobby.Players?
                .Where(uid => !string.IsNullOrEmpty(uid))
                .Distinct()
                .Count() ?? 0;
        }

        private static int GetPlaylistCount(MDEN.Protocol.Messages.Lobby.LobbySyncPush lobby)
        {
            return lobby.Playlist?
                .Where(entry => !string.IsNullOrEmpty(entry))
                .Distinct()
                .Count() ?? 0;
        }

        private static string GetGoalName(byte goal)
        {
            return (LobbyGoal)goal switch
            {
                LobbyGoal.Score => "分数",
                LobbyGoal.Custom => "自定义",
                _ => "准确率"
            };
        }

        private static string Highlight(string value, string color)
        {
            return $"<color={color}>{value}</color>";
        }

        private static string EscapeRichText(string value)
        {
            return string.IsNullOrEmpty(value)
                ? string.Empty
                : value.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
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
            if (button == _btnLeave)
            {
                NativeConfirmDialog.Show(
                    string.Empty,
                    "是否退出房间？",
                    confirmed =>
                    {
                        if (confirmed)
                        {
                            _ = LeaveLobbyAsync();
                        }
                    });
                return;
            }

            if (button == _btnJoinLock)
            {
                var lobby = LobbyManager.CurrentLobby;
                if (!CanChangeRoomSettings(lobby)) return;

                _ = UpdateLobbySettingsAsync(!lobby.JoinLocked, false, null);
                return;
            }

            if (button == _btnPassword)
            {
                if (!CanChangeRoomSettings(LobbyManager.CurrentLobby)) return;

                ShowPasswordInput();
                return;
            }

            if (_playerItems.TryGetValue(button, out var player))
            {
                Close();
                UIManager.OpenWindow(new RoomPlayerWindow(player));
            }
        }

        private static bool CanChangeRoomSettings(MDEN.Protocol.Messages.Lobby.LobbySyncPush lobby)
        {
            if (lobby == null) return false;
            if (lobby.HostUid == PlayerManager.CurrentUid) return true;

            Il2CppAssets.Scripts.UI.Controls.ShowText.ShowInfo("只有房主可以修改房间设置");
            return false;
        }

        private void ShowPasswordInput()
        {
            if (_window != null)
            {
                _window.ForceClose();
            }

            var input = new InputWindow();
            input.OnCompletion += (w) =>
            {
                var value = input.Result?.Trim();
                if (value != null && value.Length > 16)
                {
                    MelonLoader.MelonLogger.Warning("Lobby password is too long. Max length is 16.");
                    MainThreadDispatcher.Enqueue(RebuildWindow);
                    return;
                }

                _ = UpdateLobbySettingsAsync(LobbyManager.CurrentLobby?.JoinLocked == true, true, value);
            };
            input.Show();
        }

        private async Task UpdateLobbySettingsAsync(bool joinLocked, bool updatePassword, string password)
        {
            using var _ = UIManager.LockUI("处理中...");

            try
            {
                await LobbyManager.SetLobbySettingsAsync(joinLocked, updatePassword, password);
                ApplyLocalLobbySettings(joinLocked, updatePassword, password);
                if (IsDisposed) return;

                MainThreadDispatcher.Enqueue(() =>
                {
                    if (IsDisposed) return;
                    RebuildWindow();
                });
            }
            catch (System.Exception ex)
            {
                MelonLoader.MelonLogger.Warning($"Update lobby settings failed: {ex.Message}");
                MainThreadDispatcher.Enqueue(RebuildWindow);
            }
        }

        private static void ApplyLocalLobbySettings(bool joinLocked, bool updatePassword, string password)
        {
            var lobby = LobbyManager.CurrentLobby;
            if (lobby == null) return;

            lobby.JoinLocked = joinLocked;
            if (updatePassword)
            {
                lobby.IsPrivate = !string.IsNullOrWhiteSpace(password);
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
