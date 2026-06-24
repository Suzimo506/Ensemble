using System;
using System.Collections.Generic;
using MDEN.Managers;
using MDEN.Protocol.Messages.Lobby;
using MDEN.Protocol.Models;
using MDEN.Protocol.Rules;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.UI;

namespace MDEN.UI.Core
{
    public static class RoomSceneOverlay
    {
        private const string NativeBackgroundPath = "UI/Standerd/PnlHome/PnlBgSwitchFsv";
        private const string StagePanelPath = "UI/Standerd/PnlStage";
        private const string PreparationPanelPath = "UI/Standerd/PnlPreparation";
        private const int OverlaySortingOrder = 32750;
        private const float InfoPanelWidth = 420f;
        private const float InfoPanelPadding = 14f;
        private const float InfoPanelTop = 82f;
        private const float InfoPanelRight = 20f;
        private const float InfoTitleTop = 12f;
        private const float InfoTitleHeight = 24f;
        private const float InfoMetaTop = 38f;
        private const float InfoMetaHeight = 22f;
        private const float PlayerListTop = 68f;
        private const float PlayerLineHeight = 32f;
        private const float PlayerRowGap = 6f;
        private const float PlayerRowInset = 10f;
        private const float PlayerAvatarSize = 28f;
        private const float PlayerAvatarGap = 8f;
        private const float PlayerNameWidth = 236f;
        private const float PlayerStateWidth = 82f;
        private const int MaxVisiblePlayerRows = 8;
        private const int MaxLockedVisiblePlayerRows = 4;
        private const float ScrollWheelDeadZone = 0.01f;
        private const int InfoTitleFontSize = 20;
        private const int InfoMetaFontSize = 17;
        private const int PlayerNameFontSize = 17;
        private const int PlayerStateFontSize = 17;
        private const string PlayerNotReadyColor = "ff7777ff";
        private const string PlayerSelectingColor = Constants.ColorYellow;
        private const string PlayerPlayingColor = Constants.ColorRed;
        private const string PlayerReadyColor = Constants.ColorSoftGreen;

        private static readonly string[] NativeSettingsPanelPaths =
        {
            "UI/Standerd/PnlOption",
            "UI/Standerd/PnlSetting",
            "UI/Standerd/PnlSettings",
            "PnlOption",
            "PnlSetting",
            "PnlSettings"
        };

        private static readonly Dictionary<string, HiddenObjectState> OriginalStates = new Dictionary<string, HiddenObjectState>();
        private static readonly Dictionary<string, string> PlayerColorCache = new Dictionary<string, string>();
        private static readonly HashSet<string> PendingColorRequests = new HashSet<string>();
        private static GameObject _frame;
        private static GameObject _roomInfoPanel;
        private static RectTransform _roomInfoPanelRect;
        private static Text _roomTitle;
        private static Text _roomMeta;
        private static Sprite _roundedSprite;
        private static int _playerScrollOffset;
        private static int _lastLobbyId = -1;
        private static bool _lastLobbyLocked;
        private static int _lastPlayerCount;
        private static bool _isDraggingPlayerList;
        private static float _dragStartMouseY;
        private static int _dragStartScrollOffset;
        private static readonly List<PlayerRowView> PlayerRows = new List<PlayerRowView>();

        public static bool IsCreated => _frame != null;
        public static bool IsHomeReady => GameObject.Find("UI/Standerd/PnlHome") != null;
        public static bool IsNavigationReady => GameObject.Find("UI/Standerd/PnlNavigation") != null;
        public static bool IsReady => IsHomeReady && IsNavigationReady;
        public static bool IsHomeVisible => GetHomeVisible();
        public static bool IsRoomInfoVisible => GetRoomInfoVisible();

        public static void InvalidatePlayerColors()
        {
            PlayerColorCache.Clear();
            PendingColorRequests.Clear();
            Refresh(LobbyManager.CurrentLobby);
        }

        private static readonly string[] HiddenObjectPaths =
        {
            "UI/Standerd/PnlHome/ElfinShow",
            "UI/Standerd/PnlHome/MuseShow/BtnInteraction"
        };

        public static void Refresh(LobbySyncPush lobby)
        {
            if (lobby == null)
            {
                Destroy();
                return;
            }

            var homeVisible = IsHomeVisible;
            if (!ShouldShowRoomInfo(lobby, homeVisible))
            {
                Hide();
                return;
            }

            EnsureFrame();
            if (_frame == null) return;

            if (homeVisible)
            {
                EnsureNativeBackgroundVisible();
                HideSinglePlayerControls();
            }

            _frame.SetActive(true);
            EnsureOverlayOrder();
            UpdateRoomInfo(lobby);
        }

        public static void Hide()
        {
            if (_frame != null) _frame.SetActive(false);
        }

        public static void UpdateVisibility()
        {
            var lobby = LobbyManager.CurrentLobby;
            var homeVisible = IsHomeVisible;
            var visible = LobbyManager.IsInLobby && ShouldShowRoomInfo(lobby, homeVisible);
            if (visible)
            {
                EnsureFrame();
            }

            if (_frame == null) return;
            _frame.SetActive(visible);
            if (visible)
            {
                if (homeVisible)
                {
                    EnsureNativeBackgroundVisible();
                    HideSinglePlayerControls();
                }

                EnsureOverlayOrder();
                HandlePlayerListPointerInput(lobby);
                UpdateRoomInfo(lobby);
            }
        }

        public static void Destroy()
        {
            RestoreSinglePlayerControls();
            EnsureNativeBackgroundVisible();

            if (_frame != null)
            {
                UnityEngine.Object.Destroy(_frame);
            }

            ClearFrameReferences();
        }

        private static void EnsureFrame()
        {
            if (_frame != null &&
                _roomInfoPanel != null &&
                _roomInfoPanelRect != null &&
                _roomTitle != null &&
                _roomMeta != null)
            {
                return;
            }

            if (_frame != null)
            {
                UnityEngine.Object.Destroy(_frame);
            }

            ClearFrameReferences();

            _frame = new GameObject("MDENRoomSceneOverlay");
            var rect = _frame.AddComponent<RectTransform>();
            rect.localScale = Vector3.one;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition3D = Vector3.zero;
            rect.sizeDelta = Vector2.zero;

            var canvas = _frame.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = OverlaySortingOrder;

            var scaler = _frame.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            _frame.AddComponent<GraphicRaycaster>();

            CreateRoomInfoPanel();
        }

        private static void ClearFrameReferences()
        {
            _frame = null;
            _roomInfoPanel = null;
            _roomInfoPanelRect = null;
            _roomTitle = null;
            _roomMeta = null;
            _playerScrollOffset = 0;
            _lastLobbyId = -1;
            _lastLobbyLocked = false;
            _lastPlayerCount = 0;
            _isDraggingPlayerList = false;
            _dragStartMouseY = 0f;
            _dragStartScrollOffset = 0;
            PlayerRows.Clear();
        }

        private static void EnsureOverlayOrder()
        {
            if (_frame == null) return;
            var canvas = _frame.GetComponent<Canvas>();
            if (canvas != null) canvas.sortingOrder = OverlaySortingOrder;
        }

        private static bool GetHomeVisible()
        {
            var home = GameObject.Find("UI/Standerd/PnlHome");
            return home != null &&
                   home.activeInHierarchy &&
                   !IsPanelVisible(StagePanelPath) &&
                   !IsPanelVisible(PreparationPanelPath);
        }

        private static bool GetRoomInfoVisible()
        {
            return LobbyManager.IsInLobby && ShouldShowRoomInfo(LobbyManager.CurrentLobby, GetHomeVisible());
        }

        private static bool ShouldShowRoomInfo(LobbySyncPush lobby, bool homeVisible)
        {
            return lobby != null &&
                   !IsNativeSettingsVisible() &&
                   (homeVisible || !lobby.IsPlaying);
        }

        private static bool IsPanelVisible(string path)
        {
            var obj = GameObject.Find(path);
            return obj != null && obj.activeInHierarchy;
        }

        private static bool IsNativeSettingsVisible()
        {
            foreach (var path in NativeSettingsPanelPaths)
            {
                if (IsPanelVisible(path)) return true;
            }

            return false;
        }

        private static void CreateRoomInfoPanel()
        {
            _roomInfoPanel = new GameObject("RoomInfoPanel");
            _roomInfoPanelRect = _roomInfoPanel.AddComponent<RectTransform>();
            _roomInfoPanelRect.SetParent(_frame.transform, false);
            _roomInfoPanelRect.localScale = Vector3.one;
            _roomInfoPanelRect.anchorMin = new Vector2(1f, 1f);
            _roomInfoPanelRect.anchorMax = new Vector2(1f, 1f);
            _roomInfoPanelRect.pivot = new Vector2(1f, 1f);
            _roomInfoPanelRect.anchoredPosition = new Vector2(-InfoPanelRight, -InfoPanelTop);
            _roomInfoPanelRect.sizeDelta = new Vector2(InfoPanelWidth, 180f);

            var background = _roomInfoPanel.AddComponent<Image>();
            background.sprite = GetRoundedSprite();
            background.type = background.sprite != null ? Image.Type.Sliced : Image.Type.Simple;
            background.color = new Color(0f, 0f, 0f, 0.38f);
            background.raycastTarget = true;

            _roomTitle = CreatePanelText("RoomTitle", InfoPanelPadding, InfoTitleTop, InfoPanelWidth - InfoPanelPadding * 2f, InfoTitleHeight, InfoTitleFontSize, TextAnchor.UpperLeft);
            _roomMeta = CreatePanelText("RoomMeta", InfoPanelPadding, InfoMetaTop, InfoPanelWidth - InfoPanelPadding * 2f, InfoMetaHeight, InfoMetaFontSize, TextAnchor.UpperLeft);
        }

        private static Text CreatePanelText(
            string name,
            float x,
            float y,
            float width,
            float height,
            int fontSize,
            TextAnchor anchor)
        {
            var obj = new GameObject(name);
            var rect = obj.AddComponent<RectTransform>();
            rect.SetParent(_roomInfoPanel.transform, false);
            rect.localScale = Vector3.one;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(x, -y);
            rect.sizeDelta = new Vector2(width, height);

            var text = obj.AddComponent<Text>();
            ApplyGameFont(text);
            text.fontSize = fontSize;
            text.alignment = anchor;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.supportRichText = true;
            text.raycastTarget = false;
            text.color = Color.white;

            var shadow = obj.AddComponent<Shadow>();
            shadow.effectDistance = new Vector2(2f, -2f);
            shadow.effectColor = new Color(0f, 0f, 0f, 0.3f);

            return text;
        }

        private static void EnsurePlayerRowCount(int count)
        {
            if (PlayerRows.Exists(row => row == null || !row.IsAlive))
            {
                PlayerRows.Clear();
            }

            while (PlayerRows.Count < count)
            {
                PlayerRows.Add(CreatePlayerRow(PlayerRows.Count));
            }
        }

        private static PlayerRowView CreatePlayerRow(int index)
        {
            var root = new GameObject("PlayerRow_" + index);
            var rootRect = root.AddComponent<RectTransform>();
            rootRect.SetParent(_roomInfoPanel.transform, false);
            rootRect.localScale = Vector3.one;
            rootRect.anchorMin = new Vector2(0f, 1f);
            rootRect.anchorMax = new Vector2(0f, 1f);
            rootRect.pivot = new Vector2(0f, 1f);
            rootRect.sizeDelta = new Vector2(InfoPanelWidth - InfoPanelPadding * 2f, PlayerLineHeight);

            var image = root.AddComponent<Image>();
            image.sprite = GetRoundedSprite();
            image.type = image.sprite != null ? Image.Type.Sliced : Image.Type.Simple;
            image.color = new Color(0f, 0f, 0f, 0.24f);
            image.raycastTarget = false;

            var avatar = CreateRowAvatar(rootRect);
            var nameX = PlayerRowInset + PlayerAvatarSize + PlayerAvatarGap;
            var nameText = CreateRowText(rootRect, "PlayerName", nameX, PlayerNameWidth, TextAnchor.MiddleLeft, PlayerNameFontSize);
            var stateText = CreateRowText(rootRect, "PlayerState", InfoPanelWidth - InfoPanelPadding * 2f - PlayerRowInset - PlayerStateWidth, PlayerStateWidth, TextAnchor.MiddleRight, PlayerStateFontSize);
            return new PlayerRowView(root, rootRect, avatar, nameText, stateText);
        }

        private static Image CreateRowAvatar(RectTransform parent)
        {
            var obj = new GameObject("PlayerAvatar");
            var rect = obj.AddComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.localScale = Vector3.one;
            rect.anchorMin = new Vector2(0f, 0.5f);
            rect.anchorMax = new Vector2(0f, 0.5f);
            rect.pivot = new Vector2(0f, 0.5f);
            rect.anchoredPosition = new Vector2(PlayerRowInset, 0f);
            rect.sizeDelta = new Vector2(PlayerAvatarSize, PlayerAvatarSize);

            var image = obj.AddComponent<Image>();
            image.preserveAspect = true;
            image.raycastTarget = false;
            return image;
        }

        private static Text CreateRowText(RectTransform parent, string name, float x, float width, TextAnchor anchor, int fontSize)
        {
            var obj = new GameObject(name);
            var rect = obj.AddComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.localScale = Vector3.one;
            rect.anchorMin = new Vector2(0f, 0.5f);
            rect.anchorMax = new Vector2(0f, 0.5f);
            rect.pivot = new Vector2(0f, 0.5f);
            rect.anchoredPosition = new Vector2(x, 0f);
            rect.sizeDelta = new Vector2(width, PlayerLineHeight);

            var text = obj.AddComponent<Text>();
            ApplyGameFont(text);
            text.fontSize = fontSize;
            text.alignment = anchor;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.supportRichText = true;
            text.raycastTarget = false;
            text.color = Color.white;

            var shadow = obj.AddComponent<Shadow>();
            shadow.effectDistance = new Vector2(1.5f, -1.5f);
            shadow.effectColor = new Color(0f, 0f, 0f, 0.42f);

            return text;
        }

        private static void HidePlayerRowsFrom(int index)
        {
            for (var i = index; i < PlayerRows.Count; i++)
            {
                PlayerRows[i].Hide();
            }
        }

        private static void HideSinglePlayerControls()
        {
            foreach (var path in HiddenObjectPaths)
            {
                var obj = FindByPathIncludingInactive(path);
                if (obj == null) continue;

                if (!OriginalStates.ContainsKey(path))
                {
                    OriginalStates[path] = new HiddenObjectState(obj, obj.activeSelf);
                }

                obj.SetActive(false);
            }
        }

        private static void RestoreSinglePlayerControls()
        {
            foreach (var state in OriginalStates)
            {
                var obj = state.Value.Target;
                if (obj == null)
                {
                    obj = FindByPathIncludingInactive(state.Key);
                }

                if (obj != null)
                {
                    obj.SetActive(state.Value.ActiveSelf);
                }
            }

            OriginalStates.Clear();
        }

        private static void EnsureNativeBackgroundVisible()
        {
            var background = FindByPathIncludingInactive(NativeBackgroundPath);
            if (background != null && !background.activeSelf)
            {
                background.SetActive(true);
            }
        }

        private static GameObject FindByPathIncludingInactive(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;

            var parts = path.Split('/');
            if (parts.Length == 0) return null;

            var current = GameObject.Find(parts[0]);
            if (current == null) return null;

            var transform = current.transform;
            for (var i = 1; i < parts.Length; i++)
            {
                transform = transform.Find(parts[i]);
                if (transform == null) return null;
            }

            return transform.gameObject;
        }

        private static void UpdateRoomInfo(LobbySyncPush lobby)
        {
            if (lobby == null || _roomInfoPanelRect == null || _roomTitle == null || _roomMeta == null)
            {
                return;
            }

            var players = GetRoomInfoPlayers(lobby);
            var maxVisibleRows = GetMaxVisiblePlayerRows(lobby);
            ResetScrollIfRoomShapeChanged(lobby, players.Length);
            ClampPlayerScrollOffset(players.Length, maxVisibleRows);
            var visiblePlayerCount = Math.Min(players.Length, maxVisibleRows);
            var playerRows = players.Length == 0 ? 1 : Math.Min(players.Length, maxVisibleRows);
            var playerListHeight = playerRows * PlayerLineHeight + Math.Max(0, playerRows - 1) * PlayerRowGap;
            var panelHeight = PlayerListTop + playerListHeight + InfoPanelPadding;
            _roomInfoPanelRect.sizeDelta = new Vector2(InfoPanelWidth, panelHeight);

            var roomName = EscapeRichText(lobby.Name);
            var hostName = EscapeRichText(GetHostName(lobby));
            _roomTitle.text =
                $"<color=#{Constants.ColorYellow}>【{roomName}】</color> " +
                LobbyRuleTextFormatter.FormatPlayMode(lobby.PlayMode, true);
            _roomMeta.text =
                $"房主：<color=#{GetPlayerColor(lobby.HostUid)}>{hostName}</color>  " +
                $"人数：<color=#{Constants.ColorCyan}>{GetPlayerCount(lobby)}/{lobby.MaxPlayers}</color>  " +
                $"观众：<color=#{Constants.ColorCyan}>{lobby.WatcherCount}</color>";

            if (players.Length == 0)
            {
                EnsurePlayerRowCount(1);
                PlayerRows[0].Set(PlayerListTop, null, null, null, "暂无玩家", "ffffffff", string.Empty, "ffffffff");
                HidePlayerRowsFrom(1);
                return;
            }

            EnsurePlayerRowCount(playerRows);
            for (var i = 0; i < visiblePlayerCount; i++)
            {
                var player = players[_playerScrollOffset + i];
                var state = GetPlayerState(lobby, player.Uid);
                var y = PlayerListTop + i * (PlayerLineHeight + PlayerRowGap);
                PlayerRows[i].Set(
                    y,
                    player.Uid,
                    player.AvatarName,
                    player.AvatarData,
                    Truncate(EscapeRichText(player.Name), 16),
                    GetPlayerColor(player.Uid),
                    state.Text,
                    state.Color);
            }

            HidePlayerRowsFrom(playerRows);
        }

        private static void HandlePlayerListPointerInput(LobbySyncPush lobby)
        {
            if (lobby == null || _roomInfoPanelRect == null) return;

            var pointerOverPanel = IsPointerOverRoomInfoPanel();
            if (Input.GetMouseButtonDown(0))
            {
                if (pointerOverPanel)
                {
                    _isDraggingPlayerList = true;
                    _dragStartMouseY = Input.mousePosition.y;
                    _dragStartScrollOffset = _playerScrollOffset;
                }
                else
                {
                    _isDraggingPlayerList = false;
                }
            }

            if (_isDraggingPlayerList)
            {
                if (Input.GetMouseButton(0))
                {
                    UpdatePlayerListDrag(lobby);
                }
                else
                {
                    _isDraggingPlayerList = false;
                }
            }

            var delta = Input.mouseScrollDelta.y;
            if (Math.Abs(delta) <= ScrollWheelDeadZone) return;
            if (!pointerOverPanel) return;

            var players = GetRoomInfoPlayers(lobby);
            var maxVisibleRows = GetMaxVisiblePlayerRows(lobby);
            if (players.Length <= maxVisibleRows)
            {
                _playerScrollOffset = 0;
                return;
            }

            _playerScrollOffset += delta < 0f ? 1 : -1;
            ClampPlayerScrollOffset(players.Length, maxVisibleRows);
        }

        private static void UpdatePlayerListDrag(LobbySyncPush lobby)
        {
            var players = GetRoomInfoPlayers(lobby);
            var maxVisibleRows = GetMaxVisiblePlayerRows(lobby);
            if (players.Length <= maxVisibleRows)
            {
                _playerScrollOffset = 0;
                return;
            }

            var rowStride = PlayerLineHeight + PlayerRowGap;
            var rowDelta = Mathf.RoundToInt((Input.mousePosition.y - _dragStartMouseY) / rowStride);
            _playerScrollOffset = _dragStartScrollOffset + rowDelta;
            ClampPlayerScrollOffset(players.Length, maxVisibleRows);
        }

        private static bool IsPointerOverRoomInfoPanel()
        {
            return _roomInfoPanelRect != null &&
                   RectTransformUtility.RectangleContainsScreenPoint(_roomInfoPanelRect, Input.mousePosition, null);
        }

        private static void ResetScrollIfRoomShapeChanged(LobbySyncPush lobby, int playerCount)
        {
            if (lobby == null) return;
            if (_lastLobbyId == lobby.Id &&
                _lastLobbyLocked == lobby.Locked &&
                _lastPlayerCount == playerCount)
            {
                return;
            }

            _lastLobbyId = lobby.Id;
            _lastLobbyLocked = lobby.Locked;
            _lastPlayerCount = playerCount;
            _playerScrollOffset = 0;
        }

        private static void ClampPlayerScrollOffset(int playerCount, int maxVisibleRows)
        {
            var maxOffset = Math.Max(0, playerCount - maxVisibleRows);
            _playerScrollOffset = Mathf.Clamp(_playerScrollOffset, 0, maxOffset);
        }

        private static int GetMaxVisiblePlayerRows(LobbySyncPush lobby)
        {
            return lobby != null && lobby.Locked ? MaxLockedVisiblePlayerRows : MaxVisiblePlayerRows;
        }

        private static PlayerStateText GetPlayerState(LobbySyncPush lobby, string uid)
        {
            if (lobby.IsPlaying)
            {
                return new PlayerStateText("游戏中", PlayerPlayingColor);
            }

            if (!lobby.Locked)
            {
                return new PlayerStateText("选歌中", PlayerSelectingColor);
            }

            var ready = !string.IsNullOrEmpty(uid) &&
                        lobby.ReadyPlayers != null &&
                        Array.IndexOf(lobby.ReadyPlayers, uid) >= 0;
            var state = ready
                ? new PlayerStateText("已准备", PlayerReadyColor)
                : new PlayerStateText("未准备", PlayerNotReadyColor);

            if (LobbyPlayModeRules.IsRookie(lobby.PlayMode))
            {
                var difficulty = GetPlayerDifficulty(lobby, uid);
                if (difficulty > 0)
                {
                    state = new PlayerStateText(
                        $"{LobbyRuleTextFormatter.FormatDifficulty(difficulty, true)} {ColorText(state.Text, state.Color)}",
                        null);
                }
            }

            return state;
        }

        private static int GetPlayerDifficulty(LobbySyncPush lobby, string uid)
        {
            var difficulty = GetDifficulty(lobby?.CurrentBattleDifficulties, uid);
            return difficulty > 0 ? difficulty : GetDifficulty(lobby?.ReadyPlayerDifficulties, uid);
        }

        private static int GetDifficulty(LobbyPlayerDifficultyEntry[] entries, string uid)
        {
            if (entries == null || string.IsNullOrWhiteSpace(uid)) return 0;

            for (var i = 0; i < entries.Length; i++)
            {
                if (entries[i]?.Uid == uid) return entries[i].Difficulty;
            }

            return 0;
        }

        private static RoomInfoPlayer[] GetRoomInfoPlayers(LobbySyncPush lobby)
        {
            var players = new List<RoomInfoPlayer>();
            var seenUids = new HashSet<string>();
            if (lobby.PlayerDetails != null && lobby.PlayerDetails.Length > 0)
            {
                foreach (var player in lobby.PlayerDetails)
                {
                    if (string.IsNullOrEmpty(player?.Uid)) continue;
                    if (!seenUids.Add(player.Uid)) continue;
                    players.Add(new RoomInfoPlayer(
                        player.Uid,
                        string.IsNullOrEmpty(player.Name) ? player.Uid : player.Name,
                        player.AvatarName,
                        player.AvatarData));
                }
            }
            else if (lobby.Players != null)
            {
                foreach (var uid in lobby.Players)
                {
                    if (string.IsNullOrEmpty(uid)) continue;
                    if (!seenUids.Add(uid)) continue;
                    players.Add(new RoomInfoPlayer(
                        uid,
                        uid == PlayerManager.CurrentUid && !string.IsNullOrEmpty(PlayerManager.CurrentProfile?.Name)
                            ? PlayerManager.CurrentProfile.Name
                            : uid,
                        uid == PlayerManager.CurrentUid ? PlayerManager.CurrentProfile?.AvatarName : null,
                        uid == PlayerManager.CurrentUid ? PlayerManager.CurrentProfile?.AvatarData : null));
                }
            }

            return players
                .OrderBy(player => player.Uid == lobby.HostUid ? 0 : 1)
                .ToArray();
        }

        private static string GetHostName(LobbySyncPush lobby)
        {
            if (!string.IsNullOrEmpty(lobby.HostName)) return lobby.HostName;
            if (lobby.PlayerDetails != null)
            {
                foreach (var player in lobby.PlayerDetails)
                {
                    if (player?.Uid == lobby.HostUid && !string.IsNullOrEmpty(player.Name))
                    {
                        return player.Name;
                    }
                }
            }

            return lobby.HostUid;
        }

        private static int GetPlayerCount(LobbySyncPush lobby)
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

        private static string GetPlayerColor(string uid)
        {
            if (uid == PlayerManager.CurrentUid)
            {
                var localColor = NormalizeHexColor(PlayerManager.CurrentProfile?.ChatColor);
                if (!string.IsNullOrEmpty(localColor)) return localColor;
            }

            if (!string.IsNullOrWhiteSpace(uid))
            {
                var lobbyColor = GetLobbyPlayerColor(uid);
                if (!string.IsNullOrEmpty(lobbyColor))
                {
                    PlayerColorCache[uid] = lobbyColor;
                    return lobbyColor;
                }

                if (PlayerColorCache.TryGetValue(uid, out var cachedColor))
                {
                    return cachedColor;
                }

                RequestPlayerColor(uid);
            }

            return "ffffffff";
        }

        private static string GetLobbyPlayerColor(string uid)
        {
            var details = LobbyManager.CurrentLobby?.PlayerDetails;
            if (details == null) return null;

            foreach (var player in details)
            {
                if (player?.Uid != uid) continue;
                return NormalizeHexColor(player.ChatColor);
            }

            return null;
        }

        private static async void RequestPlayerColor(string uid)
        {
            if (string.IsNullOrWhiteSpace(uid) || PendingColorRequests.Contains(uid)) return;
            PendingColorRequests.Add(uid);

            var resolvedColor = "ffffffff";
            try
            {
                var profile = await PlayerManager.GetProfileAsync(uid);
                var color = NormalizeHexColor(profile?.ChatColor);
                resolvedColor = string.IsNullOrEmpty(color) ? "ffffffff" : color;
            }
            catch
            {
            }
            finally
            {
                PendingColorRequests.Remove(uid);
            }

            PlayerColorCache[uid] = resolvedColor;
            MainThreadDispatcher.Enqueue(() => Refresh(LobbyManager.CurrentLobby));
        }

        private static string NormalizeHexColor(string color)
        {
            if (string.IsNullOrWhiteSpace(color)) return null;

            var value = color.Trim().TrimStart('#');
            if (value.Length == 6) value += "ff";
            if (value.Length != 8) return null;

            for (var i = 0; i < value.Length; i++)
            {
                if (!Uri.IsHexDigit(value[i])) return null;
            }

            return value;
        }

        private static string EscapeRichText(string value)
        {
            return value?.Replace("<", "＜").Replace(">", "＞") ?? string.Empty;
        }

        private static string ColorText(string text, string color)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;
            if (string.IsNullOrEmpty(color)) return text;
            return $"<color=#{color}>{text}</color>";
        }

        private static string Truncate(string value, int maxLength)
        {
            if (string.IsNullOrEmpty(value) || value.Length <= maxLength) return value;
            return value.Substring(0, maxLength) + "...";
        }

        private static void ApplyGameFont(Text text)
        {
            NativeFontCache.ApplyTo(text);
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

        private sealed class HiddenObjectState
        {
            public HiddenObjectState(GameObject target, bool activeSelf)
            {
                Target = target;
                ActiveSelf = activeSelf;
            }

            public GameObject Target { get; }
            public bool ActiveSelf { get; }
        }

        private sealed class RoomInfoPlayer
        {
            public RoomInfoPlayer(string uid, string name, string avatarName, string avatarData)
            {
                Uid = uid;
                Name = string.IsNullOrWhiteSpace(name) ? uid : name;
                AvatarName = avatarName;
                AvatarData = avatarData;
            }

            public string Uid { get; }
            public string Name { get; }
            public string AvatarName { get; }
            public string AvatarData { get; }
        }

        private sealed class PlayerRowView
        {
            private readonly GameObject _root;
            private readonly RectTransform _rect;
            private readonly Image _avatar;
            private readonly Text _name;
            private readonly Text _state;

            public PlayerRowView(GameObject root, RectTransform rect, Image avatar, Text name, Text state)
            {
                _root = root;
                _rect = rect;
                _avatar = avatar;
                _name = name;
                _state = state;
            }

            public bool IsAlive => _root != null &&
                                   _rect != null &&
                                   _avatar != null &&
                                   _name != null &&
                                   _state != null;

            public void Set(
                float y,
                string uid,
                string avatarName,
                string avatarData,
                string playerName,
                string playerColor,
                string stateText,
                string stateColor)
            {
                if (_root != null && !_root.activeSelf)
                {
                    _root.SetActive(true);
                }

                if (_rect != null)
                {
                    _rect.anchoredPosition = new Vector2(InfoPanelPadding, -y);
                }

                if (_avatar != null)
                {
                    _avatar.sprite = AvatarManager.GetAvatarSprite(uid, avatarName, avatarData);
                    _avatar.enabled = _avatar.sprite != null;
                }

                if (_name != null)
                {
                    _name.text = $"<color=#{playerColor}>{playerName}</color>";
                }

                if (_state != null)
                {
                    _state.text = string.IsNullOrEmpty(stateText)
                        ? string.Empty
                        : ColorText(stateText, stateColor);
                }
            }

            public void Hide()
            {
                if (_root != null) _root.SetActive(false);
            }
        }

        private readonly struct PlayerStateText
        {
            public PlayerStateText(string text, string color)
            {
                Text = text;
                Color = color;
            }

            public string Text { get; }
            public string Color { get; }
        }
    }
}
