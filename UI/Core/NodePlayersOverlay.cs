using System;
using System.Collections.Generic;
using Il2CppAssets.Scripts.UI.Controls;
using MDEN.Managers;
using MDEN.Protocol.Enums;
using MDEN.Protocol.Messages.Social;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Events;
using UnityEngine.UI;

namespace MDEN.UI.Core
{
    internal static class NodePlayersOverlay
    {
        private const int SortingOrder = 32767;
        private const float CapsuleWidth = 1040f;
        private const float CapsuleHeight = 56f;
        private const float CapsuleGap = 12f;
        private const float ListViewportHeight = 780f;
        private static readonly TimeSpan OpenedInLobbyMaxAge = TimeSpan.FromSeconds(20);
        private const int GoodPingThresholdMs = 80;
        private const int MediumPingThresholdMs = 180;
        private const int MaxRoomNameLength = 16;
        private const int MaxPlayerNameLength = 18;
        private const string InputBlockReason = "NodePlayersOverlay";

        private static GameObject _root;
        private static RectTransform _viewportRoot;
        private static RectTransform _listRoot;
        private static Text _messageText;
        private static ScrollRect _scrollRect;
        private static int? _openedLobbyId;
        private static DateTime _openedAtUtc;
        private static int _generation;
        private static bool _initialized;
        private static readonly HashSet<string> InvitedUids = new HashSet<string>();
        private static Sprite _roundedSprite;

        public static void Initialize()
        {
            if (_initialized) return;
            LobbyManager.CurrentLobbyChanged += HandleLobbyChanged;
            ConnectionManager.StateChanged += HandleConnectionStateChanged;
            _initialized = true;
        }

        public static void Deinitialize()
        {
            if (!_initialized) return;
            LobbyManager.CurrentLobbyChanged -= HandleLobbyChanged;
            ConnectionManager.StateChanged -= HandleConnectionStateChanged;
            _initialized = false;
            Destroy();
        }

        public static void Show()
        {
            Destroy();
            InvitedUids.Clear();
            _generation++;
            _openedLobbyId = LobbyManager.CurrentLobby?.Id;
            _openedAtUtc = DateTime.UtcNow;
            CreateRoot();
            CreateTitle();
            CreateListRoot();
            SetMessage(I18nManager.T("node.players.loading"));
            _ = RefreshAsync(_generation);
        }

        public static void Destroy()
        {
            if (_root != null)
            {
                UnityEngine.Object.Destroy(_root);
                _root = null;
            }

            _listRoot = null;
            _viewportRoot = null;
            _messageText = null;
            _scrollRect = null;
            _openedLobbyId = null;
            _generation++;
            NativeInputBlocker.Clear(InputBlockReason);
        }

        private static async System.Threading.Tasks.Task RefreshAsync(int generation)
        {
            try
            {
                var players = await SocialManager.GetNodePlayersAsync();
                MainThreadDispatcher.Enqueue(() =>
                {
                    if (!IsCurrentGeneration(generation)) return;
                    RenderPlayers(players);
                });
            }
            catch (Exception ex)
            {
                MainThreadDispatcher.Enqueue(() =>
                {
                    if (!IsCurrentGeneration(generation)) return;
                    SetMessage(I18nManager.Tf("node.players.load_failed", ex.Message));
                });
            }
        }

        private static void HandleLobbyChanged(MDEN.Protocol.Messages.Lobby.LobbySyncPush lobby)
        {
            if (_root == null) return;
            if (ShouldCloseForLobbyChange(lobby))
            {
                Destroy();
            }
        }

        private static void HandleConnectionStateChanged(ConnectionLifecycleState state)
        {
            if (state != ConnectionLifecycleState.Connected)
            {
                Destroy();
            }
        }

        private static bool ShouldCloseForLobbyChange(MDEN.Protocol.Messages.Lobby.LobbySyncPush lobby)
        {
            if (lobby?.IsPlaying == true) return true;
            if (!_openedLobbyId.HasValue) return lobby != null;
            if (lobby == null) return true;
            if (lobby.Id != _openedLobbyId.Value) return true;
            return DateTime.UtcNow - _openedAtUtc > OpenedInLobbyMaxAge;
        }

        private static bool IsCurrentGeneration(int generation)
        {
            return _root != null && _generation == generation;
        }

        private static void CreateRoot()
        {
            _root = new GameObject("MDENNodePlayersOverlay");
            UnityEngine.Object.DontDestroyOnLoad(_root);

            var rootRect = _root.AddComponent<RectTransform>();
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.offsetMin = Vector2.zero;
            rootRect.offsetMax = Vector2.zero;

            var canvas = _root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.overrideSorting = true;
            canvas.sortingOrder = SortingOrder;

            var scaler = _root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            _root.AddComponent<GraphicRaycaster>();
            CreateShade(rootRect);
            NativeInputBlocker.SetBlocked(InputBlockReason, true);
        }

        private static void CreateShade(RectTransform parent)
        {
            var shade = new GameObject("Shade");
            var shadeRect = shade.AddComponent<RectTransform>();
            shadeRect.SetParent(parent, false);
            shadeRect.anchorMin = Vector2.zero;
            shadeRect.anchorMax = Vector2.one;
            shadeRect.offsetMin = Vector2.zero;
            shadeRect.offsetMax = Vector2.zero;

            var image = shade.AddComponent<Image>();
            image.color = new Color(0f, 0f, 0f, 0.82f);
            image.raycastTarget = true;

            var button = shade.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.targetGraphic = image;
            button.onClick.AddListener((UnityAction)new Action(Destroy));
        }

        private static void CreateTitle()
        {
            var title = CreateText(_root.transform, "Title", I18nManager.Tf("node.players.title", ConnectionManager.CurrentServerDisplayName), 40, TextAnchor.MiddleCenter);
            title.color = new Color(1f, 0.86f, 0.42f, 1f);
            title.fontStyle = FontStyle.Bold;

            var rect = title.rectTransform;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.offsetMin = new Vector2(160f, -118f);
            rect.offsetMax = new Vector2(-160f, -48f);
        }

        private static void CreateListRoot()
        {
            var viewport = new GameObject("ListViewport");
            _viewportRoot = viewport.AddComponent<RectTransform>();
            _viewportRoot.SetParent(_root.transform, false);
            _viewportRoot.anchorMin = new Vector2(0.5f, 1f);
            _viewportRoot.anchorMax = new Vector2(0.5f, 1f);
            _viewportRoot.pivot = new Vector2(0.5f, 1f);
            _viewportRoot.anchoredPosition = new Vector2(0f, -150f);
            _viewportRoot.sizeDelta = new Vector2(CapsuleWidth, ListViewportHeight);

            var viewportImage = viewport.AddComponent<Image>();
            viewportImage.color = new Color(0f, 0f, 0f, 0f);
            viewportImage.raycastTarget = true;
            viewport.AddComponent<RectMask2D>();

            var content = new GameObject("List");
            _listRoot = content.AddComponent<RectTransform>();
            _listRoot.SetParent(_viewportRoot, false);
            _listRoot.anchorMin = new Vector2(0.5f, 1f);
            _listRoot.anchorMax = new Vector2(0.5f, 1f);
            _listRoot.pivot = new Vector2(0.5f, 1f);
            _listRoot.anchoredPosition = Vector2.zero;
            _listRoot.sizeDelta = new Vector2(CapsuleWidth, ListViewportHeight);

            _scrollRect = viewport.AddComponent<ScrollRect>();
            _scrollRect.content = _listRoot;
            _scrollRect.viewport = _viewportRoot;
            _scrollRect.horizontal = false;
            _scrollRect.vertical = true;
            _scrollRect.movementType = ScrollRect.MovementType.Clamped;
            _scrollRect.scrollSensitivity = 36f;

            _messageText = CreateText(_viewportRoot, "Message", string.Empty, 28, TextAnchor.MiddleCenter);
            _messageText.color = new Color(0.88f, 0.88f, 0.92f, 1f);
            var msgRect = _messageText.rectTransform;
            msgRect.anchorMin = Vector2.zero;
            msgRect.anchorMax = Vector2.one;
            msgRect.offsetMin = Vector2.zero;
            msgRect.offsetMax = Vector2.zero;
        }

        private static void RenderPlayers(NodePlayerEntry[] players)
        {
            if (_root == null || _listRoot == null) return;

            ClearList();
            if (players == null || players.Length == 0)
            {
                SetMessage(I18nManager.T("node.players.empty"));
                return;
            }

            SetMessage(string.Empty);
            var contentHeight = Math.Max(
                ListViewportHeight,
                players.Length * CapsuleHeight + Math.Max(0, players.Length - 1) * CapsuleGap);
            _listRoot.sizeDelta = new Vector2(CapsuleWidth, contentHeight);
            if (_scrollRect != null) _scrollRect.verticalNormalizedPosition = 1f;

            var y = 0f;
            for (var i = 0; i < players.Length; i++)
            {
                CreatePlayerCapsule(players[i], y);
                y -= CapsuleHeight + CapsuleGap;
            }
        }

        private static void ClearList()
        {
            for (var i = _listRoot.childCount - 1; i >= 0; i--)
            {
                var child = _listRoot.GetChild(i);
                UnityEngine.Object.Destroy(child.gameObject);
            }
        }

        private static void SetMessage(string message)
        {
            if (_messageText != null)
            {
                _messageText.text = message ?? string.Empty;
                _messageText.gameObject.SetActive(!string.IsNullOrWhiteSpace(_messageText.text));
            }
        }

        private static void CreatePlayerCapsule(NodePlayerEntry player, float y)
        {
            var capsule = new GameObject("PlayerCapsule");
            var rect = capsule.AddComponent<RectTransform>();
            rect.SetParent(_listRoot, false);
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, y);
            rect.sizeDelta = new Vector2(CapsuleWidth, CapsuleHeight);

            var image = capsule.AddComponent<Image>();
            image.sprite = GetRoundedSprite();
            image.type = image.sprite != null ? Image.Type.Sliced : Image.Type.Simple;
            image.color = new Color(0.78f, 0.78f, 0.82f, 0.28f);
            image.raycastTarget = false;

            var status = CreateText(rect, "Status", BuildStatusText(player), 22, TextAnchor.MiddleLeft);
            var statusRect = status.rectTransform;
            statusRect.anchorMin = new Vector2(0f, 0f);
            statusRect.anchorMax = new Vector2(0f, 1f);
            statusRect.offsetMin = new Vector2(28f, 0f);
            statusRect.offsetMax = new Vector2(300f, 0f);

            var name = CreateText(rect, "Name", ColorText(Truncate(GetPlayerName(player), MaxPlayerNameLength), NormalizeColor(player.ChatColor)), 24, TextAnchor.MiddleLeft);
            var nameRect = name.rectTransform;
            nameRect.anchorMin = new Vector2(0f, 0f);
            nameRect.anchorMax = new Vector2(1f, 1f);
            nameRect.offsetMin = new Vector2(318f, 0f);
            nameRect.offsetMax = new Vector2(-330f, 0f);

            var ping = CreateText(rect, "Ping", BuildPingText(player), 22, TextAnchor.MiddleCenter);
            var pingRect = ping.rectTransform;
            pingRect.anchorMin = new Vector2(1f, 0f);
            pingRect.anchorMax = new Vector2(1f, 1f);
            pingRect.pivot = new Vector2(1f, 0.5f);
            pingRect.anchoredPosition = new Vector2(-218f, 0f);
            pingRect.sizeDelta = new Vector2(86f, 0f);

            CreateInviteButton(rect, player);
        }

        private static void CreateInviteButton(RectTransform parent, NodePlayerEntry player)
        {
            var buttonObj = new GameObject("InviteButton");
            var rect = buttonObj.AddComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(1f, 0.5f);
            rect.anchorMax = new Vector2(1f, 0.5f);
            rect.pivot = new Vector2(1f, 0.5f);
            rect.anchoredPosition = new Vector2(-18f, 0f);
            rect.sizeDelta = new Vector2(190f, 40f);

            var image = buttonObj.AddComponent<Image>();
            image.sprite = GetRoundedSprite();
            image.type = image.sprite != null ? Image.Type.Sliced : Image.Type.Simple;

            var canInvite = CanInvite(player);
            image.color = canInvite
                ? new Color(0.42f, 0.16f, 0.66f, 0.96f)
                : new Color(0.18f, 0.18f, 0.2f, 0.58f);

            var button = buttonObj.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.targetGraphic = image;
            button.interactable = canInvite;
            if (canInvite)
            {
                button.onClick.AddListener((UnityAction)new Action(() => SendInvite(player, button)));
            }

            var label = CreateText(rect, "Label", GetInviteButtonText(player, canInvite), 20, TextAnchor.MiddleCenter);
            label.color = canInvite ? Color.white : new Color(0.74f, 0.74f, 0.78f, 1f);
            var labelRect = label.rectTransform;
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;
        }

        private static async void SendInvite(NodePlayerEntry player, Button button)
        {
            if (player == null || button == null) return;

            var targetUid = player.Uid;
            SetInviteButtonState(button, I18nManager.T("node.players.invite_sent"), false);

            try
            {
                await SocialManager.SendLobbyInviteAsync(targetUid);
                MainThreadDispatcher.Enqueue(() => InvitedUids.Add(targetUid));
            }
            catch (Exception ex)
            {
                MainThreadDispatcher.Enqueue(() =>
                {
                    SetInviteButtonState(button, I18nManager.T("node.players.invite"), true);
                    ShowText.ShowInfo(I18nManager.Tf("node.players.invite_failed", ex.Message));
                });
            }
        }

        private static void SetInviteButtonState(Button button, string labelText, bool interactable)
        {
            if (button == null) return;

            button.interactable = interactable;
            var label = button.GetComponentInChildren<Text>();
            if (label != null) label.text = labelText;
        }

        private static bool CanInvite(NodePlayerEntry player)
        {
            return player != null &&
                   LobbyManager.IsInLobby &&
                   !string.IsNullOrWhiteSpace(player.Uid) &&
                   player.Uid != PlayerManager.CurrentUid &&
                   (PlayerStatus)player.Status == PlayerStatus.Online &&
                   !InvitedUids.Contains(player.Uid);
        }

        private static string GetInviteButtonText(NodePlayerEntry player, bool canInvite)
        {
            if (player != null && InvitedUids.Contains(player.Uid)) return I18nManager.T("node.players.invite_sent");
            return canInvite ? I18nManager.T("node.players.invite") : I18nManager.T("node.players.invite_unavailable");
        }

        private static string BuildStatusText(NodePlayerEntry player)
        {
            var status = player == null ? PlayerStatus.Offline : (PlayerStatus)player.Status;
            return status switch
            {
                PlayerStatus.Online => ColorText(I18nManager.T("node.players.status.idle"), Constants.ColorSoftGreen),
                PlayerStatus.SinglePlaying => ColorText(I18nManager.T("node.players.status.single"), Constants.ColorRed),
                _ => ColorText(I18nManager.Tf("node.players.status.room", Truncate(player?.LobbyName, MaxRoomNameLength)), Constants.ColorYellow)
            };
        }

        private static string BuildPingText(NodePlayerEntry player)
        {
            var ping = player?.PingMS ?? 0;
            return ColorText($"{ping}ms", GetPingColor(ping));
        }

        private static string GetPingColor(ushort ping)
        {
            if (ping <= GoodPingThresholdMs) return Constants.ColorSoftGreen;
            if (ping <= MediumPingThresholdMs) return Constants.ColorYellow;
            return Constants.ColorRed;
        }

        private static Text CreateText(Transform parent, string name, string value, int fontSize, TextAnchor alignment)
        {
            var obj = new GameObject(name);
            var rect = obj.AddComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.localScale = Vector3.one;

            var text = obj.AddComponent<Text>();
            NativeFontCache.ApplyTo(text);
            text.text = value;
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.supportRichText = true;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.raycastTarget = false;
            return text;
        }

        private static Sprite GetRoundedSprite()
        {
            if (_roundedSprite != null) return _roundedSprite;

            try
            {
                _roundedSprite = Addressables.LoadAssetAsync<Sprite>("SprRoundedsquare").WaitForCompletion();
            }
            catch
            {
                _roundedSprite = null;
            }

            return _roundedSprite;
        }

        private static string GetPlayerName(NodePlayerEntry player)
        {
            return string.IsNullOrWhiteSpace(player?.Name) ? player?.Uid ?? string.Empty : player.Name;
        }

        private static string NormalizeColor(string color)
        {
            if (string.IsNullOrWhiteSpace(color)) return "ffffffff";
            var value = color.Trim().TrimStart('#');
            return System.Text.RegularExpressions.Regex.IsMatch(value, "^[0-9a-fA-F]{6}([0-9a-fA-F]{2})?$")
                ? value
                : "ffffffff";
        }

        private static string ColorText(string value, string color)
        {
            return $"<color=#{color}>{EscapeRichText(value)}</color>";
        }

        private static string EscapeRichText(string value)
        {
            return string.IsNullOrEmpty(value)
                ? string.Empty
                : value.Replace("<", "＜").Replace(">", "＞");
        }

        private static string Truncate(string value, int maxLength)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            return value.Length <= maxLength ? value : value.Substring(0, maxLength) + "...";
        }
    }
}
