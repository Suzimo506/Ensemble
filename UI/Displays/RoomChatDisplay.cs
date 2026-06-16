using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.RegularExpressions;
using Il2CppAssets.Scripts.UI.Controls;
using MDEN.Managers;
using MDEN.Protocol.Messages.Chat;
using MDEN.UI.Core;
using MelonLoader;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Events;
using UnityEngine.UI;

namespace MDEN.UI.Displays
{
    public sealed class RoomChatDisplay
    {
        private GameObject _frame;
        private GameObject _scrollFrame;
        private Image _backgroundImage;
        private ScrollRect _scrollRect;
        private InputField _inputField;
        private Button _clearButton;
        private bool _sendInProgress;
        private bool _gameInputBlocked;
        private int _clearSlashFrame = -1;
        private int _suppressGameInputUntilFrame = -1;

        public bool IsConsumingInput => (_inputField != null && _inputField.isFocused)
            || _sendInProgress
            || Time.frameCount <= _suppressGameInputUntilFrame;
        public bool IsCreated => _frame != null;
        public void CreateEmpty() => Initialize();

        public void InvalidatePlayerColors()
        {
            _playerColorCache.Clear();
            _pendingColorRequests.Clear();
            RefreshMessageTexts();
        }

        private readonly Dictionary<ChatPushMsg, Text> _textList = new Dictionary<ChatPushMsg, Text>();
        private readonly List<ChatPushMsg> _messages = new List<ChatPushMsg>();
        private readonly Dictionary<string, string> _playerColorCache = new Dictionary<string, string>();
        private readonly HashSet<string> _pendingColorRequests = new HashSet<string>();

        private static Sprite _btnBaseSprite;
        private static Text _nativeFontTemplate;
        private readonly Stopwatch _lastForceUpdate = Stopwatch.StartNew();

        private const float EntryWidth = 480f;
        private const int FontSize = 20;
        private const float EmptyVisibleLines = 0.75f;
        private const int MaxVisibleLines = 9;
        private const float OutlineOffset = 25f;
        private const float InputHeight = 60f;
        private const float InputGap = 8f;
        private const float FramePadding = 10f;
        private const float InputLeftPadding = 10f;
        private const float InputRightPadding = 42f;
        private const float InputVerticalPadding = 2f;
        private static readonly Color BackgroundDefaultColor = new Color(0f, 0f, 0f, 0.15f);
        private static readonly Color BackgroundFocusedColor = new Color(0f, 0f, 0f, 0.32f);
        private static readonly Color InputClearButtonBgColor = new Color(0.42f, 0.16f, 0.66f, 0.95f);
        private static readonly Color InputClearButtonIconColor = new Color(0.88f, 0.46f, 1f, 1f);
        private const string WhiteTextColor = "ffffffff";
        private const string GreenTextColor = "66ff66ff";
        private const string RedTextColor = "ff5555ff";
        private const string PinkTextColor = Constants.ColorPink;
        private readonly Vector2 _entrySize = new Vector2(EntryWidth, FontSize + 8f);

        private Vector2 GetFrameSize(int lines)
        {
            return GetScrollFrameSize(lines);
        }

        private Vector2 GetScrollFrameSize(int lines)
        {
            var messageAreaHeight = _entrySize.y * GetVisibleMessageLines(lines);
            var height = messageAreaHeight + InputHeight + InputGap + FramePadding * 2f;
            return new Vector2(_entrySize.x + OutlineOffset, height);
        }

        private static float GetVisibleMessageLines(int lines)
        {
            if (lines <= 0) return EmptyVisibleLines;
            return Math.Min(lines, MaxVisibleLines);
        }

        public void Initialize()
        {
            if (_frame != null) return;

            var parent = GameObject.Find("UI/Standerd/PnlNavigation")?.transform;
            if (parent == null) return;

            _textList.Clear();

            if (_btnBaseSprite == null)
            {
                try
                {
                    _btnBaseSprite = Addressables.LoadAssetAsync<Sprite>("BtnBase").WaitForCompletion();
                }
                catch
                {
                    _btnBaseSprite = null;
                }
            }

            _frame = new GameObject("MDENRoomChat");
            var frameRect = _frame.AddComponent<RectTransform>();
            frameRect.SetParent(parent, false);
            frameRect.localScale = Vector3.one;
            frameRect.anchorMin = new Vector2(0f, 1f);
            frameRect.anchorMax = new Vector2(0f, 1f);
            frameRect.pivot = new Vector2(0f, 1f);
            frameRect.anchoredPosition = new Vector2(10f, -80f);
            _frame.transform.SetAsLastSibling();

            _scrollFrame = new GameObject("ScrollFrame");
            var scrollRectTrans = _scrollFrame.AddComponent<RectTransform>();
            scrollRectTrans.SetParent(_frame.transform, false);
            scrollRectTrans.localScale = Vector3.one;
            scrollRectTrans.anchorMin = new Vector2(0f, 1f);
            scrollRectTrans.anchorMax = new Vector2(0f, 1f);
            scrollRectTrans.pivot = new Vector2(0f, 1f);
            scrollRectTrans.anchoredPosition = Vector2.zero;

            _backgroundImage = _scrollFrame.AddComponent<Image>();
            _backgroundImage.type = Image.Type.Tiled;
            _backgroundImage.sprite = _btnBaseSprite;
            _backgroundImage.color = BackgroundDefaultColor;

            var viewport = new GameObject("Viewport");
            var viewportRect = viewport.AddComponent<RectTransform>();
            viewportRect.SetParent(_scrollFrame.transform, false);
            viewportRect.localScale = Vector3.one;
            viewportRect.anchorMin = Vector2.zero;
            viewportRect.anchorMax = Vector2.one;
            viewportRect.offsetMin = new Vector2(FramePadding, FramePadding + InputHeight + InputGap);
            viewportRect.offsetMax = new Vector2(-FramePadding, -FramePadding);
            viewport.AddComponent<RectMask2D>();

            var content = new GameObject("Content");
            var contentRect = content.AddComponent<RectTransform>();
            contentRect.SetParent(viewport.transform, false);
            contentRect.localScale = Vector3.one;
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.pivot = new Vector2(0f, 1f);
            contentRect.anchoredPosition = Vector2.zero;

            _scrollRect = _scrollFrame.AddComponent<ScrollRect>();
            _scrollRect.content = contentRect;
            _scrollRect.viewport = viewportRect;
            _scrollRect.horizontal = false;
            _scrollRect.vertical = true;
            _scrollRect.scrollSensitivity = 16f;

            if (!CreateNativeInputField(_scrollFrame.transform))
            {
                UnityEngine.Object.Destroy(_frame);
                _frame = null;
                _scrollFrame = null;
                _backgroundImage = null;
                _scrollRect = null;
                return;
            }

            foreach (var msg in _messages)
            {
                var text = AddTextToContent(msg, _scrollRect.content);
                text.text = FormatMessage(msg);
            }

            Canvas.ForceUpdateCanvases();
            ResizeMessageTexts();
            UpdateLayout();
            ScrollToBottom();
        }

        public void Destroy()
        {
            DestroyInternal(true);
        }

        public void ResetSceneObjects()
        {
            DestroyInternal(false);
        }

        private void DestroyInternal(bool clearMessages)
        {
            SetGameInputBlocked(false);
            _sendInProgress = false;
            _clearSlashFrame = -1;
            _suppressGameInputUntilFrame = -1;

            if (_inputField != null)
            {
                _inputField.onEndEdit.RemoveAllListeners();
                _inputField.onValueChanged.RemoveAllListeners();
            }

            if (_clearButton != null)
            {
                _clearButton.onClick.RemoveAllListeners();
            }

            foreach (var txt in _textList.Values)
            {
                if (txt != null && txt.gameObject != null)
                {
                    UnityEngine.Object.Destroy(txt.gameObject);
                }
            }

            _textList.Clear();
            if (clearMessages)
            {
                _messages.Clear();
            }
            _inputField = null;
            _clearButton = null;
            _backgroundImage = null;

            if (_frame != null)
            {
                UnityEngine.Object.Destroy(_frame);
                _frame = null;
            }

            _scrollFrame = null;
            _scrollRect = null;
        }

        public void AddMessage(ChatPushMsg message)
        {
            if (message == null) return;

            _messages.Add(message);
            if (_messages.Count > 50)
            {
                var oldest = _messages[0];
                _messages.RemoveAt(0);
                if (_textList.TryGetValue(oldest, out var oldText))
                {
                    if (oldText != null) UnityEngine.Object.Destroy(oldText.gameObject);
                    _textList.Remove(oldest);
                }
            }

            if (_frame == null || _scrollRect == null) return;

            var text = AddTextToContent(message, _scrollRect.content);
            text.text = FormatMessage(message);

            Canvas.ForceUpdateCanvases();
            ResizeMessageText(text);
            UpdateLayout();
            ScrollToBottom();
        }

        public void Update()
        {
            if (_frame == null || _inputField == null) return;

            if (Input.GetKeyDown(KeyCode.Slash) && !_inputField.isFocused)
            {
                _suppressGameInputUntilFrame = Time.frameCount + 2;
                _clearSlashFrame = Time.frameCount + 1;
                SetGameInputBlocked(true);
                _inputField.Select();
                _inputField.ActivateInputField();
                _inputField.MoveTextEnd(false);
                MainThreadDispatcher.Enqueue(ClearSlashShortcutText);
            }

            UpdateInputVisualState(_inputField, _clearButton, _inputField.isFocused);
            UpdateBackgroundFocusState(_inputField.isFocused);
        }

        private bool CreateNativeInputField(Transform parent)
        {
            var templateInput = UnityEngine.Resources.FindObjectsOfTypeAll<Il2CppAssets.Scripts.UI.PeroInputField>();
            if (templateInput == null || templateInput.Length == 0)
            {
                MelonLogger.Warning("Cannot create native room chat input: no PeroInputField template found.");
                return false;
            }

            var inputObj = GameObject.Instantiate(templateInput[0].gameObject, parent);
            inputObj.name = "MDENRoomChatInput";
            inputObj.SetActive(true);

            var bgImg = inputObj.GetComponent<Image>();
            if (bgImg == null) bgImg = inputObj.AddComponent<Image>();
            bgImg.type = Image.Type.Sliced;
            bgImg.color = new Color(0f, 0f, 0f, 0.4f);

            var rect = inputObj.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.sizeDelta = new Vector2(_entrySize.x + OutlineOffset, InputHeight);

            var layoutElement = inputObj.GetComponent<LayoutElement>() ?? inputObj.AddComponent<LayoutElement>();
            layoutElement.ignoreLayout = true;

            _inputField = inputObj.GetComponent<InputField>();
            if (_inputField == null)
            {
                UnityEngine.Object.Destroy(inputObj);
                return false;
            }

            _clearButton = CreateClearButton(inputObj);
            ApplyInputTextPadding(_inputField, GetClearButtonReservedWidth(_clearButton));
            ConfigureInputField(_inputField, _clearButton);
            UpdateInputVisualState(_inputField, _clearButton, false);
            return true;
        }

        private void ConfigureInputField(InputField inputField, Button clearButton)
        {
            inputField.text = string.Empty;
            inputField.contentType = InputField.ContentType.Standard;
            inputField.characterValidation = InputField.CharacterValidation.None;
            inputField.characterLimit = 200;
            inputField.lineType = InputField.LineType.SingleLine;
            inputField.caretWidth = 2;

            if (inputField.textComponent != null)
            {
                inputField.textComponent.fontSize = 22;
                inputField.textComponent.alignment = TextAnchor.MiddleLeft;
                inputField.textComponent.color = Color.white;
                inputField.textComponent.supportRichText = false;
                ApplyGameFont(inputField.textComponent);
            }

            var placeholderTxt = inputField.placeholder?.GetComponent<Text>();
            if (placeholderTxt != null)
            {
                placeholderTxt.text = "按\"/\"或点击输入框输入消息";
                placeholderTxt.fontSize = inputField.textComponent != null ? inputField.textComponent.fontSize : 22;
                placeholderTxt.color = new Color(1f, 1f, 1f, 0.45f);
                placeholderTxt.alignment = TextAnchor.MiddleLeft;
                placeholderTxt.supportRichText = false;

                if (inputField.textComponent != null)
                {
                    var sourceRect = inputField.textComponent.GetComponent<RectTransform>();
                    var placeholderRect = placeholderTxt.GetComponent<RectTransform>();
                    if (sourceRect != null && placeholderRect != null)
                    {
                        placeholderRect.anchorMin = sourceRect.anchorMin;
                        placeholderRect.anchorMax = sourceRect.anchorMax;
                        placeholderRect.pivot = sourceRect.pivot;
                        placeholderRect.anchoredPosition = sourceRect.anchoredPosition;
                        placeholderRect.sizeDelta = sourceRect.sizeDelta;
                        placeholderRect.offsetMin = sourceRect.offsetMin;
                        placeholderRect.offsetMax = sourceRect.offsetMax;
                    }
                }
            }

            var trigger = inputField.gameObject.GetComponent<UnityEngine.EventSystems.EventTrigger>()
                ?? inputField.gameObject.AddComponent<UnityEngine.EventSystems.EventTrigger>();
            trigger.triggers.Clear();

            var selectEntry = new UnityEngine.EventSystems.EventTrigger.Entry
            {
                eventID = UnityEngine.EventSystems.EventTriggerType.Select
            };
            selectEntry.callback.AddListener((UnityAction<UnityEngine.EventSystems.BaseEventData>)new Action<UnityEngine.EventSystems.BaseEventData>(_ =>
            {
                SetGameInputBlocked(true);
                UpdateInputVisualState(inputField, clearButton, true);
                UpdateBackgroundFocusState(true);
            }));
            trigger.triggers.Add(selectEntry);

            var deselectEntry = new UnityEngine.EventSystems.EventTrigger.Entry
            {
                eventID = UnityEngine.EventSystems.EventTriggerType.Deselect
            };
            deselectEntry.callback.AddListener((UnityAction<UnityEngine.EventSystems.BaseEventData>)new Action<UnityEngine.EventSystems.BaseEventData>(_ =>
            {
                SetGameInputBlocked(false);
                UpdateInputVisualState(inputField, clearButton, false);
                UpdateBackgroundFocusState(false);
            }));
            trigger.triggers.Add(deselectEntry);

            inputField.onValueChanged.AddListener((UnityAction<string>)new Action<string>(_ =>
            {
                UpdateInputVisualState(inputField, clearButton, inputField.isFocused);
            }));

            if (clearButton != null)
            {
                clearButton.onClick.AddListener((UnityAction)new Action(() =>
                {
                    inputField.text = string.Empty;
                    UpdateInputVisualState(inputField, clearButton, inputField.isFocused);
                    inputField.ActivateInputField();
                }));
            }

            inputField.onEndEdit.AddListener((UnityAction<string>)OnInputSubmit);
        }

        private Text AddTextToContent(ChatPushMsg msg, RectTransform parent)
        {
            var obj = new GameObject(msg == null ? "InputEntry" : "ChatEntry");
            var rect = obj.AddComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.localScale = Vector3.one;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.sizeDelta = _entrySize;

            var text = obj.AddComponent<Text>();
            ApplyGameFont(text);
            text.fontSize = FontSize;
            text.alignment = TextAnchor.UpperLeft;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.supportRichText = true;
            text.color = Color.white;
            text.raycastTarget = false;

            if (msg != null)
            {
                _textList.Add(msg, text);
                ConfigureMessageClick(text, msg);
            }
            return text;
        }

        private void ConfigureMessageClick(Text text, ChatPushMsg msg)
        {
            var copyName = GetCopyableChartName(msg);
            if (string.IsNullOrWhiteSpace(copyName)) return;

            text.raycastTarget = true;
            var trigger = text.gameObject.AddComponent<UnityEngine.EventSystems.EventTrigger>();
            var clickEntry = new UnityEngine.EventSystems.EventTrigger.Entry
            {
                eventID = UnityEngine.EventSystems.EventTriggerType.PointerClick
            };
            clickEntry.callback.AddListener((UnityAction<UnityEngine.EventSystems.BaseEventData>)new Action<UnityEngine.EventSystems.BaseEventData>(_ =>
            {
                GUIUtility.systemCopyBuffer = copyName;
                ShowText.ShowInfo($"已复制谱面名: {copyName}");
            }));
            trigger.triggers.Add(clickEntry);
        }

        private void UpdateLayout()
        {
            if (_frame == null || _scrollFrame == null || _scrollRect == null) return;

            ResizeMessageTexts();
            int totalLines = GetTotalMessageLines();
            var scrollSize = GetScrollFrameSize(totalLines);
            var frameSize = GetFrameSize(totalLines);
            _frame.GetComponent<RectTransform>().sizeDelta = frameSize;
            _scrollFrame.GetComponent<RectTransform>().sizeDelta = frameSize;

            float currentY = 0f;
            foreach (var msg in _messages)
            {
                if (_textList.TryGetValue(msg, out var text))
                {
                    var rect = text.GetComponent<RectTransform>();
                    rect.anchoredPosition = new Vector2(0f, currentY);
                    currentY -= rect.sizeDelta.y;
                }
            }

            var viewportHeight = Math.Max(0f, scrollSize.y - FramePadding * 2f - InputHeight - InputGap);
            _scrollRect.content.sizeDelta = new Vector2(_entrySize.x, Math.Max(viewportHeight, -currentY));

            if (_inputField != null)
            {
                var inputRect = _inputField.GetComponent<RectTransform>();
                inputRect.anchorMin = new Vector2(0f, 0f);
                inputRect.anchorMax = new Vector2(1f, 0f);
                inputRect.pivot = new Vector2(0.5f, 0f);
                inputRect.offsetMin = new Vector2(FramePadding, FramePadding);
                inputRect.offsetMax = new Vector2(-FramePadding, FramePadding + InputHeight);
            }
        }

        private int GetTotalMessageLines()
        {
            if (_lastForceUpdate.ElapsedMilliseconds >= 100)
            {
                Canvas.ForceUpdateCanvases();
                _lastForceUpdate.Restart();
            }

            int count = 0;
            foreach (var text in _textList.Values)
            {
                var rect = text.GetComponent<RectTransform>();
                count += Math.Max(1, Mathf.CeilToInt((rect != null ? rect.sizeDelta.y : _entrySize.y) / _entrySize.y));
            }

            return count;
        }

        private void ResizeMessageTexts()
        {
            foreach (var text in _textList.Values)
            {
                ResizeMessageText(text);
            }
        }

        private void ResizeMessageText(Text text)
        {
            if (text == null) return;

            var rect = text.GetComponent<RectTransform>();
            if (rect == null) return;

            rect.sizeDelta = new Vector2(_entrySize.x, _entrySize.y);
            Canvas.ForceUpdateCanvases();

            var preferredHeight = text.preferredHeight;

            var height = Math.Max(_entrySize.y, preferredHeight + 4f);
            rect.sizeDelta = new Vector2(_entrySize.x, height);
        }

        private void ScrollToBottom()
        {
            if (_scrollRect != null)
            {
                _scrollRect.normalizedPosition = new Vector2(0f, 0f);
            }
        }

        private async void OnInputSubmit(string text)
        {
            var submittedByEnter = Input.GetKey(KeyCode.Return) || Input.GetKey(KeyCode.KeypadEnter);
            if (!submittedByEnter || _sendInProgress)
            {
                UpdateInputVisualState(_inputField, _clearButton, _inputField != null && _inputField.isFocused);
                return;
            }

            var message = SanitizeMessage(text);
            if (string.IsNullOrEmpty(message))
            {
                return;
            }

            _sendInProgress = true;
            _suppressGameInputUntilFrame = Time.frameCount + 2;

            try
            {
                using var _ = UIManager.LockUI("Sending message...");
                await ChatManager.SendAsync(message);
                MainThreadDispatcher.Enqueue(ClearSubmittedInput);
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Send chat failed: {ex.Message}");
                MainThreadDispatcher.Enqueue(FinishFailedSubmit);
            }
        }

        private void ClearSubmittedInput()
        {
            _sendInProgress = false;
            if (_inputField == null)
            {
                SetGameInputBlocked(false);
                return;
            }

            _inputField.text = string.Empty;
            UpdateInputVisualState(_inputField, _clearButton, true);
            _inputField.ActivateInputField();
        }

        private void FinishFailedSubmit()
        {
            _sendInProgress = false;
            UpdateInputVisualState(_inputField, _clearButton, _inputField != null && _inputField.isFocused);
        }

        private void ClearSlashShortcutText()
        {
            if (_inputField == null || Time.frameCount < _clearSlashFrame) return;
            if (_inputField.text == "/") _inputField.text = string.Empty;
            _clearSlashFrame = -1;
        }

        private static string SanitizeMessage(string value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? string.Empty
                : value.Trim().Replace("\r", string.Empty).Replace("\n", string.Empty);
        }

        private string FormatMessage(ChatPushMsg msg)
        {
            if (msg.IsSystem)
            {
                return FormatSystemMessage(msg);
            }

            var name = string.IsNullOrWhiteSpace(msg.AuthorName) ? msg.AuthorUid : msg.AuthorName;
            return $"{ColorText(EscapeRichText(name), GetPlayerColor(msg.AuthorUid, msg.AuthorName))}: {EscapeRichText(msg.Message)}";
        }

        private string FormatSystemMessage(ChatPushMsg msg)
        {
            if (msg.Message == "PlayerMissingChart")
            {
                var missing = ParsePlayerMissingChart(msg);
                if (missing.HasValue)
                {
                    return $"{SystemPrefix()} {ColorText(EscapeRichText(missing.Value.PlayerName), RedTextColor)} 未下载该谱面: {FormatMissingChartNameForDisplay(missing.Value.ChartName)}";
                }
            }

            var textMissing = ParseTextMissingChart(msg.Message);
            if (textMissing.HasValue)
            {
                var playerName = string.IsNullOrWhiteSpace(textMissing.Value.Players) ? "Unknown" : textMissing.Value.Players;
                return $"{SystemPrefix()} {ColorText(EscapeRichText(playerName), RedTextColor)} 未下载该谱面: {FormatMissingChartNameForDisplay(textMissing.Value.ChartName)}";
            }

            if ((msg.Message == "PlaylistAdd" || msg.Message == "PlaylistRemove") &&
                !string.IsNullOrWhiteSpace(msg.ExtraData))
            {
                var parts = msg.ExtraData.Split(new[] { '#' }, 2);
                if (parts.Length == 2)
                {
                    var playerName = parts[0];
                    var chartName = parts[1];
                    var action = msg.Message == "PlaylistAdd" ? "添加了" : "移除了";
                    return $"{SystemPrefix()} {ColorText(EscapeRichText(playerName), GetPlayerColorByName(playerName))} {action} {ColorText(EscapeRichText(CleanChartNameForDisplay(chartName)), Constants.ColorYellow)}";
                }
            }

            if (msg.Message == "PlayerJoinedLobby")
            {
                var player = ParsePlayerEventData(msg);
                if (player.HasValue)
                {
                    return $"{SystemPrefix()} {ColorText(EscapeRichText(player.Value.Text), GetPlayerColor(player.Value.Uid, player.Value.Text))} 加入了房间";
                }
            }

            if (msg.Message == "PlayerLeftLobby")
            {
                var player = ParsePlayerEventData(msg);
                if (player.HasValue)
                {
                    return $"{SystemPrefix()} {ColorText(EscapeRichText(player.Value.Text), GetPlayerColor(player.Value.Uid, player.Value.Text))} 离开了房间";
                }
            }

            if (msg.Message == "PlayerEntranceMessage")
            {
                var entrance = ParsePlayerEventData(msg);
                if (entrance.HasValue)
                {
                    return $"{SystemPrefix()} {ColorText(EscapeRichText(entrance.Value.Text), PinkTextColor)}";
                }
            }

            if (msg.Message == "房主已开始游戏，请准备")
            {
                return $"{SystemPrefix()} {ColorText("房主已开始游戏，请准备", GreenTextColor)}";
            }

            if (msg.Message == "所有玩家已准备，开始游戏")
            {
                return $"{SystemPrefix()} {ColorText("所有玩家已准备，开始游戏", GreenTextColor)}";
            }

            if (msg.Message == "房主已停止游戏")
            {
                return $"{SystemPrefix()} {ColorText("房主已停止游戏", RedTextColor)}";
            }

            if (!string.IsNullOrWhiteSpace(msg.Message) && msg.Message.EndsWith(" 已准备"))
            {
                var playerName = msg.Message.Substring(0, msg.Message.Length - " 已准备".Length);
                return $"{SystemPrefix()} {ColorText(EscapeRichText(playerName), GetPlayerColorByName(playerName))} {ColorText("已准备", GreenTextColor)}";
            }

            if (!string.IsNullOrWhiteSpace(msg.Message) && msg.Message.EndsWith(" 取消准备"))
            {
                var playerName = msg.Message.Substring(0, msg.Message.Length - " 取消准备".Length);
                return $"{SystemPrefix()} {ColorText(EscapeRichText(playerName), GetPlayerColorByName(playerName))} {ColorText("取消准备", RedTextColor)}";
            }

            return $"{SystemPrefix()} {EscapeRichText(msg.Message)}";
        }

        private static string GetCopyableChartName(ChatPushMsg msg)
        {
            if (msg == null || !msg.IsSystem) return null;

            if (msg.Message == "PlayerMissingChart")
            {
                var missing = ParsePlayerMissingChart(msg);
                return missing.HasValue ? CleanChartNameForCopy(missing.Value.ChartName) : null;
            }

            var textMissing = ParseTextMissingChart(msg.Message);
            if (textMissing.HasValue)
            {
                return CleanChartNameForCopy(textMissing.Value.ChartName);
            }

            return null;
        }

        private static (string PlayerName, string ChartName)? ParsePlayerMissingChart(ChatPushMsg msg)
        {
            if (string.IsNullOrWhiteSpace(msg?.ExtraData)) return null;

            var parts = msg.ExtraData.Split(new[] { '#' }, 2);
            if (parts.Length < 2) return null;

            return (parts[0], parts[1]);
        }

        private static (string Uid, string Text)? ParsePlayerEventData(ChatPushMsg msg)
        {
            if (string.IsNullOrWhiteSpace(msg?.ExtraData)) return null;

            var parts = msg.ExtraData.Split(new[] { '#' }, 2);
            if (parts.Length < 2) return null;

            return (parts[0], parts[1]);
        }

        private static (string ChartName, string Players)? ParseTextMissingChart(string message)
        {
            const string prefix = "有玩家缺少 ";
            if (string.IsNullOrWhiteSpace(message) || !message.StartsWith(prefix)) return null;

            var value = message.Substring(prefix.Length);
            var splitIndex = value.LastIndexOf(": ");
            if (splitIndex < 0) splitIndex = value.LastIndexOf('：');
            if (splitIndex < 0)
            {
                return (value.Trim(), null);
            }

            return (value.Substring(0, splitIndex).Trim(), value.Substring(splitIndex + 1).Trim());
        }

        private static string CleanChartNameForCopy(string chartName)
        {
            if (string.IsNullOrWhiteSpace(chartName)) return string.Empty;

            var value = CleanChartNameForDisplay(chartName);

            var lastSpace = value.LastIndexOf(" ");
            if (lastSpace > 0 && value.EndsWith("★"))
            {
                value = value.Substring(0, lastSpace);
            }

            return value.Trim();
        }

        private static string CleanChartNameForDisplay(string chartName)
        {
            if (string.IsNullOrWhiteSpace(chartName)) return string.Empty;

            var value = Regex.Replace(chartName, "<.*?>", string.Empty);
            value = Regex.Replace(value, @"^\s*([【\[].*?[\]】]\s*)+", string.Empty);
            return value.Trim();
        }

        private static string FormatMissingChartNameForDisplay(string chartName)
        {
            if (string.IsNullOrWhiteSpace(chartName)) return ColorText(string.Empty, Constants.ColorYellow);

            var raw = chartName.Trim();
            var visible = StripRichTextForDisplay(raw);
            var match = Regex.Match(visible, @"^\s*(?<category>[【\[].*?[\]】])\s*(?<rest>.*)$");
            if (!match.Success)
            {
                return ColorText(EscapeRichText(CleanChartNameForDisplay(raw)), Constants.ColorYellow);
            }

            var category = EscapeRichText(match.Groups["category"].Value.Trim());
            var rest = EscapeRichText(match.Groups["rest"].Value.Trim());
            var categoryColor = ExtractFirstColor(raw);
            var categoryText = string.IsNullOrEmpty(categoryColor)
                ? category
                : ColorText(category, categoryColor);

            return string.IsNullOrWhiteSpace(rest)
                ? categoryText
                : $"{categoryText}{ColorText(rest, Constants.ColorYellow)}";
        }

        private static string StripRichTextForDisplay(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return string.Empty;

            value = Regex.Replace(value, @"<\s*/?\s*color\b[^>]*>", string.Empty, RegexOptions.IgnoreCase);
            value = Regex.Replace(value, @"^\s*color\s*=\s*#?[0-9a-fA-F]{3,8}\s*", string.Empty, RegexOptions.IgnoreCase);
            return value.Trim();
        }

        private static string ExtractFirstColor(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return null;

            var match = Regex.Match(value, @"color\s*=\s*#?(?<color>[0-9a-fA-F]{3,8})", RegexOptions.IgnoreCase);
            return match.Success ? match.Groups["color"].Value : null;
        }

        private static string SystemPrefix()
        {
            return ColorText("[系统]", Constants.ColorYellow);
        }

        private string GetPlayerColorByName(string playerName)
        {
            var uid = FindPlayerUidByName(playerName);
            return GetPlayerColor(uid, playerName);
        }

        private string GetPlayerColor(string uid, string fallbackName)
        {
            if (uid == PlayerManager.CurrentUid)
            {
                var localColor = NormalizeHexColor(PlayerManager.CurrentProfile?.ChatColor);
                if (!string.IsNullOrEmpty(localColor)) return localColor;
            }

            if (!string.IsNullOrWhiteSpace(uid))
            {
                if (_playerColorCache.TryGetValue(uid, out var cachedColor))
                {
                    return cachedColor;
                }

                RequestPlayerColor(uid);
            }

            return WhiteTextColor;
        }

        private string FindPlayerUidByName(string playerName)
        {
            if (string.IsNullOrWhiteSpace(playerName)) return null;

            var lobby = LobbyManager.CurrentLobby;
            if (lobby?.PlayerDetails == null) return null;

            foreach (var player in lobby.PlayerDetails)
            {
                if (player == null) continue;
                if (player.Name == playerName || player.Uid == playerName)
                {
                    return player.Uid;
                }
            }

            return null;
        }

        private async void RequestPlayerColor(string uid)
        {
            if (string.IsNullOrWhiteSpace(uid) || _pendingColorRequests.Contains(uid)) return;
            _pendingColorRequests.Add(uid);

            var resolvedColor = WhiteTextColor;
            try
            {
                var profile = await PlayerManager.GetProfileAsync(uid);
                var color = NormalizeHexColor(profile?.ChatColor);
                resolvedColor = string.IsNullOrEmpty(color) ? WhiteTextColor : color;
            }
            catch
            {
                resolvedColor = WhiteTextColor;
            }

            MainThreadDispatcher.Enqueue(() =>
            {
                _playerColorCache[uid] = resolvedColor;
                _pendingColorRequests.Remove(uid);
                RefreshMessageTexts();
            });
        }

        private void RefreshMessageTexts()
        {
            foreach (var pair in _textList)
            {
                if (pair.Value != null)
                {
                    pair.Value.text = FormatMessage(pair.Key);
                }
            }

            UpdateLayout();
        }

        private static string NormalizeHexColor(string color)
        {
            if (string.IsNullOrWhiteSpace(color)) return null;

            color = color.Trim().TrimStart('#');
            if (color.Length == 3)
            {
                color = $"{color[0]}{color[0]}{color[1]}{color[1]}{color[2]}{color[2]}";
            }

            if (color.Length == 6) color += "ff";
            if (color.Length != 8) return null;

            for (var i = 0; i < color.Length; i++)
            {
                if (!Uri.IsHexDigit(color[i])) return null;
            }

            return color.ToLowerInvariant();
        }

        private static string ColorText(string text, string color)
        {
            var normalized = NormalizeHexColor(color) ?? WhiteTextColor;
            return $"<color=#{normalized}>{text}</color>";
        }

        private static string EscapeRichText(string value)
        {
            return value?.Replace("<", "＜").Replace(">", "＞") ?? string.Empty;
        }

        private static void ApplyInputTextPadding(InputField inputField, float rightPadding = InputRightPadding)
        {
            if (inputField == null || inputField.textComponent == null) return;

            var textAreaTransform = inputField.textComponent.transform.parent;
            if (textAreaTransform != null && textAreaTransform != inputField.transform)
            {
                if (textAreaTransform.GetComponent<RectMask2D>() == null)
                    textAreaTransform.gameObject.AddComponent<RectMask2D>();

                var taRect = textAreaTransform.GetComponent<RectTransform>();
                if (taRect != null)
                {
                    taRect.offsetMin = new Vector2(InputLeftPadding, InputVerticalPadding);
                    taRect.offsetMax = new Vector2(-rightPadding, -InputVerticalPadding);
                }
            }
            else
            {
                var txtRect = inputField.textComponent.GetComponent<RectTransform>();
                if (txtRect != null)
                {
                    txtRect.offsetMin = new Vector2(InputLeftPadding, InputVerticalPadding);
                    txtRect.offsetMax = new Vector2(-rightPadding, -InputVerticalPadding);
                }
            }
        }

        private static float GetClearButtonReservedWidth(Button clearButton)
        {
            return InputRightPadding;
        }

        private static void AddClearButtonLine(Transform parent, float zRotation)
        {
            var lineObj = new GameObject(zRotation > 0 ? "LineA" : "LineB");
            lineObj.transform.SetParent(parent, false);

            var lineRect = lineObj.AddComponent<RectTransform>();
            lineRect.anchorMin = new Vector2(0.5f, 0.5f);
            lineRect.anchorMax = new Vector2(0.5f, 0.5f);
            lineRect.pivot = new Vector2(0.5f, 0.5f);
            lineRect.sizeDelta = new Vector2(14f, 2.8f);
            lineRect.anchoredPosition = Vector2.zero;
            lineRect.localRotation = Quaternion.Euler(0f, 0f, zRotation);

            var lineImage = lineObj.AddComponent<Image>();
            lineImage.color = InputClearButtonIconColor;
        }

        private static Button CreateClearButton(GameObject inputObj)
        {
            if (inputObj == null) return null;

            var clearObj = new GameObject("MDENRoomChatClearButton");
            clearObj.transform.SetParent(inputObj.transform, false);

            var image = clearObj.AddComponent<Image>();
            image.color = InputClearButtonBgColor;
            image.type = Image.Type.Sliced;

            var inputBg = inputObj.GetComponent<Image>();
            if (inputBg != null && inputBg.sprite != null)
            {
                image.sprite = inputBg.sprite;
            }

            AddClearButtonLine(clearObj.transform, 45f);
            AddClearButtonLine(clearObj.transform, -45f);

            var clearButton = clearObj.AddComponent<Button>();
            clearButton.onClick.RemoveAllListeners();

            var clearRect = clearButton.GetComponent<RectTransform>() ?? clearButton.gameObject.AddComponent<RectTransform>();
            clearRect.SetParent(inputObj.transform, false);
            clearRect.anchorMin = new Vector2(1f, 0.5f);
            clearRect.anchorMax = new Vector2(1f, 0.5f);
            clearRect.pivot = new Vector2(1f, 0.5f);
            clearRect.sizeDelta = new Vector2(28f, 28f);
            clearRect.anchoredPosition = new Vector2(-6f, 0f);

            var layoutElement = clearButton.GetComponent<LayoutElement>() ?? clearButton.gameObject.AddComponent<LayoutElement>();
            layoutElement.ignoreLayout = true;
            layoutElement.minWidth = clearRect.sizeDelta.x;
            layoutElement.minHeight = clearRect.sizeDelta.y;
            layoutElement.preferredWidth = clearRect.sizeDelta.x;
            layoutElement.preferredHeight = clearRect.sizeDelta.y;

            clearButton.gameObject.SetActive(false);
            clearButton.transform.SetAsLastSibling();
            return clearButton;
        }

        private static void UpdateInputVisualState(InputField inputField, Button clearButton, bool hidePlaceholder)
        {
            if (inputField == null) return;

            bool hasText = !string.IsNullOrEmpty(inputField.text);

            if (inputField.placeholder != null)
                inputField.placeholder.gameObject.SetActive(!hidePlaceholder && !hasText);

            if (clearButton != null)
                clearButton.gameObject.SetActive(hasText);
        }

        private void UpdateBackgroundFocusState(bool focused)
        {
            if (_backgroundImage != null)
            {
                _backgroundImage.color = focused ? BackgroundFocusedColor : BackgroundDefaultColor;
            }
        }

        private void ApplyGameFont(Text text)
        {
            if (text == null) return;

            var template = _inputField?.textComponent ?? FindNativeFontTemplate();
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
            if (_nativeFontTemplate != null && _nativeFontTemplate.font != null)
            {
                return _nativeFontTemplate;
            }

            Text fallback = null;
            var texts = UnityEngine.Resources.FindObjectsOfTypeAll<Text>();
            if (texts != null)
            {
                for (int i = 0; i < texts.Length; i++)
                {
                    var text = texts[i];
                    if (text == null || text.font == null) continue;
                    fallback ??= text;

                    var fontName = text.font.name ?? string.Empty;
                    if (!fontName.Contains("Arial"))
                    {
                        _nativeFontTemplate = text;
                        return _nativeFontTemplate;
                    }
                }
            }

            _nativeFontTemplate = fallback;
            return _nativeFontTemplate;
        }

        private void SetGameInputBlocked(bool blocked)
        {
            if (_gameInputBlocked == blocked) return;
            _gameInputBlocked = blocked;

            try
            {
                var manager = Il2CppAssets.Scripts.PeroTools.Managers.InputManager.instance;
                if (manager != null) manager.isStopKeyAction = blocked;
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Set game input block failed: {ex.Message}");
            }
        }
    }
}
