using LocalizeLib;
using MDEN.Managers;
using MDEN.Protocol.Enums;
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
        private const string InjectedDetailsName = "MDENPlayerProfileDetails";

        private readonly PlayerSyncEntry _player;
        private ForumWindow _window;
        private ForumObject _btnBack;
        private ForumObject _btnAddFriend;
        private ForumObject _btnProfile;
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
        }

        private void BuildList()
        {
            _window.ForumObjects.Clear();
            _btnBack = AddButton("返回", "关闭玩家资料");
            _btnAddFriend = AddButton("添加好友", "好友功能稍后接入");
            _btnProfile = AddButton("玩家资料", BuildSummary());
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
            if (button == _btnBack)
            {
                UIManager.CloseCurrentWindow();
                return;
            }

            if (button == _btnAddFriend)
            {
                MelonLogger.Msg($"Friend action placeholder: {_player.Uid}");
                return;
            }

            if (button == _btnProfile) return;
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
            InjectDetails(imgBase);
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

        private void InjectDetails(Transform imgBase)
        {
            var detailsObj = new GameObject(InjectedDetailsName);
            detailsObj.transform.SetParent(imgBase, false);
            detailsObj.transform.localScale = Vector3.one;

            var rect = detailsObj.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(170f, -70f);
            rect.sizeDelta = new Vector2(430f, 300f);

            var text = detailsObj.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            text.fontSize = 24;
            text.alignment = TextAnchor.UpperLeft;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.supportRichText = true;
            text.raycastTarget = false;
            text.color = Color.white;
            text.text = BuildDetails();
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

            var oldDetails = imgBase.Find(InjectedDetailsName);
            if (oldDetails != null) UnityEngine.Object.Destroy(oldDetails.gameObject);
        }

        private string BuildSummary()
        {
            return $"头衔: {GetDisplayTitle()}\nUID: {_player.Uid}\n状态: {GetStatusText(_player.Status)}\nPing: {_player.PingMS}ms";
        }

        private string BuildDetails()
        {
            return $"<color=#{Constants.ColorYellow}>头衔</color>\n{EscapeRichText(GetDisplayTitle())}\n\n" +
                $"<color=#{Constants.ColorYellow}>UID</color>\n{EscapeRichText(_player.Uid)}\n\n" +
                $"<color=#{Constants.ColorYellow}>状态</color>\n{GetStatusText(_player.Status)}\n\n" +
                $"<color=#{Constants.ColorYellow}>临时资料</color>\n这里先显示占位内容，后续接入详细资料。";
        }

        private string GetDisplayName()
        {
            return string.IsNullOrWhiteSpace(_player.Name) ? _player.Uid ?? "玩家资料" : _player.Name;
        }

        private string GetDisplayTitle()
        {
            return string.IsNullOrWhiteSpace(_player.Title) ? "暂无头衔" : _player.Title;
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
