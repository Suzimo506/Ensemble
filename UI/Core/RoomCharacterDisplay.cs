using System;
using System.Collections.Generic;
using System.Linq;
using Il2CppAssets.Scripts.Database;
using Il2CppAssets.Scripts.PeroTools.Commons;
using Il2CppAssets.Scripts.PeroTools.Managers;
using Il2CppAssets.Scripts.UI;
using Il2CppAssets.Scripts.UI.Controls;
using Il2CppPeroTools2.Resources;
using MDEN.Managers;
using MDEN.Protocol.Enums;
using MDEN.Protocol.Messages.Lobby;
using MDEN.Protocol.Models;
using MDEN.UI.Windows;
using MelonLoader;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace MDEN.UI.Core
{
    public static class RoomCharacterDisplay
    {
        private const int MaxOtherSlots = 4;
        private const int OtherSlotsPerPage = 2;
        private const int PirateRinGirlIndex = 32;
        private const float CenterX = 1.3f;
        private const float LocalCharacterXOffset = 0.25f;
        private const float LocalY = -0.9f;
        private const float OtherY = -1.65f;
        private const float Z = 100f;
        private const float LocalScale = 0.75f;
        private const float OtherScale = 0.6f;
        private const float LabelXOffset = -1.55f;
        private const float LocalLabelXOffset = LabelXOffset - LocalCharacterXOffset;
        private const float LocalTitleYOffset = 3.65f;
        private const float LocalNameYOffset = 3.32f;
        private const float OtherTitleYOffset = 3.20f;
        private const float OtherNameYOffset = 2.91f;
        private const float LabelZOffset = -0.5f;
        private const float PageButtonScreenEdgeOffset = 88f;
        private const float PageButtonScreenYRatio = 0.5f;
        private static readonly Vector2 LocalLabelSize = new Vector2(250f, 125f);
        private static readonly Vector2 OtherLabelSize = new Vector2(250f, 125f);
        private static readonly Vector2 PageButtonSize = new Vector2(96f, 96f);
        private static readonly Color PageButtonNormalColor = new Color(1f, 0.18f, 0.78f, 1f);
        private static readonly Color PageButtonHighlightedColor = new Color(1f, 0.62f, 0.96f, 1f);
        private static readonly Color PageButtonPressedColor = new Color(0.84f, 0.08f, 1f, 1f);
        private static readonly Color PageButtonDisabledColor = new Color(0.62f, 0.42f, 0.58f, 0.72f);
        private static readonly float[] OtherOffsets = { 5.7f, 8.4f };
        private static readonly string[] OwnedObjectNames =
        {
            "MDENRoomCharacterLabels",
            "MDENRoomCharacterOther0",
            "MDENRoomCharacterOther1",
            "MDENRoomCharacterOther2",
            "MDENRoomCharacterOther3",
            "MDENRoomCharacterRightA",
            "MDENRoomCharacterRightB",
            "MDENRoomCharacterPrev",
            "MDENRoomCharacterNext",
            "MDENRoomLocalTitle",
            "MDENRoomLocalName",
            "MDENRoomOther0Title",
            "MDENRoomOther0Name",
            "MDENRoomOther1Title",
            "MDENRoomOther1Name",
            "MDENRoomOther2Title",
            "MDENRoomOther2Name",
            "MDENRoomOther3Title",
            "MDENRoomOther3Name"
        };

        private static GameObject _nativeMuseShow;
        private static GameObject _nativeElfinShow;
        private static readonly OtherCharacterSlot[] OtherSlots = CreateSlots();
        private static SlotLabels _localLabels;
        private static Button _prevPageButton;
        private static Button _nextPageButton;
        private static Text _prevPageText;
        private static Text _nextPageText;
        private static int _currentPage;
        private static int _currentLobbyId = -1;
        private static Vector3 _nativePosition;
        private static Vector3 _nativeScale;
        private static bool _created;
        private static Text _fontTemplate;
        private static readonly HashSet<string> WarningKeys = new HashSet<string>();
        private static readonly Dictionary<string, int> TalkBubbleGenerations = new Dictionary<string, int>();

        public static bool IsCreated => _created;

        public static bool Refresh(LobbySyncPush lobby)
        {
            if (lobby == null)
            {
                Destroy();
                return true;
            }

            if (!ShouldDisplay(lobby))
            {
                HideGeneratedObjects();
                return true;
            }

            if (!EnsureCreated())
            {
                return false;
            }

            var localPlayer = GetLocalPlayer(lobby);
            ResetPageIfLobbyChanged(lobby);
            var otherPlayers = GetOtherPlayers(lobby).ToList();
            var pageCount = GetPageCount(otherPlayers.Count);
            _currentPage = ClampPage(_currentPage, pageCount);
            var visiblePlayers = GetVisibleOtherPlayers(otherPlayers);
            if (_nativeElfinShow != null) _nativeElfinShow.SetActive(false);

            if (visiblePlayers.Count == 0)
            {
                SetTransform(_nativeMuseShow, CenterX + LocalCharacterXOffset, LocalY, LocalScale);
                PrepareMuseShow(_nativeMuseShow, true);
                ApplyLabels(_localLabels, _nativeMuseShow, localPlayer, true);
                HideOtherSlots();
                RefreshPageButtons(pageCount);
                return true;
            }

            SetTransform(_nativeMuseShow, CenterX + LocalCharacterXOffset, LocalY, LocalScale);
            PrepareMuseShow(_nativeMuseShow, true);
            ApplyLabels(_localLabels, _nativeMuseShow, localPlayer, true);

            var ready = true;
            for (var i = 0; i < OtherSlots.Length; i++)
            {
                var player = i < visiblePlayers.Count ? visiblePlayers[i] : null;
                if (!ApplyOtherSlot(OtherSlots[i], player, i))
                {
                    ready = false;
                }
            }

            RefreshPageButtons(pageCount);
            return ready;
        }

        public static void UpdateLabelPositions(LobbySyncPush lobby)
        {
            if (!ShouldDisplay(lobby))
            {
                HideGeneratedObjects();
                return;
            }

            UpdateLabelPositionsForActiveSlots(lobby);
        }

        public static bool HasExpectedCharacterContent(LobbySyncPush lobby)
        {
            if (lobby == null || !ShouldDisplay(lobby)) return true;
            if (!_created || _nativeMuseShow == null || _localLabels == null) return false;

            var otherPlayers = GetOtherPlayers(lobby).ToList();
            var pageCount = GetPageCount(otherPlayers.Count);
            var currentPage = ClampPage(_currentPage, pageCount);
            var visiblePlayers = GetVisibleOtherPlayers(otherPlayers, currentPage);
            if (visiblePlayers.Count == 0)
            {
                return _nativeMuseShow.activeSelf &&
                       _localLabels.HasPlayer &&
                       OtherSlots.All(slot => !slot.IsActive);
            }

            if (!_nativeMuseShow.activeSelf || !_localLabels.HasPlayer) return false;
            for (var i = 0; i < OtherSlots.Length; i++)
            {
                var player = i < visiblePlayers.Count ? visiblePlayers[i] : null;
                if (!OtherSlots[i].HasExpectedContent(player)) return false;
            }

            return true;
        }

        public static void Destroy()
        {
            RestoreNativeMuseShow();
            DestroyGeneratedObjects();
            _nativeMuseShow = null;
            _nativeElfinShow = null;
            _fontTemplate = null;
            TalkBubbleGenerations.Clear();
            WarningKeys.Clear();
            _created = false;
        }

        public static void ShowChatBubble(string uid, string message)
        {
            if (!_created || string.IsNullOrWhiteSpace(uid) || string.IsNullOrWhiteSpace(message)) return;

            var normalizedUid = NormalizeUid(uid);
            var target = FindCharacterRoot(normalizedUid);
            if (target == null || !target.activeSelf) return;

            var bubble = GetTalkBubble(target);
            if (bubble == null) return;

            var safeMessage = EscapeRichText(message.Trim().Replace("\r", " ").Replace("\n", " "));
            if (string.IsNullOrWhiteSpace(safeMessage)) return;

            bubble.gameObject.SetActive(true);
            bubble.SetTalkTxt(safeMessage);
            PlayChatExpression(target);

            var generation = NextTalkBubbleGeneration(normalizedUid);
            _ = HideTalkBubbleLater(normalizedUid, bubble, GetTalkBubbleDurationMs(safeMessage), generation);
        }

        public static void DestroyGeneratedObjects()
        {
            RestoreNativeMuseShow();
            foreach (var slot in OtherSlots)
            {
                slot.Destroy();
            }

            _localLabels?.Destroy();
            _localLabels = null;
            DestroyPageButtons();
            _currentPage = 0;
            _currentLobbyId = -1;
            _created = false;
            DestroyOwnedObjectsByName();
        }

        private static bool ShouldDisplay(LobbySyncPush lobby)
        {
            return lobby != null &&
                   LobbyManager.IsInLobby &&
                   RoomSceneOverlay.IsHomeVisible;
        }

        private static bool EnsureCreated()
        {
            if (_created && _nativeMuseShow != null && _localLabels != null && !_localLabels.IsDestroyed)
            {
                return true;
            }

            DestroyGeneratedObjects();
            _nativeMuseShow = FindHomeChildByName("MuseShow") ?? FindByPathIncludingInactive("UI/Standerd/PnlHome/MuseShow");
            _nativeElfinShow = FindHomeChildByName("ElfinShow") ?? FindByPathIncludingInactive("UI/Standerd/PnlHome/ElfinShow");
            if (_nativeMuseShow == null)
            {
                WarnOnce("native-muse-missing", $"Native MuseShow is missing. Home children={DescribeHomeChildren()}");
                return false;
            }

            _nativePosition = _nativeMuseShow.transform.position;
            _nativeScale = _nativeMuseShow.transform.localScale;
            PrepareMuseShow(_nativeMuseShow, true);
            _localLabels = SlotLabels.Create(_nativeMuseShow.transform.parent, "MDENRoomLocal", LocalLabelSize, 24, 36, ApplyGameFont);
            CreatePageButtons(_nativeMuseShow.transform.parent);

            for (var i = 0; i < OtherSlots.Length; i++)
            {
                var root = UnityEngine.Object.Instantiate(_nativeMuseShow, _nativeMuseShow.transform.parent);
                root.name = $"MDENRoomCharacterOther{i}";
                PrepareMuseShow(root, false);
                root.SetActive(false);
                OtherSlots[i].Bind(
                    root,
                    SlotLabels.Create(root.transform.parent, $"MDENRoomOther{i}", OtherLabelSize, 22, 32, ApplyGameFont));
            }

            _created = true;
            return true;
        }

        private static bool ApplyOtherSlot(OtherCharacterSlot slot, RoomCharacterPlayer player, int slotIndex)
        {
            if (slot == null) return false;
            if (player == null)
            {
                slot.Hide();
                return true;
            }

            var offsetIndex = Mathf.Min(slotIndex / 2, OtherOffsets.Length - 1);
            var side = slotIndex % 2 == 0 ? 1f : -1f;
            var x = CenterX + side * OtherOffsets[offsetIndex];
            var sortingOrder = -(offsetIndex + 1);
            slot.Show(player, x, OtherY, OtherScale, sortingOrder);

            var girlIndex = player.GirlIndex < 0 ? 0 : player.GirlIndex;
            if (slot.IsSameCharacter(girlIndex) && slot.HasCharacterPrefab) return true;

            if (!ReplaceCharacter(slot, girlIndex))
            {
                slot.ResetCharacter();
                return false;
            }

            return true;
        }

        private static bool ReplaceCharacter(OtherCharacterSlot slot, int girlIndex)
        {
            var prefabTransform = slot.PrefabTransform;
            if (prefabTransform == null)
            {
                WarnOnce("prefab-transform-missing", "Character prefab transform is missing.");
                return false;
            }

            var charInfo = GetCharacterInfo(girlIndex);
            var assetName = charInfo?.mainShow;
            if (string.IsNullOrEmpty(assetName))
            {
                WarnOnce($"asset-missing-{girlIndex}", $"Character mainShow asset is missing. girlIndex={girlIndex}");
                return false;
            }

            var sourcePrefab = LoadCharacterPrefab(assetName);
            if (sourcePrefab == null)
            {
                WarnOnce($"prefab-not-loaded-{assetName}", $"Character prefab is not loaded. asset={assetName}");
                return false;
            }

            ClearCharacterPrefab(slot.Root);
            var newShow = UnityEngine.Object.Instantiate(sourcePrefab, prefabTransform);
            NormalizeCharacterPrefab(newShow, girlIndex, slot.SortingOrder);
            RemoveSpecialCharacterExtras(newShow, girlIndex);
            if (!HasVisibleCharacterContent(newShow))
            {
                WarnOnce($"visible-content-missing-{girlIndex}", $"Character has no visible content after load. girlIndex={girlIndex}");
                DestroyObject(newShow);
                return false;
            }

            var museComponent = prefabTransform.gameObject.GetComponent<MuseShow>();
            if (museComponent != null)
            {
                museComponent.m_MuseShow = newShow;
                BindCharacterExpression(slot.Root, museComponent);
            }

            slot.SetCharacter(girlIndex, newShow);
            MelonLogger.Msg($"Room character loaded: girlIndex={girlIndex}, asset={assetName}");
            return true;
        }

        private static Il2CppAssets.Scripts.Database.CharacterInfo GetCharacterInfo(int girlIndex)
        {
            try
            {
                return Singleton<ConfigManager>.instance
                    ?.GetConfigObject<DBConfigCharacter>()
                    ?.GetCharacterInfoByIndex(girlIndex);
            }
            catch (Exception ex)
            {
                WarnOnce($"character-info-error-{girlIndex}", $"Failed to read character info. girlIndex={girlIndex}, error={ex.Message}");
                return null;
            }
        }

        private static GameObject LoadCharacterPrefab(string assetName)
        {
            try
            {
                var manager = ResourcesManager.instance;
                return manager == null ? null : manager.LoadFromName<GameObject>(assetName);
            }
            catch (Exception ex)
            {
                WarnOnce($"prefab-load-error-{assetName}", $"Failed to load character prefab. asset={assetName}, error={ex.Message}");
                return null;
            }
        }

        private static void PrepareMuseShow(GameObject museShow, bool local)
        {
            if (museShow == null) return;
            museShow.SetActive(true);

            var interaction = museShow.transform.Find("BtnInteraction");
            if (interaction != null) interaction.gameObject.SetActive(false);

            var bubble = museShow.transform.Find("FirstTwnTalkBubble");
            if (bubble != null) bubble.gameObject.SetActive(false);

            if (!local)
            {
                ClearCharacterPrefab(museShow);
            }
            else
            {
                PruneDuplicateCharacterPrefabs(museShow);
            }
        }

        private static void SetTransform(GameObject target, float x, float y, float scale)
        {
            if (target == null) return;
            target.transform.position = new Vector3(x, y, Z);
            target.transform.localScale = new Vector3(scale, scale, scale);
        }

        private static void RestoreNativeMuseShow()
        {
            if (_nativeMuseShow != null)
            {
                _nativeMuseShow.transform.position = _nativePosition;
                _nativeMuseShow.transform.localScale = _nativeScale;

                var interaction = _nativeMuseShow.transform.Find("BtnInteraction");
                if (interaction != null) interaction.gameObject.SetActive(true);

                HideTalkBubble(_nativeMuseShow);
            }

            if (_nativeElfinShow != null)
            {
                _nativeElfinShow.SetActive(true);
            }
        }

        private static void HideGeneratedObjects()
        {
            _localLabels?.SetPlayer(null);
            HideOtherSlots();
            HidePageButtons();
            RestoreNativeMuseShow();
        }

        private static void HideOtherSlots()
        {
            foreach (var slot in OtherSlots)
            {
                slot.Hide();
            }
        }

        private static int GetPageCount(int otherPlayerCount)
        {
            return Math.Max(1, (otherPlayerCount + OtherSlotsPerPage - 1) / OtherSlotsPerPage);
        }

        private static int ClampPage(int page, int pageCount)
        {
            return Math.Max(0, Math.Min(page, pageCount - 1));
        }

        private static List<RoomCharacterPlayer> GetVisibleOtherPlayers(List<RoomCharacterPlayer> otherPlayers)
        {
            return GetVisibleOtherPlayers(otherPlayers, _currentPage);
        }

        private static List<RoomCharacterPlayer> GetVisibleOtherPlayers(List<RoomCharacterPlayer> otherPlayers, int page)
        {
            if (otherPlayers == null || otherPlayers.Count == 0)
            {
                return new List<RoomCharacterPlayer>();
            }

            var clampedPage = ClampPage(page, GetPageCount(otherPlayers.Count));
            return otherPlayers
                .Skip(clampedPage * OtherSlotsPerPage)
                .Take(OtherSlotsPerPage)
                .ToList();
        }

        private static void RefreshPageButtons(int pageCount)
        {
            var shouldShow = pageCount > 1 &&
                             _prevPageButton != null &&
                             _nextPageButton != null &&
                             _prevPageText != null &&
                             _nextPageText != null;
            if (_prevPageButton != null) _prevPageButton.gameObject.SetActive(shouldShow);
            if (_nextPageButton != null) _nextPageButton.gameObject.SetActive(shouldShow);
            if (!shouldShow) return;

            _prevPageText.text = "◀";
            _nextPageText.text = "▶";
            _prevPageButton.interactable = _currentPage > 0;
            _nextPageButton.interactable = _currentPage < pageCount - 1;
            PositionPageButtons();
        }

        private static void HidePageButtons()
        {
            if (_prevPageButton != null) _prevPageButton.gameObject.SetActive(false);
            if (_nextPageButton != null) _nextPageButton.gameObject.SetActive(false);
        }

        private static void ResetPageIfLobbyChanged(LobbySyncPush lobby)
        {
            if (lobby == null) return;
            if (_currentLobbyId == lobby.Id) return;

            _currentLobbyId = lobby.Id;
            _currentPage = 0;
        }

        private static void ChangePage(int delta)
        {
            var lobby = LobbyManager.CurrentLobby;
            if (lobby == null) return;

            var pageCount = GetPageCount(GetOtherPlayers(lobby).Count());
            var nextPage = ClampPage(_currentPage + delta, pageCount);
            if (nextPage == _currentPage) return;

            _currentPage = nextPage;
            RoomHudController.RequestRefresh();
        }

        private static void CreatePageButtons(Transform parent)
        {
            _prevPageButton = CreatePageButton(parent, "MDENRoomCharacterPrev", "◀", true, () => ChangePage(-1), out _prevPageText);
            _nextPageButton = CreatePageButton(parent, "MDENRoomCharacterNext", "▶", false, () => ChangePage(1), out _nextPageText);
            RefreshPageButtons(1);
        }

        private static Button CreatePageButton(
            Transform parent,
            string name,
            string label,
            bool leftSide,
            Action action,
            out Text text)
        {
            var obj = new GameObject(name);
            var rect = obj.AddComponent<RectTransform>();
            rect.SetParent(parent);
            rect.localScale = Vector3.one;
            rect.sizeDelta = PageButtonSize;

            text = obj.AddComponent<Text>();
            ApplyGameFont(text);
            text.text = label;
            text.fontSize = 58;
            text.alignment = TextAnchor.MiddleCenter;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.supportRichText = true;
            text.raycastTarget = true;
            text.color = PageButtonNormalColor;

            var shadow = obj.AddComponent<Shadow>();
            shadow.effectDistance = new Vector2(2f, -2f);
            shadow.effectColor = new Color(0f, 0f, 0f, 0.45f);

            var button = obj.AddComponent<Button>();
            button.targetGraphic = text;
            button.transition = Selectable.Transition.ColorTint;
            button.colors = new ColorBlock
            {
                normalColor = PageButtonNormalColor,
                highlightedColor = PageButtonHighlightedColor,
                pressedColor = PageButtonPressedColor,
                selectedColor = PageButtonHighlightedColor,
                disabledColor = PageButtonDisabledColor,
                colorMultiplier = 1f,
                fadeDuration = 0.08f
            };
            button.onClick.AddListener((UnityAction)(() => action.Invoke()));
            obj.SetActive(false);

            SetPageButtonPosition(obj, leftSide);
            return button;
        }

        private static void PositionPageButtons()
        {
            SetPageButtonPosition(_prevPageButton?.gameObject, true);
            SetPageButtonPosition(_nextPageButton?.gameObject, false);
            _prevPageButton?.transform.SetAsLastSibling();
            _nextPageButton?.transform.SetAsLastSibling();
        }

        private static void SetPageButtonPosition(GameObject obj, bool leftSide)
        {
            if (obj == null) return;

            var camera = Camera.main;
            if (camera == null || _nativeMuseShow == null)
            {
                obj.transform.position = new Vector3(leftSide ? -8f : 10f, LocalY + 2f, Z + LabelZOffset);
                return;
            }

            var screenX = leftSide ? PageButtonScreenEdgeOffset : Screen.width - PageButtonScreenEdgeOffset;
            var screenY = Screen.height * PageButtonScreenYRatio;
            var nativeDepth = camera.WorldToScreenPoint(_nativeMuseShow.transform.position).z;
            var position = camera.ScreenToWorldPoint(new Vector3(screenX, screenY, nativeDepth));
            obj.transform.position = new Vector3(position.x, position.y, Z + LabelZOffset);
        }

        private static void DestroyPageButtons()
        {
            DestroyObject(_prevPageButton?.gameObject);
            DestroyObject(_nextPageButton?.gameObject);
            _prevPageButton = null;
            _nextPageButton = null;
            _prevPageText = null;
            _nextPageText = null;
        }

        private static GameObject FindCharacterRoot(string uid)
        {
            if (IsSameUid(uid, PlayerManager.CurrentUid)) return _nativeMuseShow;

            foreach (var slot in OtherSlots)
            {
                if (slot.IsPlayer(uid)) return slot.Root;
            }

            return null;
        }

        private static DefaultTalkBubble GetTalkBubble(GameObject museShow)
        {
            var bubble = museShow?.transform.Find("FirstTwnTalkBubble");
            return bubble == null ? null : bubble.gameObject.GetComponent<DefaultTalkBubble>();
        }

        private static void PlayChatExpression(GameObject museShow)
        {
            var expression = museShow?.transform.Find("BtnInteraction")?.GetComponent<CharacterExpression>();
            if (expression == null) return;

            try
            {
                var expressionInfo = expression.expressionContainer?.RandomExpression();
                var museComponent = museShow.transform.Find("ShowLocalization/SpinePerfab_other")?.gameObject.GetComponent<MuseShow>();
                var apply = museComponent?.apply;
                if (expressionInfo == null || apply == null) return;

                apply.PlayCharacterApply(expressionInfo.animName, null);
            }
            catch (Exception ex)
            {
                WarnOnce($"chat-expression-{museShow.name}", $"Failed to play chat expression. target={museShow.name}, error={ex.Message}");
            }
        }

        private static void BindCharacterExpression(GameObject museShow, MuseShow museComponent)
        {
            var expression = museShow?.transform.Find("BtnInteraction")?.GetComponent<CharacterExpression>();
            if (expression == null || museComponent == null) return;

            try
            {
                expression.SetMuseShow(museComponent);
            }
            catch (Exception ex)
            {
                WarnOnce($"character-expression-bind-{museShow.name}", $"Failed to bind character expression. target={museShow.name}, error={ex.Message}");
            }
        }

        private static void HideTalkBubble(GameObject museShow)
        {
            var bubble = GetTalkBubble(museShow);
            if (bubble == null) return;

            try
            {
                bubble.EndTalk();
            }
            catch
            {
                // 原生气泡在场景切换时可能已经处于销毁边缘，隐藏失败可以忽略。
            }

            bubble.gameObject.SetActive(false);
        }

        private static int NextTalkBubbleGeneration(string uid)
        {
            if (!TalkBubbleGenerations.TryGetValue(uid, out var generation))
            {
                generation = 0;
            }

            generation++;
            TalkBubbleGenerations[uid] = generation;
            return generation;
        }

        private static async System.Threading.Tasks.Task HideTalkBubbleLater(string uid, DefaultTalkBubble bubble, int durationMs, int generation)
        {
            await System.Threading.Tasks.Task.Delay(durationMs);
            MainThreadDispatcher.Enqueue(() =>
            {
                if (!TalkBubbleGenerations.TryGetValue(uid, out var currentGeneration) ||
                    currentGeneration != generation ||
                    bubble == null)
                {
                    return;
                }

                try
                {
                    bubble.EndTalk();
                }
                catch
                {
                    return;
                }

                _ = HideTalkBubbleObjectLater(uid, bubble, generation);
            });
        }

        private static async System.Threading.Tasks.Task HideTalkBubbleObjectLater(string uid, DefaultTalkBubble bubble, int generation)
        {
            await System.Threading.Tasks.Task.Delay(300);
            MainThreadDispatcher.Enqueue(() =>
            {
                if (!TalkBubbleGenerations.TryGetValue(uid, out var currentGeneration) ||
                    currentGeneration != generation ||
                    bubble == null)
                {
                    return;
                }

                bubble.gameObject.SetActive(false);
            });
        }

        private static int GetTalkBubbleDurationMs(string message)
        {
            var wordCount = message.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).Length;
            var duration = wordCount * 500 + 800
                + message.Count(c => c == ',' || c == '，') * 200
                + message.Count(c => c == '.' || c == '。') * 400
                + message.Count(c => c == '!' || c == '！') * 200
                + message.Count(c => c == '?' || c == '？') * 300;

            return Math.Max(1200, Math.Min(duration, 10000));
        }

        private static void UpdateLabelPositionsForActiveSlots(LobbySyncPush lobby)
        {
            if (!_created || !ShouldDisplay(lobby)) return;

            _localLabels?.UpdatePosition(_nativeMuseShow, true);
            foreach (var slot in OtherSlots)
            {
                slot.UpdateLabels();
            }
        }

        private static void ClearCharacterPrefab(GameObject museShow)
        {
            var prefabTransform = museShow?.transform.Find("ShowLocalization/SpinePerfab_other");
            if (prefabTransform == null) return;

            for (var i = prefabTransform.childCount - 1; i >= 0; i--)
            {
                DestroyObject(prefabTransform.GetChild(i).gameObject);
            }

            var museComponent = prefabTransform.gameObject.GetComponent<MuseShow>();
            if (museComponent != null) museComponent.m_MuseShow = null;
        }

        private static void PruneDuplicateCharacterPrefabs(GameObject museShow)
        {
            var prefabTransform = museShow?.transform.Find("ShowLocalization/SpinePerfab_other");
            if (prefabTransform == null || prefabTransform.childCount <= 1) return;

            GameObject keptObject = null;
            for (var i = prefabTransform.childCount - 1; i >= 0; i--)
            {
                var child = prefabTransform.GetChild(i);
                if (child == null) continue;

                if (keptObject == null && child.gameObject.activeSelf && HasVisibleCharacterContent(child.gameObject))
                {
                    keptObject = child.gameObject;
                    break;
                }
            }

            if (keptObject == null) return;

            for (var i = prefabTransform.childCount - 1; i >= 0; i--)
            {
                var child = prefabTransform.GetChild(i);
                if (child == null || child.gameObject == keptObject) continue;

                DestroyObject(child.gameObject);
            }

            var museComponent = prefabTransform.gameObject.GetComponent<MuseShow>();
            if (museComponent != null)
            {
                museComponent.m_MuseShow = keptObject;
            }
        }

        private static void NormalizeCharacterPrefab(GameObject root, int girlIndex, int sortingOrder)
        {
            if (root == null) return;

            root.SetActive(true);
            if (girlIndex == PirateRinGirlIndex)
            {
                ActivateHierarchy(root.transform);
            }

            RemoveCharacterExpressionComponents(root);

            var renderers = root.GetComponentsInChildren<Renderer>(true);
            if (renderers != null)
            {
                foreach (var renderer in renderers)
                {
                    if (renderer == null) continue;
                    renderer.gameObject.SetActive(true);
                    renderer.enabled = true;
                    renderer.sortingOrder = sortingOrder;
                    ResetRendererAlpha(renderer);
                }
            }

            var canvasGroups = root.GetComponentsInChildren<CanvasGroup>(true);
            if (canvasGroups != null)
            {
                foreach (var canvasGroup in canvasGroups)
                {
                    if (canvasGroup == null) continue;
                    canvasGroup.alpha = 1f;
                    canvasGroup.gameObject.SetActive(true);
                }
            }

            var graphics = root.GetComponentsInChildren<Graphic>(true);
            if (graphics != null)
            {
                foreach (var graphic in graphics)
                {
                    if (graphic == null) continue;
                    graphic.enabled = true;
                    graphic.gameObject.SetActive(true);
                    var color = graphic.color;
                    color.a = 1f;
                    graphic.color = color;
                    graphic.canvasRenderer.SetAlpha(1f);
                }
            }
        }

        private static void ActivateHierarchy(Transform transform)
        {
            if (transform == null) return;

            transform.gameObject.SetActive(true);
            for (var i = 0; i < transform.childCount; i++)
            {
                ActivateHierarchy(transform.GetChild(i));
            }
        }

        private static void RemoveSpecialCharacterExtras(GameObject newShow, int girlIndex)
        {
            if (newShow == null) return;

            switch (girlIndex)
            {
                case 31:
                    var bloodheirHandler = newShow.GetComponent<Il2CppAssets.Scripts.UI.Specials.BloodheirTransformGenerator>();
                    if (bloodheirHandler != null && bloodheirHandler.m_BloodheirTransformObj != null)
                    {
                        UnityEngine.Object.Destroy(bloodheirHandler.m_BloodheirTransformObj);
                    }
                    break;
                case 33:
                    var diverHandler = newShow.GetComponent<Il2CppAssets.Scripts.UI.Specials.DiverBuroHandler>();
                    if (diverHandler != null && diverHandler.m_PnlDaveFishViewObj != null)
                    {
                        UnityEngine.Object.Destroy(diverHandler.m_PnlDaveFishViewObj);
                    }
                    break;
            }
        }

        private static void RemoveCharacterExpressionComponents(GameObject root)
        {
            var behaviours = root.GetComponentsInChildren<MonoBehaviour>(true);
            if (behaviours == null) return;

            foreach (var behaviour in behaviours)
            {
                if (behaviour == null) continue;
                if (behaviour.GetType().Name == "CharacterExpression")
                {
                    UnityEngine.Object.Destroy(behaviour);
                }
            }
        }

        private static bool HasVisibleCharacterContent(GameObject root)
        {
            if (root == null || !root.activeSelf) return false;

            var renderers = root.GetComponentsInChildren<Renderer>(true);
            if (renderers != null && renderers.Any(renderer =>
                    renderer != null &&
                    renderer.enabled &&
                    renderer.gameObject.activeInHierarchy &&
                    HasVisibleRendererAlpha(renderer)))
            {
                return true;
            }

            var graphics = root.GetComponentsInChildren<Graphic>(true);
            return graphics != null && graphics.Any(graphic =>
                graphic != null &&
                graphic.enabled &&
                graphic.gameObject.activeInHierarchy &&
                graphic.color.a > 0.01f);
        }

        private static void ResetRendererAlpha(Renderer renderer)
        {
            try
            {
                var materials = renderer.materials;
                if (materials == null) return;

                foreach (var material in materials)
                {
                    ResetMaterialAlpha(material, "_Color");
                    ResetMaterialAlpha(material, "_TintColor");
                }
            }
            catch
            {
                // 部分原生材质没有可写颜色属性，跳过即可。
            }
        }

        private static void ResetMaterialAlpha(Material material, string propertyName)
        {
            if (material == null || !material.HasProperty(propertyName)) return;

            var color = material.GetColor(propertyName);
            color.a = 1f;
            material.SetColor(propertyName, color);
        }

        private static bool HasVisibleRendererAlpha(Renderer renderer)
        {
            try
            {
                var materials = renderer.materials;
                if (materials == null || materials.Length == 0) return true;

                var checkedColor = false;
                foreach (var material in materials)
                {
                    if (material == null) continue;
                    if (HasVisibleMaterialColor(material, "_Color", ref checkedColor) ||
                        HasVisibleMaterialColor(material, "_TintColor", ref checkedColor))
                    {
                        return true;
                    }
                }

                return !checkedColor;
            }
            catch
            {
                return true;
            }
        }

        private static bool HasVisibleMaterialColor(Material material, string propertyName, ref bool checkedColor)
        {
            if (!material.HasProperty(propertyName)) return false;

            checkedColor = true;
            return material.GetColor(propertyName).a > 0.01f;
        }

        private static RoomCharacterPlayer GetLocalPlayer(LobbySyncPush lobby)
        {
            var uid = PlayerManager.CurrentUid;
            return GetPlayers(lobby).FirstOrDefault(p => IsSameUid(p.Uid, uid))
                ?? new RoomCharacterPlayer
                {
                    Uid = uid,
                    Name = PlayerManager.CurrentProfile?.Name ?? uid,
                    Title = PlayerManager.CurrentProfile?.Title,
                    ChatColor = PlayerManager.CurrentProfile?.ChatColor,
                    GirlIndex = GetLocalDisplaySelection().GirlIndex,
                    ElfinIndex = GetLocalDisplaySelection().ElfinIndex,
                    Status = (byte)PlayerStatus.InLobby,
                    IsHost = IsSameUid(uid, lobby.HostUid)
                };
        }

        private static IEnumerable<RoomCharacterPlayer> GetOtherPlayers(LobbySyncPush lobby)
        {
            var currentUid = PlayerManager.CurrentUid;
            foreach (var player in GetPlayers(lobby))
            {
                if (IsSameUid(player.Uid, currentUid)) continue;
                if (player.Status == (byte)PlayerStatus.Offline) continue;
                yield return player;
            }
        }

        private static IEnumerable<RoomCharacterPlayer> GetPlayers(LobbySyncPush lobby)
        {
            var characterMap = lobby.PlayerCharacters?
                .Where(p => !string.IsNullOrEmpty(NormalizeUid(p?.Uid)))
                .GroupBy(p => NormalizeUid(p.Uid))
                .ToDictionary(group => group.Key, group => group.First());
            var seenUids = new HashSet<string>();

            if (lobby.PlayerDetails != null && lobby.PlayerDetails.Length > 0)
            {
                foreach (var player in lobby.PlayerDetails)
                {
                    var uid = NormalizeUid(player?.Uid);
                    if (string.IsNullOrEmpty(uid)) continue;
                    if (!seenUids.Add(uid)) continue;
                    var character = GetCharacter(characterMap, uid);
                    yield return new RoomCharacterPlayer
                    {
                        Uid = uid,
                        Name = string.IsNullOrEmpty(player.Name) ? uid : player.Name,
                        Bio = player.Bio,
                        Title = GetDisplayTitle(player),
                        ChatColor = GetDisplayColor(player),
                        PingMS = player.PingMS,
                        Status = player.Status,
                        GirlIndex = GetGirlIndex(uid, character),
                        ElfinIndex = GetElfinIndex(uid, character),
                        IsHost = IsSameUid(uid, lobby.HostUid)
                    };
                }

                yield break;
            }

            if (lobby.Players == null) yield break;
            foreach (var rawUid in lobby.Players)
            {
                var uid = NormalizeUid(rawUid);
                if (string.IsNullOrEmpty(uid)) continue;
                if (!seenUids.Add(uid)) continue;
                var character = GetCharacter(characterMap, uid);
                yield return new RoomCharacterPlayer
                {
                    Uid = uid,
                    Name = uid,
                    Bio = IsSameUid(uid, PlayerManager.CurrentUid) ? PlayerManager.CurrentProfile?.Bio : null,
                    Title = IsSameUid(uid, PlayerManager.CurrentUid) ? PlayerManager.CurrentProfile?.Title : null,
                    ChatColor = IsSameUid(uid, PlayerManager.CurrentUid) ? PlayerManager.CurrentProfile?.ChatColor : null,
                    GirlIndex = GetGirlIndex(uid, character),
                    ElfinIndex = GetElfinIndex(uid, character),
                    Status = (byte)(IsSameUid(uid, PlayerManager.CurrentUid) ? PlayerStatus.InLobby : PlayerStatus.Offline),
                    IsHost = IsSameUid(uid, lobby.HostUid)
                };
            }
        }

        private static int GetGirlIndex(string uid, LobbyPlayerCharacterEntry character)
        {
            if (IsSameUid(uid, PlayerManager.CurrentUid))
            {
                return GetLocalDisplaySelection().GirlIndex;
            }

            var favGirlIndex = character?.FavGirlIndex ?? -1;
            return favGirlIndex >= 0 ? favGirlIndex : character?.GirlIndex ?? 0;
        }

        private static int GetElfinIndex(string uid, LobbyPlayerCharacterEntry character)
        {
            if (IsSameUid(uid, PlayerManager.CurrentUid))
            {
                return GetLocalDisplaySelection().ElfinIndex;
            }

            var favElfinIndex = character?.FavElfinIndex ?? -2;
            return favElfinIndex >= -1 ? favElfinIndex : character?.ElfinIndex ?? -1;
        }

        private static GameSelectionInfo GetLocalDisplaySelection()
        {
            return ModConfigManager.EnableFavGirlDisplayForOthers
                ? GameAccountManager.GetCurrentFavGirlSelection()
                : GameAccountManager.GetCurrentSelection();
        }

        private static LobbyPlayerCharacterEntry GetCharacter(
            Dictionary<string, LobbyPlayerCharacterEntry> characterMap,
            string uid)
        {
            if (characterMap == null || string.IsNullOrEmpty(uid)) return null;
            characterMap.TryGetValue(uid, out var character);
            return character;
        }

        private static string GetDisplayTitle(PlayerSyncEntry player)
        {
            if (IsSameUid(player?.Uid, PlayerManager.CurrentUid) && !string.IsNullOrEmpty(PlayerManager.CurrentProfile?.Title))
            {
                return PlayerManager.CurrentProfile.Title;
            }

            return player?.Title;
        }

        private static string GetDisplayColor(PlayerSyncEntry player)
        {
            if (IsSameUid(player?.Uid, PlayerManager.CurrentUid) && !string.IsNullOrEmpty(PlayerManager.CurrentProfile?.ChatColor))
            {
                return PlayerManager.CurrentProfile.ChatColor;
            }

            return player?.ChatColor;
        }

        private static PlayerSyncEntry ToPlayerSyncEntry(RoomCharacterPlayer player)
        {
            return new PlayerSyncEntry
            {
                Uid = player.Uid,
                Name = player.Name,
                Bio = player.Bio,
                Title = player.Title,
                ChatColor = player.ChatColor,
                PingMS = player.PingMS,
                Status = player.Status
            };
        }

        private static void ApplyLabels(SlotLabels labels, GameObject slot, RoomCharacterPlayer player, bool local)
        {
            if (labels == null || slot == null)
            {
                return;
            }

            if (player == null)
            {
                labels.SetPlayer(null);
                return;
            }

            labels.SetPlayer(player);
            labels.UpdatePosition(slot, local);
            var entry = ToPlayerSyncEntry(player);
            labels.Button.onClick = new Button.ButtonClickedEvent();
            labels.Button.onClick.AddListener((UnityAction)(() => WindowStackController.OpenWindow(new RoomPlayerWindow(entry))));
        }

        private static string FormatTitle(string title)
        {
            return string.IsNullOrWhiteSpace(title)
                ? string.Empty
                : $"<color=#{Constants.ColorYellow}>[{EscapeRichText(title)}]</color>";
        }

        private static string FormatName(string name, string chatColor)
        {
            var displayName = string.IsNullOrWhiteSpace(name) ? "Unknown" : Truncate(EscapeRichText(name), 12);
            return $"<b><color=#{NormalizeHexColor(chatColor) ?? Constants.ColorPink}>{displayName}</color></b>";
        }

        private static string NormalizeHexColor(string color)
        {
            if (string.IsNullOrWhiteSpace(color)) return null;

            var value = color.Trim().TrimStart('#');
            if (value.Length == 3)
            {
                value = $"{value[0]}{value[0]}{value[1]}{value[1]}{value[2]}{value[2]}";
            }

            if (value.Length == 6) value += "ff";
            if (value.Length != 8) return null;

            for (var i = 0; i < value.Length; i++)
            {
                if (!Uri.IsHexDigit(value[i])) return null;
            }

            return value;
        }

        private static string Truncate(string value, int maxLength)
        {
            if (string.IsNullOrEmpty(value) || value.Length <= maxLength) return value;
            return value.Substring(0, maxLength) + "...";
        }

        private static string EscapeRichText(string value)
        {
            return value?.Replace("<", "＜").Replace(">", "＞") ?? string.Empty;
        }

        private static string NormalizeUid(string uid)
        {
            return uid?.Trim();
        }

        private static bool IsSameUid(string left, string right)
        {
            return string.Equals(NormalizeUid(left), NormalizeUid(right), StringComparison.OrdinalIgnoreCase);
        }

        private static float GetScreenScale()
        {
            return Mathf.Max(0.85f, Screen.height / 1080f);
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

        private static GameObject FindHomeChildByName(string objectName)
        {
            if (string.IsNullOrWhiteSpace(objectName)) return null;

            var home = FindByPathIncludingInactive("UI/Standerd/PnlHome") ?? FindSceneObjectByName("PnlHome");
            if (home == null) return null;

            return FindChildByName(home.transform, objectName);
        }

        private static GameObject FindSceneObjectByName(string objectName)
        {
            var objects = UnityEngine.Resources.FindObjectsOfTypeAll<GameObject>();
            foreach (var obj in objects)
            {
                if (obj == null || obj.name != objectName) continue;
                if (!obj.scene.IsValid() || !obj.scene.isLoaded) continue;
                return obj;
            }

            return null;
        }

        private static GameObject FindChildByName(Transform root, string objectName)
        {
            if (root == null) return null;
            if (root.name == objectName) return root.gameObject;

            for (var i = 0; i < root.childCount; i++)
            {
                var match = FindChildByName(root.GetChild(i), objectName);
                if (match != null) return match;
            }

            return null;
        }

        private static string DescribeHomeChildren()
        {
            var home = FindByPathIncludingInactive("UI/Standerd/PnlHome") ?? FindSceneObjectByName("PnlHome");
            if (home == null) return "PnlHome not found";

            var names = new List<string>();
            var limit = Mathf.Min(home.transform.childCount, 24);
            for (var i = 0; i < limit; i++)
            {
                names.Add(home.transform.GetChild(i).name);
            }

            if (home.transform.childCount > limit)
            {
                names.Add("...");
            }

            return string.Join(", ", names);
        }

        private static void WarnOnce(string key, string message)
        {
            if (WarningKeys.Add(key))
            {
                MelonLogger.Warning(message);
            }
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
            if (_fontTemplate != null && _fontTemplate.font != null)
            {
                return _fontTemplate;
            }

            Text fallback = null;
            var texts = UnityEngine.Resources.FindObjectsOfTypeAll<Text>();
            if (texts != null)
            {
                for (var i = 0; i < texts.Length; i++)
                {
                    var text = texts[i];
                    if (text == null || text.font == null) continue;
                    fallback ??= text;

                    var fontName = text.font.name ?? string.Empty;
                    if (!fontName.Contains("Arial"))
                    {
                        _fontTemplate = text;
                        return _fontTemplate;
                    }
                }
            }

            _fontTemplate = fallback;
            return _fontTemplate;
        }

        private static void DestroyObject(GameObject obj)
        {
            if (obj == null) return;

            obj.SetActive(false);
            UnityEngine.Object.Destroy(obj);
        }

        private static void DestroyOwnedObjectsByName()
        {
            foreach (var objectName in OwnedObjectNames)
            {
                var obj = GameObject.Find(objectName);
                if (obj == null) continue;
                DestroyObject(obj);
            }
        }

        private static OtherCharacterSlot[] CreateSlots()
        {
            var slots = new OtherCharacterSlot[MaxOtherSlots];
            for (var i = 0; i < slots.Length; i++)
            {
                slots[i] = new OtherCharacterSlot();
            }

            return slots;
        }

        private sealed class OtherCharacterSlot
        {
            private int _girlIndex = -1;
            private string _uid;
            private GameObject _currentShow;

            public GameObject Root { get; private set; }
            public SlotLabels Labels { get; private set; }
            public int SortingOrder { get; private set; }
            public bool IsActive => Root != null && Root.activeSelf;
            public bool HasCharacterPrefab => _currentShow != null && _currentShow.activeSelf && HasVisibleCharacterContent(_currentShow);
            public Transform PrefabTransform => Root?.transform.Find("ShowLocalization/SpinePerfab_other");

            public void Bind(GameObject root, SlotLabels labels)
            {
                Root = root;
                Labels = labels;
                _girlIndex = -1;
                _uid = null;
                _currentShow = null;
            }

            public void Show(RoomCharacterPlayer player, float x, float y, float scale, int sortingOrder)
            {
                if (Root == null) return;

                Root.SetActive(true);
                SetTransform(Root, x, y, scale);
                SortingOrder = sortingOrder;
                SetRendererSorting(Root, sortingOrder);
                if (!IsSameUid(_uid, player.Uid))
                {
                    HideTalkBubble(Root);
                }

                ApplyLabels(Labels, Root, player, false);
                _uid = player.Uid;
            }

            public void Hide()
            {
                if (Root != null) Root.SetActive(false);
                HideTalkBubble(Root);
                Labels?.SetPlayer(null);
                _uid = null;
                _girlIndex = -1;
            }

            public bool IsPlayer(string uid)
            {
                return IsActive && IsSameUid(_uid, uid);
            }

            public bool IsSameCharacter(int girlIndex)
            {
                return _girlIndex == girlIndex;
            }

            public void SetCharacter(int girlIndex, GameObject currentShow)
            {
                _girlIndex = girlIndex;
                _currentShow = currentShow;
            }

            public void ResetCharacter()
            {
                _girlIndex = -1;
                _currentShow = null;
            }

            public bool HasExpectedContent(RoomCharacterPlayer player)
            {
                if (player == null) return !IsActive && (Labels == null || !Labels.HasPlayer);
                return IsActive &&
                       IsSameUid(_uid, player.Uid) &&
                       Labels != null &&
                       Labels.HasPlayer &&
                       HasCharacterPrefab;
            }

            public void UpdateLabels()
            {
                Labels?.UpdatePosition(Root, false);
            }

            public void Destroy()
            {
                DestroyObject(Root);
                Labels?.Destroy();
                Root = null;
                Labels = null;
                _girlIndex = -1;
                _uid = null;
                _currentShow = null;
            }

            private static void SetRendererSorting(GameObject root, int sortingOrder)
            {
                var renderers = root?.GetComponentsInChildren<Renderer>(true);
                if (renderers == null) return;

                foreach (var renderer in renderers)
                {
                    if (renderer == null) continue;
                    renderer.sortingOrder = sortingOrder;
                }
            }
        }

        private sealed class SlotLabels
        {
            private readonly Text _title;
            private readonly Text _name;
            private string _uid;

            private SlotLabels(Text title, Text name)
            {
                _title = title;
                _name = name;
                Button = name.GetComponent<Button>();
            }

            public bool HasPlayer => !string.IsNullOrEmpty(_uid);
            public Button Button { get; }
            public bool IsDestroyed => _title == null || _name == null || Button == null;

            public static SlotLabels Create(
                Transform parent,
                string prefix,
                Vector2 size,
                int titleFontSize,
                int nameFontSize,
                Action<Text> applyFont)
            {
                var title = CreateText(parent, prefix + "Title", size, titleFontSize, false, applyFont);
                var name = CreateText(parent, prefix + "Name", size, nameFontSize, true, applyFont);
                return new SlotLabels(title, name);
            }

            public void SetPlayer(RoomCharacterPlayer player)
            {
                if (player == null)
                {
                    _uid = null;
                    SetVisible(false);
                    return;
                }

                _uid = player.Uid;
                _title.text = FormatTitle(player.Title);
                _name.text = FormatName(player.Name, player.ChatColor);
                SetVisible(true);
            }

            public void UpdatePosition(GameObject slot, bool local)
            {
                if (slot == null || !slot.activeSelf || !HasPlayer)
                {
                    SetVisible(false);
                    return;
                }

                var titleYOffset = local ? LocalTitleYOffset : OtherTitleYOffset;
                var nameYOffset = local ? LocalNameYOffset : OtherNameYOffset;
                var slotPosition = slot.transform.position;
                var labelX = slotPosition.x + (local ? LocalLabelXOffset : LabelXOffset);
                var labelZ = slotPosition.z + LabelZOffset;
                SetPosition(
                    new Vector3(labelX, slotPosition.y + titleYOffset, labelZ),
                    new Vector3(labelX, slotPosition.y + nameYOffset, labelZ));
                SetAsLastSibling();
                SetVisible(true);
            }

            public void Destroy()
            {
                if (_title != null) UnityEngine.Object.Destroy(_title.gameObject);
                if (_name != null) UnityEngine.Object.Destroy(_name.gameObject);
            }

            private static Text CreateText(
                Transform parent,
                string name,
                Vector2 size,
                int fontSize,
                bool clickable,
                Action<Text> applyFont)
            {
                var obj = new GameObject(name);
                var rect = obj.AddComponent<RectTransform>();
                rect.SetParent(parent);
                rect.localScale = Vector3.one;
                rect.sizeDelta = size;
                rect.anchoredPosition3D = Vector3.zero;

                var text = obj.AddComponent<Text>();
                applyFont(text);
                text.fontSize = fontSize;
                text.alignment = TextAnchor.MiddleCenter;
                text.horizontalOverflow = HorizontalWrapMode.Overflow;
                text.verticalOverflow = VerticalWrapMode.Overflow;
                text.raycastTarget = clickable;
                text.supportRichText = true;
                text.color = Color.white;

                var shadow = obj.AddComponent<Shadow>();
                shadow.effectDistance = new Vector2(2f, -2f);
                shadow.effectColor = new Color(0f, 0f, 0f, 0.45f);

                if (clickable)
                {
                    var button = obj.AddComponent<Button>();
                    button.targetGraphic = text;
                    button.transition = Selectable.Transition.ColorTint;
                }

                obj.SetActive(false);
                return text;
            }

            private void SetVisible(bool visible)
            {
                if (_title != null) _title.gameObject.SetActive(visible && !string.IsNullOrEmpty(_title.text));
                if (_name != null) _name.gameObject.SetActive(visible);
            }

            private void SetPosition(Vector3 titlePosition, Vector3 namePosition)
            {
                if (_title != null) _title.transform.position = titlePosition;
                if (_name != null) _name.transform.position = namePosition;
            }

            private void SetAsLastSibling()
            {
                if (_title != null) _title.transform.SetAsLastSibling();
                if (_name != null) _name.transform.SetAsLastSibling();
            }
        }

        private sealed class RoomCharacterPlayer
        {
            public string Uid { get; set; }
            public string Name { get; set; }
            public string Bio { get; set; }
            public string Title { get; set; }
            public string ChatColor { get; set; }
            public ushort PingMS { get; set; }
            public byte Status { get; set; }
            public int GirlIndex { get; set; }
            public int ElfinIndex { get; set; }
            public bool IsHost { get; set; }
        }
    }
}
