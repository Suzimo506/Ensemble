using System;
using System.Collections.Generic;
using MDEN.Managers;
using MDEN.Protocol.Messages.Lobby;
using UnityEngine;
using UnityEngine.UI;

namespace MDEN.UI.Core
{
    public static class RoomSceneOverlay
    {
        private const string NativeBackgroundPath = "UI/Standerd/PnlHome/PnlBgSwitchFsv";
        private const int OverlaySortingOrder = 32767;

        private static readonly Dictionary<string, HiddenObjectState> OriginalStates = new Dictionary<string, HiddenObjectState>();
        private static readonly Dictionary<string, string> PlayerColorCache = new Dictionary<string, string>();
        private static readonly HashSet<string> PendingColorRequests = new HashSet<string>();
        private static GameObject _frame;
        private static Text _roomInfo;
        private static Text _fontTemplate;

        public static bool IsCreated => _frame != null;
        public static bool IsHomeReady => GameObject.Find("UI/Standerd/PnlHome") != null;
        public static bool IsNavigationReady => GameObject.Find("UI/Standerd/PnlNavigation") != null;
        public static bool IsReady => IsHomeReady && IsNavigationReady;
        public static bool IsHomeVisible => GetHomeVisible();

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

            EnsureFrame();
            if (_frame == null) return;

            if (!IsHomeVisible)
            {
                _frame.SetActive(false);
                return;
            }

            EnsureNativeBackgroundVisible();
            HideSinglePlayerControls();
            EnsureHomeStartButtonVisible();
            _frame.SetActive(true);
            EnsureOverlayOrder();
            _roomInfo.text = FormatRoomInfo(lobby);
        }

        public static void Hide()
        {
            if (_frame != null) _frame.SetActive(false);
        }

        public static void UpdateVisibility()
        {
            if (_frame == null) return;
            var visible = LobbyManager.IsInLobby && IsHomeVisible;
            _frame.SetActive(visible);
            if (visible)
            {
                EnsureNativeBackgroundVisible();
                EnsureOverlayOrder();
                if (_roomInfo != null)
                {
                    _roomInfo.text = FormatRoomInfo(LobbyManager.CurrentLobby);
                }
            }
        }

        public static void Destroy()
        {
            RestoreSinglePlayerControls();
            EnsureNativeBackgroundVisible();

            if (_frame != null)
            {
                UnityEngine.Object.Destroy(_frame);
                _frame = null;
                _roomInfo = null;
            }
        }

        private static void EnsureFrame()
        {
            if (_frame != null) return;

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

            _roomInfo = CreateText("RoomInfo", -25f, -90f, 26, TextAnchor.UpperRight);
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
                   !IsPanelVisible("UI/Standerd/PnlStage") &&
                   !IsPanelVisible("UI/Standerd/PnlPreparation");
        }

        private static bool IsPanelVisible(string path)
        {
            var obj = GameObject.Find(path);
            return obj != null && obj.activeInHierarchy;
        }

        private static Text CreateText(string name, float x, float y, int fontSize, TextAnchor anchor)
        {
            var obj = new GameObject(name);
            var rect = obj.AddComponent<RectTransform>();
            rect.SetParent(_frame.transform);
            rect.localScale = Vector3.one;
            rect.anchorMin = new Vector2(1f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 1f);
            rect.anchoredPosition = new Vector2(x, y);
            rect.sizeDelta = new Vector2(520f, fontSize * 4f);

            var text = obj.AddComponent<Text>();
            ApplyGameFont(text);
            text.fontSize = fontSize;
            text.alignment = anchor;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.supportRichText = true;
            text.raycastTarget = false;
            text.color = Color.white;

            var shadow = obj.AddComponent<Shadow>();
            shadow.effectDistance = new Vector2(2f, -2f);
            shadow.effectColor = new Color(0f, 0f, 0f, 0.3f);

            return text;
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

        private static void EnsureHomeStartButtonVisible()
        {
            var startButton = GameObject.Find("UI/Standerd/PnlHome/Bottom/Btn");
            if (startButton != null && !startButton.activeSelf)
            {
                startButton.SetActive(true);
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

        private static string FormatRoomInfo(LobbySyncPush lobby)
        {
            var roomName = EscapeRichText(lobby.Name);
            var hostName = EscapeRichText(GetHostName(lobby));
            var hostColor = GetPlayerColor(lobby.HostUid);
            return $"<color=#{Constants.ColorYellow}>【{roomName}】</color> {GetPlayerCount(lobby)}/{lobby.MaxPlayers}\n" +
                   $"房主：<color=#{hostColor}>【{hostName}】</color>";
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
            if (lobby.PlayerDetails != null && lobby.PlayerDetails.Length > 0) return lobby.PlayerDetails.Length;
            return lobby.Players?.Length ?? 0;
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
                if (PlayerColorCache.TryGetValue(uid, out var cachedColor))
                {
                    return cachedColor;
                }

                RequestPlayerColor(uid);
            }

            return "ffffffff";
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

        private static void ApplyGameFont(Text text)
        {
            if (text == null) return;

            var template = FindNativeFontTemplate();
            if (template != null && template.font != null)
            {
                text.font = template.font;
                text.material = template.material;
                return;
            }

            text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        }

        private static Text FindNativeFontTemplate()
        {
            if (_fontTemplate != null && _fontTemplate.font != null) return _fontTemplate;

            var candidates = Resources.FindObjectsOfTypeAll<Text>();
            foreach (var text in candidates)
            {
                if (text == null || text.font == null) continue;
                var fontName = text.font.name ?? string.Empty;
                if (!fontName.Contains("Arial"))
                {
                    _fontTemplate = text;
                    return _fontTemplate;
                }
            }

            return null;
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
    }
}
