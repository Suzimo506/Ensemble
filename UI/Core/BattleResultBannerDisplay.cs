using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Il2CppAssets.Scripts.UI.Panels;
using MDEN.Managers;
using MDEN.Protocol.Models;
using MDEN.UI.Displays;
using UnityEngine;
using UnityEngine.UI;

namespace MDEN.UI.Core
{
    internal static class BattleResultBannerDisplay
    {
        private const string RootName = "MDENBattleResultBanner";
        private const string ResultEntryName = "MDENBattleResultEntry";
        private const int OverlaySortingOrder = 32766;
        private const int NativeMessageSuppressionFrames = 600;
        private const int NativeMessageSuppressIntervalFrames = 10;
        private const int NativeMessagePanelLookupIntervalFrames = 30;
        private const float EntryWidth = 1180f;
        private const float EntryHeight = 58f;
        private const float EntrySpacing = 68f;
        private const float EntryAvatarSize = 46f;
        private const float EntryGroupMaxWidth = 980f;
        private const float EntryRankMinWidth = 64f;
        private const float EntryAvatarGap = 14f;
        private const float EntryNameGap = 12f;
        private const float EntrySlideOffset = 180f;
        private const float EntrySlideDuration = 0.42f;
        private const float ClickSoundVolumeScale = 2.4f;
        private static readonly TimeSpan CellDelay = TimeSpan.FromMilliseconds(145);
        private static PnlMessage _pnlMessage;
        private static int _nextNativeMessageDirectLookupFrame;
        private static int _nextNativeMessageSuppressFrame;
        private static int _allowNativeMessagesUntilFrame;
        private static GameObject _root;
        private static RectTransform _entryRoot;
        private static readonly List<EntryAnimation> EntryAnimations = new();
        private static BattlePlayerEntry[] _displayedPlayers = Array.Empty<BattlePlayerEntry>();
        private static BattlePlayerEntry[] _pendingRefreshPlayers;
        private static int _resultGeneration;
        private static int _suppressNativeMessagesUntilFrame;
        private static bool _enterWasDown;
        private static int _ignoreMouseInputUntilFrame;

        public static bool ShowingResults { get; private set; }
        public static bool IsVisible => _root != null;
        public static bool IsActive => IsVisible || ShowingResults;
        private static bool IsSuppressingNativeMessages => IsVisible || Time.frameCount <= _suppressNativeMessagesUntilFrame;

        public static void RefreshIfVisible(BattlePlayerEntry[] players)
        {
            if (!IsActive) return;
            if (ShowingResults)
            {
                _pendingRefreshPlayers = CloneBattleEntries(players);
                return;
            }

            if (!HasResultChanged(players)) return;

            QueueRefreshWithoutAnimation(players);
        }

        public static void ShowOrRefresh(BattlePlayerEntry[] players)
        {
            if (IsActive)
            {
                RefreshIfVisible(players);
                return;
            }

            _ = ShowAsync(players);
        }

        public static void Update()
        {
            HandleOverlayInput();
            UpdateEntryAnimations();
            if (IsSuppressingNativeMessages && Time.frameCount >= _nextNativeMessageSuppressFrame)
            {
                _nextNativeMessageSuppressFrame = Time.frameCount + NativeMessageSuppressIntervalFrames;
                SuppressNativeMessagesNow();
            }
        }

        public static async Task ShowAsync(BattlePlayerEntry[] players)
        {
            if (!LobbyManager.IsInLobby) return;

            var generation = 0;
            try
            {
                var orderedPlayers = BattleLobbyDisplay
                    .OrderPlayers(players ?? Array.Empty<BattlePlayerEntry>())
                    .ToArray();
                if (orderedPlayers.Length == 0)
                {
                    MDEN.Managers.ClientLogManager.Warning("Battle result skipped: no player snapshot.");
                    return;
                }

                generation = ++_resultGeneration;
                ShowingResults = true;
                await MainThreadDispatcher.InvokeAsync(() =>
                {
                    if (generation != _resultGeneration) return;
                    DestroyRoot();
                    CreateRoot();
                    UpdateEntryAnimations();
                    StartNativeMessageSuppression();
                });

                EntryLayout layout = null;
                await MainThreadDispatcher.InvokeAsync(() =>
                {
                    if (generation == _resultGeneration)
                    {
                        layout = MeasureEntryLayout(orderedPlayers);
                    }
                });
                if (layout == null) return;

                for (var i = 0; i < orderedPlayers.Length; i++)
                {
                    var player = orderedPlayers[i];
                    await AddOneAsync(player, i + 1, i, orderedPlayers.Length, generation, layout);
                }

                if (generation == _resultGeneration)
                {
                    _displayedPlayers = orderedPlayers.Select(CloneBattleEntry).ToArray();
                    var pending = _pendingRefreshPlayers;
                    _pendingRefreshPlayers = null;
                    if (pending != null && HasResultChanged(pending))
                    {
                        QueueRefreshWithoutAnimation(pending);
                    }
                }
            }
            catch (Exception ex)
            {
                MDEN.Managers.ClientLogManager.Warning($"Battle result display failed: {ex.Message}");
                if (generation == 0 || generation == _resultGeneration)
                {
                    ClearAll();
                }
            }
            finally
            {
                if (generation == 0 || generation == _resultGeneration)
                {
                    ShowingResults = false;
                    ReleaseBattleResultInputBlock();
                }
            }
        }

        public static void CloseWithSound()
        {
            if (!IsActive) return;

            UiSoundManager.Play(UiSound.Yes, ClickSoundVolumeScale);
            ClearAll();
        }

        public static void ClearAll()
        {
            _resultGeneration++;
            _suppressNativeMessagesUntilFrame = 0;
            _displayedPlayers = Array.Empty<BattlePlayerEntry>();
            _pendingRefreshPlayers = null;
            _nextNativeMessageDirectLookupFrame = 0;
            _nextNativeMessageSuppressFrame = 0;
            ShowingResults = false;
            _enterWasDown = false;
            _ignoreMouseInputUntilFrame = 0;
            _allowNativeMessagesUntilFrame = 0;
            ReleaseBattleResultInputBlock();
            MainThreadDispatcher.Enqueue(ClearAllOnMainThread);
        }

        public static void AllowNativeMessagesBriefly()
        {
            _allowNativeMessagesUntilFrame = Math.Max(_allowNativeMessagesUntilFrame, Time.frameCount + 90);
        }

        public static void SuppressNativeMessages()
        {
            MainThreadDispatcher.Enqueue(StartNativeMessageSuppression);
        }

        internal static bool ShouldSuppressNativeMessageObject(GameObject obj)
        {
            return IsSuppressingNativeMessages &&
                   Time.frameCount > _allowNativeMessagesUntilFrame &&
                   obj != null &&
                   obj.name == "PnlMessage" &&
                   obj.GetComponent<PnlMessage>() != null;
        }

        private static void ClearAllOnMainThread()
        {
            DestroyRoot();
            ClearNativeEntries();
            ReleaseBattleResultInputBlock();
        }

        private static async Task AddOneAsync(BattlePlayerEntry player, int rank, int index, int count, int generation, EntryLayout layout)
        {
            await MainThreadDispatcher.InvokeAsync(() =>
            {
                if (EnsureRootReady(generation))
                {
                    AddEntry(player, rank, index, count, generation, layout);
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
            _ignoreMouseInputUntilFrame = Time.frameCount + 1;
            ReleaseBattleResultInputBlock();
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
            image.raycastTarget = false;
        }

        private static void AddEntry(
            BattlePlayerEntry player,
            int rank,
            int index,
            int count,
            int generation,
            EntryLayout layout,
            bool animate = true)
        {
            if (generation != _resultGeneration) return;
            if (_root == null || _entryRoot == null || layout == null) return;

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

            var groupRoot = new GameObject("Content");
            groupRoot.transform.SetParent(obj.transform, false);
            var groupRect = groupRoot.AddComponent<RectTransform>();
            groupRect.anchorMin = new Vector2(0.5f, 0.5f);
            groupRect.anchorMax = new Vector2(0.5f, 0.5f);
            groupRect.pivot = new Vector2(0f, 0.5f);
            groupRect.anchoredPosition = new Vector2(-layout.ContentWidth * 0.5f, 0f);
            groupRect.sizeDelta = new Vector2(layout.ContentWidth, EntryHeight);

            var rankLabel = new GameObject("Rank");
            rankLabel.transform.SetParent(groupRoot.transform, false);
            var rankRect = rankLabel.AddComponent<RectTransform>();
            rankRect.anchorMin = new Vector2(0f, 0.5f);
            rankRect.anchorMax = new Vector2(0f, 0.5f);
            rankRect.pivot = new Vector2(0f, 0.5f);
            rankRect.anchoredPosition = Vector2.zero;
            rankRect.sizeDelta = new Vector2(layout.RankWidth, EntryHeight);

            var rankText = rankLabel.AddComponent<Text>();
            ApplyGameFont(rankText);
            rankText.text = BattleLobbyDisplay.FormatResultRank(player, rank);
            rankText.fontSize = 30;
            rankText.alignment = TextAnchor.MiddleRight;
            rankText.horizontalOverflow = HorizontalWrapMode.Overflow;
            rankText.verticalOverflow = VerticalWrapMode.Overflow;
            rankText.supportRichText = true;
            rankText.raycastTarget = false;
            rankText.color = Color.white;

            var avatar = new GameObject("Avatar");
            avatar.transform.SetParent(groupRoot.transform, false);
            var avatarRect = avatar.AddComponent<RectTransform>();
            avatarRect.anchorMin = new Vector2(0f, 0.5f);
            avatarRect.anchorMax = new Vector2(0f, 0.5f);
            avatarRect.pivot = new Vector2(0f, 0.5f);
            avatarRect.anchoredPosition = new Vector2(layout.AvatarX, 0f);
            avatarRect.sizeDelta = new Vector2(EntryAvatarSize, EntryAvatarSize);

            var avatarImage = avatar.AddComponent<Image>();
            avatarImage.sprite = GetPlayerAvatarSprite(player?.Uid);
            avatarImage.preserveAspect = true;
            avatarImage.raycastTarget = false;

            var label = new GameObject("Details");
            label.transform.SetParent(groupRoot.transform, false);
            var labelRect = label.AddComponent<RectTransform>();
            labelRect.anchorMin = new Vector2(0f, 0.5f);
            labelRect.anchorMax = new Vector2(0f, 0.5f);
            labelRect.pivot = new Vector2(0f, 0.5f);
            labelRect.anchoredPosition = new Vector2(layout.LabelX, 0f);
            labelRect.sizeDelta = new Vector2(layout.LabelWidth, EntryHeight);

            var labelText = label.AddComponent<Text>();
            ApplyGameFont(labelText);
            labelText.text = BattleLobbyDisplay.FormatResultNameAndInfo(player);
            labelText.fontSize = 30;
            labelText.alignment = TextAnchor.MiddleLeft;
            labelText.horizontalOverflow = HorizontalWrapMode.Overflow;
            labelText.verticalOverflow = VerticalWrapMode.Overflow;
            labelText.supportRichText = true;
            labelText.raycastTarget = false;
            labelText.color = Color.white;

            if (animate)
            {
                EntryAnimations.Add(new EntryAnimation(rect, group, targetPosition, generation));
                UpdateEntryAnimations();
            }
            else
            {
                rect.anchoredPosition = targetPosition;
                group.alpha = 1f;
            }
        }

        private static EntryLayout MeasureEntryLayout(BattlePlayerEntry[] orderedPlayers)
        {
            if (_entryRoot == null) return null;

            var measureObj = new GameObject("MeasureText");
            measureObj.transform.SetParent(_entryRoot, false);
            var measureText = measureObj.AddComponent<Text>();
            ApplyGameFont(measureText);
            measureText.fontSize = 30;
            measureText.alignment = TextAnchor.MiddleLeft;
            measureText.horizontalOverflow = HorizontalWrapMode.Overflow;
            measureText.verticalOverflow = VerticalWrapMode.Overflow;
            measureText.supportRichText = true;
            measureText.raycastTarget = false;
            measureText.color = new Color(1f, 1f, 1f, 0f);

            var rankWidth = EntryRankMinWidth;
            var labelWidth = 0f;
            try
            {
                for (var i = 0; i < orderedPlayers.Length; i++)
                {
                    var player = orderedPlayers[i];
                    measureText.text = BattleLobbyDisplay.FormatResultRank(player, i + 1);
                    Canvas.ForceUpdateCanvases();
                    rankWidth = Mathf.Max(rankWidth, measureText.preferredWidth);

                    measureText.text = BattleLobbyDisplay.FormatResultNameAndInfo(player);
                    Canvas.ForceUpdateCanvases();
                    labelWidth = Mathf.Max(labelWidth, measureText.preferredWidth);
                }
            }
            finally
            {
                UnityEngine.Object.Destroy(measureObj);
            }

            var avatarX = rankWidth + EntryAvatarGap;
            var labelX = avatarX + EntryAvatarSize + EntryNameGap;
            labelWidth = Mathf.Min(EntryGroupMaxWidth - labelX, labelWidth);
            labelWidth = Mathf.Max(0f, labelWidth);

            return new EntryLayout(rankWidth, avatarX, labelX, labelWidth);
        }

        private static void DestroyRoot()
        {
            EntryAnimations.Clear();
            if (_root == null) return;
            UnityEngine.Object.Destroy(_root);
            _root = null;
            _entryRoot = null;
        }

        private static void QueueRefreshWithoutAnimation(BattlePlayerEntry[] players)
        {
            var snapshot = CloneBattleEntries(players);
            MainThreadDispatcher.Enqueue(() => UpdateEntriesWithoutAnimation(snapshot));
        }

        private static void UpdateEntriesWithoutAnimation(BattlePlayerEntry[] players)
        {
            var orderedPlayers = BattleLobbyDisplay
                .OrderPlayers(players ?? Array.Empty<BattlePlayerEntry>())
                .ToArray();
            if (orderedPlayers.Length == 0 || _root == null || _entryRoot == null) return;

            EntryAnimations.Clear();
            for (var i = _entryRoot.childCount - 1; i >= 0; i--)
            {
                var child = _entryRoot.GetChild(i);
                if (child != null)
                {
                    child.gameObject.SetActive(false);
                    UnityEngine.Object.Destroy(child.gameObject);
                }
            }

            var generation = _resultGeneration;
            var layout = MeasureEntryLayout(orderedPlayers);
            if (layout == null) return;

            for (var i = 0; i < orderedPlayers.Length; i++)
            {
                AddEntry(
                    orderedPlayers[i],
                    i + 1,
                    i,
                    orderedPlayers.Length,
                    generation,
                    layout,
                    false);
            }

            EntryAnimations.Clear();
            _displayedPlayers = orderedPlayers.Select(CloneBattleEntry).ToArray();
        }

        private static bool HasResultChanged(BattlePlayerEntry[] players)
        {
            var orderedPlayers = BattleLobbyDisplay
                .OrderPlayers(players ?? Array.Empty<BattlePlayerEntry>())
                .ToArray();
            if (orderedPlayers.Length == 0) return false;
            if (orderedPlayers.Length != _displayedPlayers.Length) return true;

            for (var i = 0; i < orderedPlayers.Length; i++)
            {
                if (!SameBattleEntry(orderedPlayers[i], _displayedPlayers[i])) return true;
            }

            return false;
        }

        private static bool SameBattleEntry(BattlePlayerEntry left, BattlePlayerEntry right)
        {
            if (left == null || right == null) return left == right;

            return left.Uid == right.Uid &&
                   left.Difficulty == right.Difficulty &&
                   left.Score == right.Score &&
                   Math.Abs(left.Accuracy - right.Accuracy) < 0.0001f &&
                   left.Perfects == right.Perfects &&
                   left.Greats == right.Greats &&
                   left.Earlies == right.Earlies &&
                   left.Lates == right.Lates &&
                   left.Misses == right.Misses &&
                   left.FC == right.FC &&
                   left.Alive == right.Alive;
        }

        private static BattlePlayerEntry CloneBattleEntry(BattlePlayerEntry entry)
        {
            if (entry == null) return null;

            return new BattlePlayerEntry
            {
                Uid = entry.Uid,
                Difficulty = entry.Difficulty,
                Score = entry.Score,
                Accuracy = entry.Accuracy,
                Perfects = entry.Perfects,
                Greats = entry.Greats,
                Earlies = entry.Earlies,
                Lates = entry.Lates,
                Misses = entry.Misses,
                FC = entry.FC,
                Alive = entry.Alive,
                PingMS = entry.PingMS
            };
        }

        private static BattlePlayerEntry[] CloneBattleEntries(BattlePlayerEntry[] players)
        {
            return (players ?? Array.Empty<BattlePlayerEntry>())
                .Select(CloneBattleEntry)
                .ToArray();
        }

        private static void StartNativeMessageSuppression()
        {
            _suppressNativeMessagesUntilFrame = Math.Max(
                _suppressNativeMessagesUntilFrame,
                Time.frameCount + NativeMessageSuppressionFrames);
            _nextNativeMessageSuppressFrame = Time.frameCount + NativeMessageSuppressIntervalFrames;
            SuppressNativeMessagesNow();
        }

        private static void SuppressNativeMessagesNow()
        {
            if (Time.frameCount <= _allowNativeMessagesUntilFrame) return;

            SuppressNativeMessagePanel(GetPnlMessage());
        }

        private static bool SuppressNativeMessagePanel(PnlMessage pnlMessage)
        {
            if (!IsScenePnlMessage(pnlMessage)) return false;

            if (pnlMessage.layout != null && pnlMessage.layout.childCount > 0)
            {
                ClearNativeEntries(pnlMessage);
            }

            if (pnlMessage.gameObject.activeSelf)
            {
                pnlMessage.gameObject.SetActive(false);
            }

            return true;
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

        private static Sprite GetPlayerAvatarSprite(string uid)
        {
            var details = LobbyManager.CurrentLobby?.PlayerDetails;
            if (!string.IsNullOrWhiteSpace(uid) && details != null)
            {
                foreach (var player in details)
                {
                    if (player?.Uid != uid) continue;
                    return AvatarManager.GetAvatarSprite(uid, player.AvatarName, player.AvatarData);
                }
            }

            if (uid == PlayerManager.CurrentUid)
            {
                return AvatarManager.GetCurrentAvatarSprite();
            }

            return AvatarManager.GetAvatarSprite(uid, null, null);
        }

        private static bool EnsureRootReady(int generation)
        {
            if (generation != _resultGeneration) return false;
            if (_root != null && _entryRoot != null) return true;

            DestroyRoot();
            CreateRoot();
            ReleaseBattleResultInputBlock();
            StartNativeMessageSuppression();
            return _root != null && _entryRoot != null;
        }

        private static void ReleaseBattleResultInputBlock()
        {
            NativeInputBlocker.ClearAndForceUnblockIfIdle("BattleResult");
        }

        private static void HandleOverlayInput()
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
            if (IsVisible)
            {
                HandleBlankAreaClick();
            }
        }

        private static void HandleBlankAreaClick()
        {
            if (Time.frameCount <= _ignoreMouseInputUntilFrame) return;
            if (!Input.GetMouseButtonDown(0)) return;
            if (IsPointerOverResultEntry(Input.mousePosition)) return;

            CloseWithSound();
        }

        private static bool IsPointerOverResultEntry(Vector2 screenPosition)
        {
            if (_entryRoot == null) return false;

            for (var i = 0; i < _entryRoot.childCount; i++)
            {
                var child = _entryRoot.GetChild(i);
                if (child == null || !child.gameObject.activeInHierarchy) continue;

                var rect = child.GetComponent<RectTransform>();
                if (rect != null && RectTransformUtility.RectangleContainsScreenPoint(rect, screenPosition, null))
                {
                    return true;
                }
            }

            return false;
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

        private sealed class EntryLayout
        {
            public EntryLayout(float rankWidth, float avatarX, float labelX, float labelWidth)
            {
                RankWidth = rankWidth;
                AvatarX = avatarX;
                LabelX = labelX;
                LabelWidth = labelWidth;
                ContentWidth = labelX + labelWidth;
            }

            public float RankWidth { get; }
            public float AvatarX { get; }
            public float LabelX { get; }
            public float LabelWidth { get; }
            public float ContentWidth { get; }
        }
    }
}
