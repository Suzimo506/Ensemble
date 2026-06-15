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
        private Text _inputText;
        private Text _placeholderText;
        private bool _lastFocusState;
        private bool _lastInputBlocked;
        private bool _suppressSubmit;
        private bool _sendInProgress;
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
            _inputText = null;
            _placeholderText = null;
            _lastFocusState = false;
            _lastInputBlocked = false;
            _suppressSubmit = false;
            _sendInProgress = false;
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
            _background.color = GetBackgroundColor(false);

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
            _inputText = CreateText("Input", _root.transform, 18, TextAnchor.MiddleLeft);
            var inputRect = _inputText.GetComponent<RectTransform>();
            inputRect.anchorMin = new Vector2(0f, 0f);
            inputRect.anchorMax = new Vector2(1f, 0f);
            inputRect.pivot = new Vector2(0.5f, 0f);
            inputRect.offsetMin = new Vector2(Padding + 8f, Padding);
            inputRect.offsetMax = new Vector2(-(Padding + 8f), Padding + InputHeight);

            _inputText.raycastTarget = true;
            _inputField = _inputText.gameObject.AddComponent<InputField>();
            _inputField.textComponent = _inputText;
            _inputField.lineType = InputField.LineType.SingleLine;
            _inputField.characterLimit = 120;
            _inputField.onEndEdit.AddListener((UnityAction<string>)OnSubmit);
            _inputField.onValueChanged.AddListener((UnityAction<string>)(_ =>
            {
                if (RefreshLayout()) RedrawMessages();
            }));

            var placeholderObj = UnityEngine.Object.Instantiate(_inputText.gameObject, _inputText.transform.parent);
            placeholderObj.name = "Placeholder";
            UnityEngine.Object.Destroy(placeholderObj.GetComponent<InputField>());

            _placeholderText = placeholderObj.GetComponent<Text>();
            _placeholderText.text = "输入聊天消息";
            _placeholderText.color = new Color(1f, 1f, 1f, 0.48f);
            _placeholderText.raycastTarget = false;
            _placeholderText.GetComponent<RectTransform>().anchoredPosition = inputRect.anchoredPosition;
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
            _background.color = GetBackgroundColor(focused);
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

        private static Color GetBackgroundColor(bool focused)
        {
            return focused
                ? new Color(0.08f, 0.03f, 0.16f, 0.72f)
                : new Color(0.08f, 0.03f, 0.16f, 0.56f);
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
                return $"<color=#{Constants.ColorYellow}>[系统]</color> {message.Message}";
            }

            var author = string.IsNullOrEmpty(message.AuthorName) ? message.AuthorUid : message.AuthorName;
            return $"<color=#{Constants.ColorCyan}>【{author}】</color>: {message.Message}";
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
            if (RefreshLayout()) RedrawMessages();
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
    }
}
