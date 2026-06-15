using System.Collections.Generic;
using System.Linq;
using Il2CppAssets.Scripts.PeroTools.Commons;
using MDEN.Managers;
using MDEN.Protocol.Messages.Chat;
using MDEN.UI.Core;
using MelonLoader;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace MDEN.UI.Displays
{
    public sealed class RoomChatDisplay
    {
        private const int CollapsedMessageLimit = 5;
        private const int ExpandedMessageLimit = 12;
        private const float EntryHeight = 24f;
        private const float InputHeight = 38f;
        private const float Padding = 14f;
        private const float IdleFadeDelay = 4f;
        private const float BackgroundFadeSpeed = 3.5f;
        private static readonly Vector2 CollapsedSize = new Vector2(500f, 190f);
        private static readonly Vector2 ExpandedSize = new Vector2(560f, 360f);

        private readonly List<ChatPushMsg> _messages = new List<ChatPushMsg>();
        private readonly List<Text> _messageTexts = new List<Text>();

        private GameObject _root;
        private RectTransform _rootRect;
        private Image _background;
        private ScrollRect _scrollRect;
        private RectTransform _viewportRect;
        private RectTransform _contentRect;
        private InputField _inputField;
        private Image _inputBackground;
        private Text _inputText;
        private Text _placeholderText;
        private float _lastActivityTime;
        private bool _lastFocusState;
        private bool _lastInputBlocked;
        private bool _suppressSubmit;
        private bool _sendInProgress;
        private int _clearSlashFrame = -1;
        private int _suppressGameInputUntilFrame = -1;

        public bool IsCreated => _root != null;
        public bool IsInputFocused => _inputField != null && _inputField.isFocused;
        public bool IsConsumingInput => IsInputFocused || _sendInProgress || Time.frameCount <= _suppressGameInputUntilFrame;

        public void CreateEmpty()
        {
            EnsureCreated();
            RefreshLayout();
        }

        public void AddMessage(ChatPushMsg message)
        {
            if (message == null) return;

            EnsureCreated();
            MarkActive();
            _messages.Add(message);
            while (_messages.Count > ExpandedMessageLimit)
            {
                _messages.RemoveAt(0);
            }

            RedrawMessages();
            ScrollToBottom();
        }

        public void Destroy()
        {
            _messages.Clear();
            _messageTexts.Clear();
            _rootRect = null;
            _background = null;
            _scrollRect = null;
            _viewportRect = null;
            _contentRect = null;
            _inputField = null;
            _inputBackground = null;
            _inputText = null;
            _placeholderText = null;
            _lastActivityTime = 0f;
            _lastFocusState = false;
            _lastInputBlocked = false;
            _suppressSubmit = false;
            _sendInProgress = false;
            _clearSlashFrame = -1;
            _suppressGameInputUntilFrame = -1;
            SetGameInputBlocked(false);

            if (_root != null)
            {
                UnityEngine.Object.Destroy(_root);
                _root = null;
            }
        }

        private void EnsureCreated()
        {
            if (_root != null) return;

            var parent = FindChatParent();
            if (parent == null)
            {
                MelonLogger.Warning("Cannot create room chat: no usable UI parent found.");
                return;
            }

            _root = new GameObject("MDENRoomChat");
            _rootRect = _root.AddComponent<RectTransform>();
            _rootRect.SetParent(parent, false);
            _rootRect.localScale = Vector3.one;
            _rootRect.anchorMin = new Vector2(0f, 1f);
            _rootRect.anchorMax = new Vector2(0f, 1f);
            _rootRect.pivot = new Vector2(0f, 1f);
            _rootRect.anchoredPosition = new Vector2(10f, -80f);
            _root.transform.SetAsLastSibling();

            _background = _root.AddComponent<Image>();
            _lastActivityTime = Time.unscaledTime;
            _background.color = GetBackgroundColor(false, false, true);

            CreateScrollArea();
            CreateInputField();
            RefreshLayout();
            MelonLogger.Msg($"Room chat display created under {GetHierarchyPath(parent)}.");
        }

        private void CreateScrollArea()
        {
            var viewport = new GameObject("Viewport");
            _viewportRect = viewport.AddComponent<RectTransform>();
            _viewportRect.SetParent(_root.transform);
            _viewportRect.localScale = Vector3.one;
            _viewportRect.anchorMin = new Vector2(0f, 0f);
            _viewportRect.anchorMax = new Vector2(1f, 1f);
            _viewportRect.offsetMin = new Vector2(Padding, Padding + InputHeight);
            _viewportRect.offsetMax = new Vector2(-Padding, -Padding);

            var mask = viewport.AddComponent<RectMask2D>();
            mask.padding = new Vector4(0f, 8f, 0f, 8f);
            mask.softness = new Vector2Int(8, 8);

            var content = new GameObject("Content");
            _contentRect = content.AddComponent<RectTransform>();
            _contentRect.SetParent(viewport.transform);
            _contentRect.localScale = Vector3.one;
            _contentRect.anchorMin = new Vector2(0f, 1f);
            _contentRect.anchorMax = new Vector2(1f, 1f);
            _contentRect.pivot = new Vector2(0.5f, 1f);
            _contentRect.anchoredPosition = Vector2.zero;

            _scrollRect = viewport.AddComponent<ScrollRect>();
            _scrollRect.content = _contentRect;
            _scrollRect.horizontal = false;
            _scrollRect.vertical = true;
            _scrollRect.scrollSensitivity = 8f;
        }

        private void CreateInputField()
        {
            var inputObj = new GameObject("InputField");
            var inputRootRect = inputObj.AddComponent<RectTransform>();
            inputRootRect.SetParent(_root.transform, false);
            inputRootRect.localScale = Vector3.one;
            inputRootRect.anchorMin = new Vector2(0f, 0f);
            inputRootRect.anchorMax = new Vector2(1f, 0f);
            inputRootRect.pivot = new Vector2(0.5f, 0f);
            inputRootRect.offsetMin = new Vector2(Padding, Padding);
            inputRootRect.offsetMax = new Vector2(-Padding, Padding + InputHeight);

            _inputBackground = inputObj.AddComponent<Image>();
            _inputBackground.color = GetInputBackgroundColor(false, true);
            _inputBackground.raycastTarget = true;

            _inputText = CreateText("InputText", inputObj.transform, 18, TextAnchor.MiddleLeft);
            _inputText.color = new Color(1f, 1f, 1f, 0.95f);
            _inputText.raycastTarget = false;

            var inputRect = _inputText.GetComponent<RectTransform>();
            inputRect.anchorMin = new Vector2(0f, 0f);
            inputRect.anchorMax = new Vector2(1f, 1f);
            inputRect.pivot = new Vector2(0.5f, 0.5f);
            inputRect.offsetMin = new Vector2(12f, 0f);
            inputRect.offsetMax = new Vector2(-12f, 0f);

            _inputField = inputObj.AddComponent<InputField>();
            _inputField.targetGraphic = _inputBackground;
            _inputField.textComponent = _inputText;
            _inputField.lineType = InputField.LineType.SingleLine;
            _inputField.characterLimit = 120;
            _inputField.onEndEdit.AddListener((UnityAction<string>)OnSubmit);
            _inputField.onValueChanged.AddListener((UnityAction<string>)(_ =>
            {
                MarkActive();
                if (RefreshLayout()) RedrawMessages();
            }));

            var placeholderObj = UnityEngine.Object.Instantiate(_inputText.gameObject, _inputText.transform.parent);
            placeholderObj.name = "Placeholder";
            UnityEngine.Object.Destroy(placeholderObj.GetComponent<InputField>());

            _placeholderText = placeholderObj.GetComponent<Text>();
            _placeholderText.text = "按“/”或点击输入框输入消息";
            _placeholderText.color = new Color(1f, 1f, 1f, 0.40f);
            _placeholderText.raycastTarget = false;
            _inputField.placeholder = _placeholderText;
        }

        private Text CreateText(string name, Transform parent, int fontSize, TextAnchor anchor)
        {
            var obj = new GameObject(name);
            var rect = obj.AddComponent<RectTransform>();
            rect.SetParent(parent);
            rect.localScale = Vector3.one;

            var text = obj.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            text.fontSize = fontSize;
            text.alignment = anchor;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.supportRichText = true;
            text.color = Color.white;
            text.raycastTarget = false;
            return text;
        }

        private void RedrawMessages()
        {
            if (_root == null) return;

            _messageTexts.RemoveAll(text => text == null);
            RefreshLayout();
            if (_root == null || _contentRect == null) return;

            var visibleMessages = _messages
                .Skip(System.Math.Max(0, _messages.Count - GetVisibleLimit()))
                .ToArray();

            EnsureMessageTextCount(visibleMessages.Length);
            for (var i = 0; i < _messageTexts.Count; i++)
            {
                if (_messageTexts[i] == null) continue;

                var active = i < visibleMessages.Length;
                _messageTexts[i].gameObject.SetActive(active);
                if (!active) continue;
                _messageTexts[i].text = FormatMessage(visibleMessages[i]);
            }

            PositionMessages(visibleMessages.Length);
        }

        private void EnsureMessageTextCount(int count)
        {
            while (_messageTexts.Count < count)
            {
                if (_contentRect == null) return;

                var text = CreateText($"Message{_messageTexts.Count}", _contentRect, 18, TextAnchor.UpperLeft);
                var rect = text.GetComponent<RectTransform>();
                rect.anchorMin = new Vector2(0f, 1f);
                rect.anchorMax = new Vector2(1f, 1f);
                rect.pivot = new Vector2(0f, 1f);
                rect.sizeDelta = new Vector2(0f, EntryHeight);
                _messageTexts.Add(text);
            }
        }

        private void PositionMessages(int count)
        {
            if (_contentRect == null) return;

            var y = 0f;
            for (var i = 0; i < count; i++)
            {
                var text = _messageTexts[i];
                if (text == null) continue;

                var rect = text.GetComponent<RectTransform>();
                rect.anchoredPosition = new Vector2(0f, -y);
                rect.sizeDelta = new Vector2(0f, EntryHeight);
                y += EntryHeight;
            }

            _contentRect.sizeDelta = new Vector2(0f, System.Math.Max(y, GetViewportHeight()));
        }

        private bool RefreshLayout()
        {
            if (_root == null || _rootRect == null || _background == null) return false;

            var focused = _inputField != null && _inputField.isFocused;
            var focusChanged = focused != _lastFocusState;
            var shouldBlockInput = IsConsumingInput;
            _lastFocusState = focused;
            if (shouldBlockInput || shouldBlockInput != _lastInputBlocked)
            {
                _lastInputBlocked = shouldBlockInput;
                SetGameInputBlocked(shouldBlockInput);
            }

            _rootRect.sizeDelta = focused ? ExpandedSize : CollapsedSize;
            var active = IsRecentlyActive();
            _background.color = Color.Lerp(
                _background.color,
                GetBackgroundColor(focused, HasMessages(), active),
                Mathf.Clamp01(Time.unscaledDeltaTime * BackgroundFadeSpeed));

            if (_inputBackground != null)
            {
                _inputBackground.color = Color.Lerp(
                    _inputBackground.color,
                    GetInputBackgroundColor(focused, active),
                    Mathf.Clamp01(Time.unscaledDeltaTime * BackgroundFadeSpeed));
            }

            return focusChanged;
        }

        private void ScrollToBottom()
        {
            if (_scrollRect == null) return;
            Canvas.ForceUpdateCanvases();
            _scrollRect.verticalNormalizedPosition = 0f;
        }

        private int GetVisibleLimit()
        {
            return _inputField != null && _inputField.isFocused
                ? ExpandedMessageLimit
                : CollapsedMessageLimit;
        }

        private float GetViewportHeight()
        {
            return (GetVisibleLimit() * EntryHeight) + 8f;
        }

        private bool HasMessages()
        {
            return _messages.Count > 0;
        }

        private bool IsRecentlyActive()
        {
            return IsConsumingInput || Time.unscaledTime - _lastActivityTime <= IdleFadeDelay;
        }

        private void MarkActive()
        {
            _lastActivityTime = Time.unscaledTime;
        }

        private static Color GetBackgroundColor(bool focused, bool hasMessages, bool active)
        {
            if (focused) return new Color(0.08f, 0.03f, 0.16f, 0.74f);
            if (active) return new Color(0.08f, 0.03f, 0.16f, 0.52f);
            return hasMessages
                ? new Color(0.08f, 0.03f, 0.16f, 0.22f)
                : new Color(0.08f, 0.03f, 0.16f, 0.14f);
        }

        private static Color GetInputBackgroundColor(bool focused, bool active)
        {
            if (focused) return new Color(0.22f, 0.08f, 0.42f, 0.68f);
            return active
                ? new Color(0.18f, 0.06f, 0.34f, 0.42f)
                : new Color(0.18f, 0.06f, 0.34f, 0.18f);
        }

        private static Transform FindChatParent()
        {
            return GameObject.Find("UI/Standerd/PnlNavigation")?.transform
                ?? GameObject.Find("UI/Standerd")?.transform
                ?? GameObject.Find("UI")?.transform;
        }

        private static string GetHierarchyPath(Transform transform)
        {
            if (transform == null) return "<null>";

            var names = new List<string>();
            var current = transform;
            while (current != null)
            {
                names.Add(current.name);
                current = current.parent;
            }

            names.Reverse();
            return string.Join("/", names);
        }

        private static string FormatMessage(ChatPushMsg message)
        {
            if (message.IsSystem)
            {
                return FormatSystemMessage(message);
            }

            var author = string.IsNullOrEmpty(message.AuthorName) ? message.AuthorUid : message.AuthorName;
            return $"<color=#{Constants.ColorCyan}>【{author}】</color>: {message.Message}";
        }

        private static string FormatSystemMessage(ChatPushMsg message)
        {
            if ((message.Message == "PlaylistAdd" || message.Message == "PlaylistRemove") &&
                !string.IsNullOrEmpty(message.ExtraData))
            {
                var parts = message.ExtraData.Split(new[] { '#' }, 2);
                if (parts.Length == 2)
                {
                    var action = message.Message == "PlaylistAdd" ? "加入" : "移除";
                    return $"<color=#{Constants.ColorPink}>【{EscapeRichText(parts[0])}】</color>将<color=#{Constants.ColorYellow}>【{EscapeRichText(parts[1])}】</color>{action}歌曲列表";
                }
            }

            return $"<color=#{Constants.ColorYellow}>[系统]</color> {message.Message}";
        }

        private static void SetGameInputBlocked(bool blocked)
        {
            try
            {
                var manager = Singleton<Il2CppAssets.Scripts.PeroTools.Managers.InputManager>.instance;
                if (manager != null) manager.isStopKeyAction = blocked;
            }
            catch (System.Exception ex)
            {
                MelonLogger.Warning($"Set game input block failed: {ex.Message}");
            }
        }

        public void Update()
        {
            if (_root == null) return;
            HandleFocusShortcut();
            if (RefreshLayout()) RedrawMessages();
        }

        private void HandleFocusShortcut()
        {
            if (_inputField == null || IsInputFocused) return;
            if (!Input.GetKeyDown(KeyCode.Slash)) return;

            _suppressGameInputUntilFrame = Time.frameCount + 2;
            _clearSlashFrame = Time.frameCount + 1;
            MarkActive();
            _inputField.Select();
            _inputField.ActivateInputField();
            _inputField.MoveTextEnd(false);
            MainThreadDispatcher.Enqueue(ClearSlashShortcutText);
        }

        private void ClearSlashShortcutText()
        {
            if (_inputField == null || Time.frameCount < _clearSlashFrame) return;
            if (_inputField.text == "/") _inputField.text = string.Empty;
            _clearSlashFrame = -1;
        }

        private async void OnSubmit(string value)
        {
            if (_suppressSubmit || _sendInProgress) return;

            _suppressGameInputUntilFrame = Time.frameCount + 2;
            var message = SanitizeMessage(value);
            if (string.IsNullOrEmpty(message))
            {
                RefreshLayout();
                return;
            }

            _sendInProgress = true;
            MarkActive();
            try
            {
                await ChatManager.SendAsync(message);
                MainThreadDispatcher.Enqueue(ClearSubmittedInput);
            }
            catch (System.Exception ex)
            {
                MelonLogger.Warning($"Send chat failed: {ex.Message}");
                MainThreadDispatcher.Enqueue(FinishFailedSubmit);
            }
        }

        private void ClearSubmittedInput()
        {
            if (_inputField == null)
            {
                _sendInProgress = false;
                SetGameInputBlocked(false);
                return;
            }

            _suppressSubmit = true;
            _inputField.text = string.Empty;
            _inputField.DeactivateInputField();
            _suppressSubmit = false;
            _sendInProgress = false;
            _suppressGameInputUntilFrame = Time.frameCount + 1;
            RedrawMessages();
        }

        private void FinishFailedSubmit()
        {
            _sendInProgress = false;
            RefreshLayout();
        }

        private static string SanitizeMessage(string value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? string.Empty
                : value.Trim().Replace("\r", string.Empty).Replace("\n", string.Empty);
        }

        private static string EscapeRichText(string value)
        {
            return value?.Replace("<", "＜").Replace(">", "＞") ?? string.Empty;
        }
    }
}
