using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Il2CppAssets.Scripts.UI.Panels;
using MDEN.Managers;
using MDEN.Protocol.Models;
using MDEN.UI.Displays;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace MDEN.UI.Core
{
    internal static class BattleResultBannerDisplay
    {
        private const string RootName = "MDENBattleResultBanner";
        private const string ResultEntryName = "MDENBattleResultEntry";
        private const int OverlaySortingOrder = 32766;
        private const int NativeMessageSuppressionFrames = 600;
        private const int NativeMessagePanelLookupIntervalFrames = 30;
        private const float EntryWidth = 1180f;
        private const float EntryHeight = 58f;
        private const float EntrySpacing = 68f;
        private const float EntrySlideOffset = 180f;
        private const float EntrySlideDuration = 0.42f;
        private const float ClickSoundVolumeScale = 2.4f;
        private static readonly TimeSpan CellDelay = TimeSpan.FromMilliseconds(145);
        private static PnlMessage _pnlMessage;
        private static PnlMessage[] _nativeMessagePanels = Array.Empty<PnlMessage>();
        private static int _nextNativeMessageDirectLookupFrame;
        private static int _nextNativeMessagePanelLookupFrame;
        private static GameObject _root;
        private static RectTransform _entryRoot;
        private static readonly List<EntryAnimation> EntryAnimations = new();
        private static int _resultGeneration;
        private static int _suppressNativeMessagesUntilFrame;
        private static bool _keyboardBlocked;
        private static bool _enterWasDown;

        public static bool ShowingResults { get; private set; }
        public static bool IsVisible => _root != null;
        public static bool IsConsumingKeyboard => IsVisible || ShowingResults;
        private static bool IsSuppressingNativeMessages => IsVisible || Time.frameCount <= _suppressNativeMessagesUntilFrame;

        public static void Update()
        {
            UpdateKeyboardBlock();
            HandleEnterKeyEdge();
            UpdateEntryAnimations();
            if (IsSuppressingNativeMessages)
            {
                SuppressNativeMessagesNow();
            }
        }

        public static async Task ShowAsync(BattlePlayerEntry[] players)
        {
            if (!LobbyManager.IsInLobby) return;

            var orderedPlayers = BattleLobbyDisplay
                .OrderPlayers(players ?? Array.Empty<BattlePlayerEntry>())
                .ToArray();
            if (orderedPlayers.Length == 0)
            {
                MDEN.Managers.ClientLogManager.Warning("Battle result skipped: no player snapshot.");
                return;
            }

            var generation = ++_resultGeneration;
            ShowingResults = true;
            MainThreadDispatcher.Enqueue(() =>
            {
                if (generation != _resultGeneration) return;
                DestroyRoot();
                CreateRoot();
                UpdateEntryAnimations();
                StartNativeMessageSuppression();
            });

            try
            {
                for (var i = 0; i < orderedPlayers.Length; i++)
                {
                    var player = orderedPlayers[i];
                    var text = BattleLobbyDisplay.FormatResultEntry(player, i + 1);
                    await AddOneAsync(text, i, orderedPlayers.Length, generation);
                }
            }
            finally
            {
                if (generation == _resultGeneration)
                {
                    ShowingResults = false;
                }
            }
        }

        public static void CloseWithSound()
        {
            if (!IsVisible) return;

            UiSoundManager.Play(UiSound.Yes, ClickSoundVolumeScale);
            ClearAll();
        }

        public static bool ShouldConsumeKeyDown(KeyCode key)
        {
            if (!IsConsumingKeyboard) return false;
            return key == KeyCode.R || key == KeyCode.Return || key == KeyCode.KeypadEnter;
        }

        public static void HandleConsumedKeyDown(KeyCode key)
        {
            if (key == KeyCode.Return || key == KeyCode.KeypadEnter)
            {
                CloseWithSound();
            }
        }

        public static void ClearAll()
        {
            _resultGeneration++;
            _suppressNativeMessagesUntilFrame = 0;
            _nativeMessagePanels = Array.Empty<PnlMessage>();
            _nextNativeMessageDirectLookupFrame = 0;
            _nextNativeMessagePanelLookupFrame = 0;
            ShowingResults = false;
            _enterWasDown = false;
            SetKeyboardBlocked(false);
            MainThreadDispatcher.Enqueue(ClearAllOnMainThread);
        }

        public static void SuppressNativeMessages()
        {
            MainThreadDispatcher.Enqueue(StartNativeMessageSuppression);
        }

        internal static bool ShouldSuppressNativeMessageObject(GameObject obj)
        {
            return IsSuppressingNativeMessages &&
                   obj != null &&
                   obj.name == "PnlMessage" &&
                   obj.GetComponent<PnlMessage>() != null;
        }

        private static void ClearAllOnMainThread()
        {
            DestroyRoot();
            ClearNativeEntries();
            UpdateKeyboardBlock();
        }

        private static async Task AddOneAsync(string text, int index, int count, int generation)
        {
            MainThreadDispatcher.Enqueue(() =>
            {
                if (EnsureRootReady(generation))
                {
                    AddEntry(text, index, count, generation);
                }
            });
            await Task.Delay(CellDelay);
        }

        private static void CreateRoot()
        {
            _root = new GameObject(RootName);
            var rect = _root.AddComponent<RectTransform>();
            rect.localScale = Vector3.one;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition3D = Vector3.zero;
            rect.sizeDelta = Vector2.zero;

            var canvas = _root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.overrideSorting = true;
            canvas.sortingOrder = OverlaySortingOrder;
            _root.AddComponent<GraphicRaycaster>();

            var scaler = _root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            CreateShade(rect);

            var entries = new GameObject("Entries");
            entries.transform.SetParent(_root.transform, false);
            _entryRoot = entries.AddComponent<RectTransform>();
            _entryRoot.anchorMin = new Vector2(0.5f, 0.5f);
            _entryRoot.anchorMax = new Vector2(0.5f, 0.5f);
            _entryRoot.pivot = new Vector2(0.5f, 0.5f);
            _entryRoot.anchoredPosition = new Vector2(0f, 18f);
            _entryRoot.sizeDelta = new Vector2(EntryWidth, 720f);
            _enterWasDown = IsEnterKeyDown();
        }

        private static void CreateShade(RectTransform rootRect)
        {
            var shade = new GameObject("Shade");
            shade.transform.SetParent(rootRect, false);

            var rect = shade.AddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var image = shade.AddComponent<Image>();
            image.color = new Color(0f, 0f, 0f, 0.56f);
            image.raycastTarget = true;

            var button = shade.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.onClick.AddListener((UnityAction)CloseWithSound);
        }

        private static void AddEntry(string text, int index, int count, int generation)
        {
            if (generation != _resultGeneration) return;
            if (_root == null || _entryRoot == null) return;

            var obj = new GameObject($"{ResultEntryName}_{index + 1}");
            obj.transform.SetParent(_entryRoot, false);

            var rect = obj.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(EntryWidth, EntryHeight);
            var targetPosition = new Vector2(0f, ((count - 1) * EntrySpacing * 0.5f) - index * EntrySpacing);
            rect.anchoredPosition = targetPosition + new Vector2(0f, EntrySlideOffset);

            var group = obj.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = false;

            var image = obj.AddComponent<Image>();
            image.color = new Color(0.24f, 0.02f, 0.32f, 0.88f);
            image.raycastTarget = false;

            var outline = obj.AddComponent<Outline>();
            outline.effectColor = new Color(1f, 0.34f, 0.92f, 0.28f);
            outline.effectDistance = new Vector2(0f, -2f);

            var label = new GameObject("Text");
            label.transform.SetParent(obj.transform, false);
            var labelRect = label.AddComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = new Vector2(28f, 0f);
            labelRect.offsetMax = new Vector2(-28f, 0f);

            var labelText = label.AddComponent<Text>();
            ApplyGameFont(labelText);
            labelText.text = text;
            labelText.fontSize = 30;
            labelText.alignment = TextAnchor.MiddleCenter;
            labelText.horizontalOverflow = HorizontalWrapMode.Overflow;
            labelText.verticalOverflow = VerticalWrapMode.Overflow;
            labelText.supportRichText = true;
            labelText.raycastTarget = false;
            labelText.color = Color.white;

            EntryAnimations.Add(new EntryAnimation(rect, group, targetPosition, generation));
            UpdateEntryAnimations();
        }

        private static void DestroyRoot()
        {
            EntryAnimations.Clear();
            if (_root == null) return;
            UnityEngine.Object.Destroy(_root);
            _root = null;
            _entryRoot = null;
        }

        private static void StartNativeMessageSuppression()
        {
            _suppressNativeMessagesUntilFrame = Math.Max(
                _suppressNativeMessagesUntilFrame,
                Time.frameCount + NativeMessageSuppressionFrames);
            SuppressNativeMessagesNow();
        }

        private static void SuppressNativeMessagesNow()
        {
            SuppressNativeMessagePanel(GetPnlMessage());

            foreach (var pnlMessage in FindNativeMessagePanelsFallback())
            {
                if (pnlMessage == _pnlMessage) continue;
                SuppressNativeMessagePanel(pnlMessage);
            }
        }

        private static bool SuppressNativeMessagePanel(PnlMessage pnlMessage)
        {
            if (!IsScenePnlMessage(pnlMessage)) return false;

            ClearNativeEntries(pnlMessage);
            if (pnlMessage.gameObject.activeSelf)
            {
                pnlMessage.gameObject.SetActive(false);
            }

            return true;
        }

        private static PnlMessage[] FindNativeMessagePanelsFallback()
        {
            if (_nativeMessagePanels.Length > 0)
            {
                var validCount = 0;
                for (var i = 0; i < _nativeMessagePanels.Length; i++)
                {
                    if (IsScenePnlMessage(_nativeMessagePanels[i]))
                    {
                        validCount++;
                    }
                }

                if (validCount == _nativeMessagePanels.Length) return _nativeMessagePanels;
                if (validCount > 0)
                {
                    var validPanels = new PnlMessage[validCount];
                    var index = 0;
                    for (var i = 0; i < _nativeMessagePanels.Length; i++)
                    {
                        var panel = _nativeMessagePanels[i];
                        if (IsScenePnlMessage(panel))
                        {
                            validPanels[index++] = panel;
                        }
                    }

                    _nativeMessagePanels = validPanels;
                    return _nativeMessagePanels;
                }

                _nativeMessagePanels = Array.Empty<PnlMessage>();
            }

            if (Time.frameCount < _nextNativeMessagePanelLookupFrame)
            {
                return Array.Empty<PnlMessage>();
            }

            _nextNativeMessagePanelLookupFrame = Time.frameCount + NativeMessagePanelLookupIntervalFrames;
            using (PerfTrace.Measure("MDEN.BattleResult.FindNativeMessagePanels"))
            {
                var panels = Resources.FindObjectsOfTypeAll<PnlMessage>();
                if (panels == null || panels.Length == 0)
                {
                    return Array.Empty<PnlMessage>();
                }

                var validPanels = new List<PnlMessage>();
                foreach (var panel in panels)
                {
                    if (IsScenePnlMessage(panel))
                    {
                        validPanels.Add(panel);
                    }
                }

                _nativeMessagePanels = validPanels.Count == 0
                    ? Array.Empty<PnlMessage>()
                    : validPanels.ToArray();
                return _nativeMessagePanels;
            }
        }

        private static bool IsScenePnlMessage(PnlMessage pnlMessage)
        {
            return pnlMessage != null &&
                   pnlMessage.gameObject != null &&
                   pnlMessage.gameObject.name == "PnlMessage" &&
                   pnlMessage.gameObject.scene.IsValid();
        }

        private static void ClearNativeEntries()
        {
            var pnlMessage = GetPnlMessage();
            ClearNativeEntries(pnlMessage);
        }

        private static void ClearNativeEntries(PnlMessage pnlMessage)
        {
            if (pnlMessage == null || pnlMessage.layout == null) return;

            for (var i = pnlMessage.layout.childCount - 1; i >= 0; i--)
            {
                var child = pnlMessage.layout.GetChild(i);
                if (child != null)
                {
                    UnityEngine.Object.Destroy(child.gameObject);
                }
            }
        }

        private static PnlMessage GetPnlMessage()
        {
            if (IsScenePnlMessage(_pnlMessage)) return _pnlMessage;
            _pnlMessage = null;

            if (Time.frameCount < _nextNativeMessageDirectLookupFrame) return null;
            _nextNativeMessageDirectLookupFrame = Time.frameCount + NativeMessagePanelLookupIntervalFrames;

            using (PerfTrace.Measure("MDEN.BattleResult.FindNativeMessagePanel"))
            {
                var obj = GameObject.Find("CommonManagers/MessagesManager/UI/PnlMessage");
                if (obj == null) return null;

                _pnlMessage = obj.GetComponent<PnlMessage>();
                return _pnlMessage;
            }
        }

        private static void ApplyGameFont(Text text)
        {
            NativeFontCache.ApplyTo(text);
        }

        private static bool EnsureRootReady(int generation)
        {
            if (generation != _resultGeneration) return false;
            if (_root != null && _entryRoot != null) return true;

            DestroyRoot();
            CreateRoot();
            UpdateKeyboardBlock();
            StartNativeMessageSuppression();
            return _root != null && _entryRoot != null;
        }

        private static void UpdateKeyboardBlock()
        {
            var shouldBlock = IsConsumingKeyboard;
            if (_keyboardBlocked == shouldBlock) return;

            SetKeyboardBlocked(shouldBlock);
        }

        private static void SetKeyboardBlocked(bool blocked)
        {
            if (_keyboardBlocked == blocked)
            {
                if (!blocked)
                {
                    NativeInputBlocker.Clear("BattleResult");
                }

                return;
            }

            _keyboardBlocked = blocked;
            NativeInputBlocker.SetBlocked("BattleResult", blocked);
        }

        private static void HandleEnterKeyEdge()
        {
            if (!IsVisible)
            {
                _enterWasDown = false;
                return;
            }

            var enterDown = IsEnterKeyDown();
            if (enterDown && !_enterWasDown)
            {
                CloseWithSound();
            }

            _enterWasDown = enterDown;
        }

        private static bool IsEnterKeyDown()
        {
            return Input.GetKey(KeyCode.Return) || Input.GetKey(KeyCode.KeypadEnter);
        }

        private static void UpdateEntryAnimations()
        {
            if (EntryAnimations.Count == 0) return;

            for (var i = EntryAnimations.Count - 1; i >= 0; i--)
            {
                var animation = EntryAnimations[i];
                if (animation.Generation != _resultGeneration || animation.Rect == null || animation.Group == null)
                {
                    EntryAnimations.RemoveAt(i);
                    continue;
                }

                animation.Elapsed += Time.unscaledDeltaTime;
                var t = Mathf.Clamp01(animation.Elapsed / EntrySlideDuration);
                var eased = 1f - Mathf.Pow(1f - t, 3f);
                animation.Rect.anchoredPosition = Vector2.LerpUnclamped(animation.StartPosition, animation.TargetPosition, eased);
                animation.Group.alpha = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / 0.78f));

                if (t < 1f)
                {
                    EntryAnimations[i] = animation;
                    continue;
                }

                animation.Rect.anchoredPosition = animation.TargetPosition;
                animation.Group.alpha = 1f;
                EntryAnimations.RemoveAt(i);
            }
        }

        private struct EntryAnimation
        {
            public EntryAnimation(RectTransform rect, CanvasGroup group, Vector2 targetPosition, int generation)
            {
                Rect = rect;
                Group = group;
                TargetPosition = targetPosition;
                StartPosition = targetPosition + new Vector2(0f, EntrySlideOffset);
                Generation = generation;
                Elapsed = 0f;
            }

            public readonly RectTransform Rect;
            public readonly CanvasGroup Group;
            public readonly Vector2 TargetPosition;
            public readonly Vector2 StartPosition;
            public readonly int Generation;
            public float Elapsed;
        }
    }
}
