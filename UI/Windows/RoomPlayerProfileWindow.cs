using System;
using System.Threading.Tasks;
using LocalizeLib;
using MDEN.Managers;
using MDEN.Protocol.Enums;
using MDEN.Protocol.Messages.Player;
using MDEN.Protocol.Models;
using MDEN.UI.Core;
using MelonLoader;
using PopupLib.UI.Components;
using PopupLib.UI.Windows;
using UnityEngine;
using UnityEngine.UI;

namespace MDEN.UI.Windows
{
    public class RoomPlayerProfileWindow : MDENWindowBase
    {
        private const string InjectedTitleName = "MDENPlayerProfileTitle";

        private readonly PlayerSyncEntry _player;
        private ForumWindow _window;
        private ForumObject _btnAddFriend;
        private GetPlayerResponse _profile;
        private int _lastSelectedIndex = -1;

        public RoomPlayerProfileWindow(PlayerSyncEntry player)
        {
            _player = player ?? new PlayerSyncEntry();
        }

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
                UnbindWindowEvents();
                RemoveInjectedObjects();
            });

            _ = LoadProfileAsync();
        }

        private async Task LoadProfileAsync()
        {
            if (string.IsNullOrWhiteSpace(_player.Uid)) return;

            try
            {
                _profile = _player.Uid == PlayerManager.CurrentUid
                    ? await PlayerManager.GetMyProfileAsync()
                    : await PlayerManager.GetProfileAsync(_player.Uid);

                if (IsDisposed) return;
                MainThreadDispatcher.Enqueue(() =>
                {
                    if (IsDisposed || _window == null) return;
                    RebuildWindow();
                });
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Load player profile failed: {_player.Uid}, {ex.Message}");
            }
        }

        private void BuildList()
        {
            _window.ForumObjects.Clear();
            _btnAddFriend = AddButton("- 添加好友 -", BuildDetails());
        }

        private ForumObject AddButton(string title, string description)
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
            using var _ = UIManager.LockUI("处理中...");
            try
            {
                var response = await SocialManager.SendFriendRequestAsync(_player.Uid);
                MainThreadDispatcher.Enqueue(() =>
                    Il2CppAssets.Scripts.UI.Controls.ShowText.ShowInfo(GetFriendActionMessage(response?.Action ?? 0)));
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Friend action failed: {_player.Uid}, {ex.Message}");
                MainThreadDispatcher.Enqueue(() => Il2CppAssets.Scripts.UI.Controls.ShowText.ShowInfo(ex.Message));
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

        private void OnInternalShowInjectTitle(PopupLib.UI.Windows.Abstract.BaseWindow w)
        {
            var uiForward = GameObject.Find("UI/Forward");
            var pnlBulletin = uiForward?.transform.Find("Tips/PnlBulletinNew");
            var imgBase = pnlBulletin?.Find("ImgBase");
            var txtTitleObj = pnlBulletin?.Find("TxtTittle");
            if (imgBase == null || txtTitleObj == null) return;

            RemoveInjectedObjects(imgBase);
            InjectTitle(imgBase, txtTitleObj);
        }

        private void InjectTitle(Transform imgBase, Transform txtTitleObj)
        {
            var newTitle = GameObject.Instantiate(txtTitleObj.gameObject, imgBase);
            newTitle.name = InjectedTitleName;
            newTitle.SetActive(true);

            var loc = newTitle.GetComponent<Il2CppAssets.Scripts.PeroTools.GeneralLocalization.Localization>();
            if (loc != null) UnityEngine.Object.Destroy(loc);

            var text = newTitle.GetComponent<Text>();
            if (text != null)
            {
                text.text = EscapeRichText(GetDisplayName());
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

        private void RemoveInjectedObjects()
        {
            var imgBase = GameObject.Find("UI/Forward/Tips/PnlBulletinNew/ImgBase")?.transform;
            if (imgBase != null) RemoveInjectedObjects(imgBase);
        }

        private void UnbindWindowEvents()
        {
            if (_window == null) return;
            _window.OnSelectionChanged -= OnSelectionChanged;
            _window.OnInternalShow -= OnInternalShowInjectTitle;
        }

        private static void RemoveInjectedObjects(Transform imgBase)
        {
            var oldTitle = imgBase.Find(InjectedTitleName);
            if (oldTitle != null) UnityEngine.Object.Destroy(oldTitle.gameObject);

            var oldTitleInScroll = imgBase.Find("ScrollView/" + InjectedTitleName);
            if (oldTitleInScroll != null) UnityEngine.Object.Destroy(oldTitleInScroll.gameObject);
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
            RemoveInjectedObjects();
            if (_window != null)
            {
                UnbindWindowEvents();
                _window.ForceClose();
                _window = null;
            }
        }
    }
}
