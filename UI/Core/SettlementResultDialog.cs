using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using MDEN.Managers;
using MDEN.Protocol.Messages.Battle;
using MelonLoader;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Events;
using UnityEngine.UI;

namespace MDEN.UI.Core
{
    public static class SettlementResultDialog
    {
        private const string RootName = "MDENSettlementResultDialog";
        private const int SortingOrder = 32767;
        private const float PanelWidth = 1120f;
        private const float PanelHeight = 760f;
        private const float SidePadding = 54f;
        private const float AwardCardWidth = 242f;
        private const float AwardCardHeight = 154f;
        private const float AwardNameRowHeight = 34f;
        private const float AwardNamesViewportHeight = 78f;
        private const float ChartViewportHeight = 290f;
        private const float CloseSoundVolume = 1.35f;
        private const float CloseButtonHitPadding = 18f;

        private static GameObject _root;
        private static RectTransform _panelRoot;
        private static RectTransform _closeButtonRect;
        private static Font _cachedFont;
        private static Sprite _roundedSprite;
        private static bool _closing;
        private static bool _enterWasDown;
        private static bool _mouseWasDown;
        private static int _ignoreMouseInputUntilFrame;

        public static bool IsVisible => _root != null;

        public static void Show(SettlementResultPush result)
        {
            try
            {
                Destroy();

                _root = CreateRoot();
                try
                {
                    var panel = CreatePanel(_root.transform);
                    CreateHeader(panel);
                    CreateAwardCards(panel, result);
                    CreatePlayedCharts(panel, result);
                    CreateCloseButton(panel);
                }
                catch (Exception ex)
                {
                    MelonLogger.Error($"Show settlement result overlay failed, fallback UI will be used: {ex}");
                    ClearRootChildren();
                    CreateFallback(result);
                }

                _enterWasDown = IsEnterKeyDown();
                _mouseWasDown = Input.GetMouseButton(0);
                _ignoreMouseInputUntilFrame = Time.frameCount + 1;
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Show settlement result dialog failed: {ex}");
                Destroy();
            }
        }

        public static void Update()
        {
            if (_root == null || _closing) return;

            HandleEnterKeyFallback();
            if (_root == null || _closing) return;

            HandleMouseFallback();
        }

        public static void Destroy()
        {
            if (_root != null)
            {
                UnityEngine.Object.Destroy(_root);
            }

            _root = null;
            _panelRoot = null;
            _closeButtonRect = null;
            _closing = false;
            _enterWasDown = false;
            _mouseWasDown = false;
            _ignoreMouseInputUntilFrame = 0;
        }

        private static void CloseWithSound()
        {
            if (_root == null || _closing) return;

            _closing = true;
            UiSoundManager.Play(UiSound.Yes, CloseSoundVolume);
            BattleResultFlowManager.SetCanExitBattleResult(true);
            BattleResultFlowManager.SetBattleResultFlowPending(false);
            Destroy();
        }

        private static void ClearRootChildren()
        {
            if (_root == null) return;

            var transform = _root.transform;
            for (var i = transform.childCount - 1; i >= 0; i--)
            {
                UnityEngine.Object.Destroy(transform.GetChild(i).gameObject);
            }

            _panelRoot = null;
            _closeButtonRect = null;
        }

        private static void HandleEnterKeyFallback()
        {
            var enterDown = IsEnterKeyDown();
            if (enterDown && !_enterWasDown)
            {
                CloseWithSound();
                return;
            }

            _enterWasDown = enterDown;
        }

        private static void HandleMouseFallback()
        {
            var mouseDown = Input.GetMouseButton(0);
            if (Time.frameCount <= _ignoreMouseInputUntilFrame)
            {
                _mouseWasDown = mouseDown;
                return;
            }

            if (!mouseDown)
            {
                _mouseWasDown = false;
                return;
            }

            if (_mouseWasDown) return;

            _mouseWasDown = true;

            var mousePosition = Input.mousePosition;
            if (IsCloseButtonPoint(mousePosition))
            {
                CloseWithSound();
                return;
            }

            if (!IsPanelPoint(mousePosition))
            {
                CloseWithSound();
            }
        }

        private static bool IsCloseButtonPoint(Vector2 screenPoint)
        {
            return _closeButtonRect != null &&
                   _closeButtonRect.gameObject.activeInHierarchy &&
                   ContainsScreenPoint(_closeButtonRect, screenPoint, CloseButtonHitPadding);
        }

        private static bool IsPanelPoint(Vector2 screenPoint)
        {
            return _panelRoot != null &&
                   _panelRoot.gameObject.activeInHierarchy &&
                   ContainsScreenPoint(_panelRoot, screenPoint, 0f);
        }

        private static bool ContainsScreenPoint(RectTransform rect, Vector2 screenPoint, float padding)
        {
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            var minX = Mathf.Min(corners[0].x, corners[1].x, corners[2].x, corners[3].x) - padding;
            var maxX = Mathf.Max(corners[0].x, corners[1].x, corners[2].x, corners[3].x) + padding;
            var minY = Mathf.Min(corners[0].y, corners[1].y, corners[2].y, corners[3].y) - padding;
            var maxY = Mathf.Max(corners[0].y, corners[1].y, corners[2].y, corners[3].y) + padding;
            return screenPoint.x >= minX &&
                   screenPoint.x <= maxX &&
                   screenPoint.y >= minY &&
                   screenPoint.y <= maxY;
        }

        private static bool IsEnterKeyDown()
        {
            return Input.GetKey(KeyCode.Return) || Input.GetKey(KeyCode.KeypadEnter);
        }

        private static GameObject CreateRoot()
        {
            var root = new GameObject(RootName);
            UnityEngine.Object.DontDestroyOnLoad(root);

            var rootRect = root.AddComponent<RectTransform>();
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.offsetMin = Vector2.zero;
            rootRect.offsetMax = Vector2.zero;

            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.overrideSorting = true;
            canvas.sortingOrder = SortingOrder;

            var scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            root.AddComponent<GraphicRaycaster>();

            var shade = new GameObject("Shade");
            shade.transform.SetParent(root.transform, false);
            var shadeRect = shade.AddComponent<RectTransform>();
            shadeRect.anchorMin = Vector2.zero;
            shadeRect.anchorMax = Vector2.one;
            shadeRect.offsetMin = Vector2.zero;
            shadeRect.offsetMax = Vector2.zero;

            var shadeImage = shade.AddComponent<Image>();
            shadeImage.color = new Color(0f, 0f, 0f, 0.62f);
            shadeImage.raycastTarget = false;

            return root;
        }

        private static void CreateFallback(SettlementResultPush result)
        {
            var root = _root.transform;

            var shade = new GameObject("FallbackShade");
            shade.transform.SetParent(root, false);
            var shadeRect = shade.AddComponent<RectTransform>();
            shadeRect.anchorMin = Vector2.zero;
            shadeRect.anchorMax = Vector2.one;
            shadeRect.offsetMin = Vector2.zero;
            shadeRect.offsetMax = Vector2.zero;

            var shadeImage = shade.AddComponent<Image>();
            shadeImage.color = new Color(0f, 0f, 0f, 0.68f);
            shadeImage.raycastTarget = false;

            var panel = new GameObject("FallbackPanel");
            panel.transform.SetParent(root, false);
            var panelRect = panel.AddComponent<RectTransform>();
            _panelRoot = panelRect;
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = new Vector2(960f, 660f);
            panelRect.anchoredPosition = Vector2.zero;

            var panelImage = panel.AddComponent<Image>();
            panelImage.color = new Color(0.42f, 0.13f, 0.50f, 0.92f);
            panelImage.raycastTarget = true;

            var title = CreateText(panelRect, "FallbackTitle", I18nManager.T("settlement.title"), 46, TextAnchor.MiddleCenter);
            title.color = new Color(1f, 0.86f, 1f, 1f);
            title.fontStyle = FontStyle.Bold;
            var titleRect = title.rectTransform;
            titleRect.anchorMin = new Vector2(0f, 1f);
            titleRect.anchorMax = new Vector2(1f, 1f);
            titleRect.pivot = new Vector2(0.5f, 1f);
            titleRect.offsetMin = new Vector2(40f, -88f);
            titleRect.offsetMax = new Vector2(-40f, -26f);

            var content = CreateText(panelRect, "FallbackContent", BuildFallbackContent(result), 24, TextAnchor.UpperLeft);
            content.color = Color.white;
            content.lineSpacing = 1.18f;
            var contentRect = content.rectTransform;
            contentRect.anchorMin = new Vector2(0f, 0f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.offsetMin = new Vector2(56f, 112f);
            contentRect.offsetMax = new Vector2(-56f, -112f);

            CreateCloseButton(panelRect);
        }

        private static RectTransform CreatePanel(Transform root)
        {
            var panelRoot = new GameObject("PanelRoot");
            panelRoot.transform.SetParent(root, false);
            _panelRoot = panelRoot.AddComponent<RectTransform>();
            _panelRoot.anchorMin = new Vector2(0.5f, 0.5f);
            _panelRoot.anchorMax = new Vector2(0.5f, 0.5f);
            _panelRoot.pivot = new Vector2(0.5f, 0.5f);
            _panelRoot.sizeDelta = new Vector2(PanelWidth + 36f, PanelHeight + 36f);
            _panelRoot.anchoredPosition = Vector2.zero;

            var shadow = CreateImage(_panelRoot, "PanelShadow", new Color(0.05f, 0f, 0.10f, 0.40f));
            shadow.anchorMin = new Vector2(0.5f, 0.5f);
            shadow.anchorMax = new Vector2(0.5f, 0.5f);
            shadow.pivot = new Vector2(0.5f, 0.5f);
            shadow.sizeDelta = new Vector2(PanelWidth + 36f, PanelHeight + 36f);
            shadow.anchoredPosition = new Vector2(0f, -10f);

            var panel = new GameObject("Panel");
            panel.transform.SetParent(_panelRoot, false);
            var panelRect = panel.AddComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = new Vector2(PanelWidth, PanelHeight);
            panelRect.anchoredPosition = Vector2.zero;

            var panelImage = panel.AddComponent<Image>();
            panelImage.sprite = GetRoundedSprite();
            panelImage.type = panelImage.sprite != null ? Image.Type.Sliced : Image.Type.Simple;
            panelImage.color = new Color(0.58f, 0.22f, 0.70f, 0.42f);
            panelImage.raycastTarget = true;

            var border = CreateImage(panelRect, "InnerBorder", new Color(1f, 0.76f, 0.98f, 0.23f));
            border.anchorMin = Vector2.zero;
            border.anchorMax = Vector2.one;
            border.offsetMin = Vector2.zero;
            border.offsetMax = Vector2.zero;

            var glass = CreateImage(panelRect, "GlassLayer", new Color(1f, 0.88f, 1f, 0.08f));
            glass.anchorMin = Vector2.zero;
            glass.anchorMax = Vector2.one;
            glass.offsetMin = new Vector2(10f, 10f);
            glass.offsetMax = new Vector2(-10f, -10f);

            var highlight = CreateImage(panelRect, "HeaderSheen", new Color(1f, 0.74f, 0.98f, 0.18f));
            highlight.anchorMin = new Vector2(0f, 1f);
            highlight.anchorMax = new Vector2(1f, 1f);
            highlight.pivot = new Vector2(0.5f, 1f);
            highlight.offsetMin = new Vector2(20f, -150f);
            highlight.offsetMax = new Vector2(-20f, -18f);

            return panelRect;
        }

        private static void CreateHeader(RectTransform parent)
        {
            var title = CreateText(parent, "Title", I18nManager.T("settlement.title"), 50, TextAnchor.MiddleCenter);
            title.color = new Color(1f, 0.86f, 1f, 1f);
            title.fontStyle = FontStyle.Bold;

            var titleRect = title.rectTransform;
            titleRect.anchorMin = new Vector2(0.5f, 1f);
            titleRect.anchorMax = new Vector2(0.5f, 1f);
            titleRect.pivot = new Vector2(0.5f, 1f);
            titleRect.sizeDelta = new Vector2(420f, 62f);
            titleRect.anchoredPosition = new Vector2(0f, -30f);

            var outline = title.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0.27f, 0.05f, 0.36f, 0.82f);
            outline.effectDistance = new Vector2(2f, -2f);

            var subtitle = CreateText(parent, "Subtitle", I18nManager.T("settlement.subtitle"), 18, TextAnchor.MiddleCenter);
            subtitle.color = new Color(1f, 0.70f, 0.96f, 0.88f);
            subtitle.fontStyle = FontStyle.Bold;
            var subtitleRect = subtitle.rectTransform;
            subtitleRect.anchorMin = new Vector2(0.5f, 1f);
            subtitleRect.anchorMax = new Vector2(0.5f, 1f);
            subtitleRect.pivot = new Vector2(0.5f, 1f);
            subtitleRect.sizeDelta = new Vector2(420f, 30f);
            subtitleRect.anchoredPosition = new Vector2(0f, -88f);
        }

        private static void CreateAwardCards(RectTransform parent, SettlementResultPush result)
        {
            var area = new GameObject("Awards");
            area.transform.SetParent(parent, false);
            var areaRect = area.AddComponent<RectTransform>();
            areaRect.anchorMin = new Vector2(0.5f, 1f);
            areaRect.anchorMax = new Vector2(0.5f, 1f);
            areaRect.pivot = new Vector2(0.5f, 1f);
            areaRect.sizeDelta = new Vector2(PanelWidth - SidePadding * 2f, AwardCardHeight);
            areaRect.anchoredPosition = new Vector2(0f, -138f);

            var gap = 14f;
            var x = 0f;
            CreateAwardCard(areaRect, x, I18nManager.T("settlement.award.dragon_coin"), result?.DragonCoinUids, result, "ffd700ff");
            x += AwardCardWidth + gap;
            CreateAwardCard(areaRect, x, I18nManager.T("settlement.award.combo"), result?.ComboUids, result, Constants.ColorBlue);
            x += AwardCardWidth + gap;
            CreateAwardCard(areaRect, x, I18nManager.T("settlement.award.perfect"), result?.PerfectUids, result, Constants.ColorPink);
            x += AwardCardWidth + gap;
            CreateAwardCard(areaRect, x, I18nManager.T("settlement.award.sleepwalk"), result?.SleepwalkUids, result, "ff9f1aff");
        }

        private static void CreateAwardCard(RectTransform parent, float x, string titleText, string[] uids, SettlementResultPush result, string accentColor)
        {
            var card = CreateImage(parent, titleText, new Color(0.68f, 0.25f, 0.78f, 0.24f), true);
            card.anchorMin = new Vector2(0f, 1f);
            card.anchorMax = new Vector2(0f, 1f);
            card.pivot = new Vector2(0f, 1f);
            card.sizeDelta = new Vector2(AwardCardWidth, AwardCardHeight);
            card.anchoredPosition = new Vector2(x, 0f);

            var topLine = CreateImage(card, "Accent", RichTextColorToUnityColor(accentColor, 0.74f));
            topLine.anchorMin = new Vector2(0f, 1f);
            topLine.anchorMax = new Vector2(1f, 1f);
            topLine.pivot = new Vector2(0.5f, 1f);
            topLine.offsetMin = new Vector2(18f, -5f);
            topLine.offsetMax = new Vector2(-18f, 0f);

            var title = CreateText(card, "Title", ColorLabel(titleText, accentColor), 23, TextAnchor.UpperLeft);
            title.fontStyle = FontStyle.Bold;
            var titleRect = title.rectTransform;
            titleRect.anchorMin = new Vector2(0f, 1f);
            titleRect.anchorMax = new Vector2(1f, 1f);
            titleRect.pivot = new Vector2(0.5f, 1f);
            titleRect.offsetMin = new Vector2(20f, -52f);
            titleRect.offsetMax = new Vector2(-20f, -18f);

            CreateAwardNameRows(card, uids, result);
        }

        private static void CreateAwardNameRows(RectTransform parent, string[] uids, SettlementResultPush result)
        {
            var uniqueUids = (uids ?? Array.Empty<string>())
                .Where(uid => !string.IsNullOrWhiteSpace(uid))
                .Distinct()
                .ToArray();

            var contentRect = CreateAwardNameViewport(parent, uniqueUids.Length);

            if (uniqueUids.Length == 0)
            {
                var empty = CreateText(contentRect, "NamesEmpty", I18nManager.T("settlement.empty"), 21, TextAnchor.MiddleLeft);
                empty.color = new Color(1f, 0.96f, 1f, 0.78f);
                var emptyRect = empty.rectTransform;
                emptyRect.anchorMin = Vector2.zero;
                emptyRect.anchorMax = Vector2.one;
                emptyRect.offsetMin = Vector2.zero;
                emptyRect.offsetMax = Vector2.zero;
                return;
            }

            for (var i = 0; i < uniqueUids.Length; i++)
            {
                CreateAwardNameRow(contentRect, uniqueUids[i], result, i);
            }
        }

        private static RectTransform CreateAwardNameViewport(RectTransform parent, int rowCount)
        {
            var viewport = new GameObject("NamesViewport");
            viewport.transform.SetParent(parent, false);
            var viewportRect = viewport.AddComponent<RectTransform>();
            viewportRect.anchorMin = new Vector2(0f, 0f);
            viewportRect.anchorMax = new Vector2(1f, 1f);
            viewportRect.offsetMin = new Vector2(16f, 14f);
            viewportRect.offsetMax = new Vector2(-16f, -62f);

            var viewportImage = viewport.AddComponent<Image>();
            viewportImage.color = new Color(1f, 1f, 1f, 0f);
            viewportImage.raycastTarget = rowCount * AwardNameRowHeight > AwardNamesViewportHeight;

            var mask = viewport.AddComponent<Mask>();
            mask.showMaskGraphic = false;

            var content = new GameObject("NamesContent");
            content.transform.SetParent(viewportRect, false);
            var contentRect = content.AddComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.pivot = new Vector2(0.5f, 1f);
            contentRect.offsetMin = Vector2.zero;
            contentRect.offsetMax = Vector2.zero;
            contentRect.sizeDelta = new Vector2(0f, Mathf.Max(AwardNamesViewportHeight, Math.Max(1, rowCount) * AwardNameRowHeight));

            var scroll = viewport.AddComponent<ScrollRect>();
            scroll.content = contentRect;
            scroll.viewport = viewportRect;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 18f;
            return contentRect;
        }

        private static void CreateAwardNameRow(RectTransform parent, string uid, SettlementResultPush result, int index)
        {
            var row = new GameObject("NameRow_" + index);
            row.transform.SetParent(parent, false);
            var rowRect = row.AddComponent<RectTransform>();
            rowRect.anchorMin = new Vector2(0f, 1f);
            rowRect.anchorMax = new Vector2(1f, 1f);
            rowRect.pivot = new Vector2(0f, 1f);
            rowRect.offsetMin = new Vector2(0f, -AwardNameRowHeight * (index + 1));
            rowRect.offsetMax = new Vector2(0f, -AwardNameRowHeight * index);

            var name = CreateText(rowRect, "Name", EscapeRichText(GetPlayerName(uid, result)), 20, TextAnchor.MiddleLeft);
            name.color = new Color(1f, 0.96f, 1f, 0.96f);
            var nameRect = name.rectTransform;
            nameRect.anchorMin = Vector2.zero;
            nameRect.anchorMax = Vector2.one;
            nameRect.offsetMin = Vector2.zero;
            nameRect.offsetMax = Vector2.zero;
        }

        private static void CreatePlayedCharts(RectTransform parent, SettlementResultPush result)
        {
            var section = CreateImage(parent, "PlayedCharts", new Color(0.24f, 0.08f, 0.30f, 0.34f), true);
            section.anchorMin = new Vector2(0.5f, 1f);
            section.anchorMax = new Vector2(0.5f, 1f);
            section.pivot = new Vector2(0.5f, 1f);
            section.sizeDelta = new Vector2(PanelWidth - SidePadding * 2f, 382f);
            section.anchoredPosition = new Vector2(0f, -322f);

            var title = CreateText(section, "ChartsTitle", ColorLabel(I18nManager.T("settlement.played_charts"), Constants.ColorBlue), 28, TextAnchor.MiddleLeft);
            title.fontStyle = FontStyle.Bold;
            var titleRect = title.rectTransform;
            titleRect.anchorMin = new Vector2(0f, 1f);
            titleRect.anchorMax = new Vector2(1f, 1f);
            titleRect.pivot = new Vector2(0.5f, 1f);
            titleRect.offsetMin = new Vector2(30f, -62f);
            titleRect.offsetMax = new Vector2(-30f, -18f);

            var total = CreateText(
                section,
                "Duration",
                $"{ColorLabel(I18nManager.T("settlement.total_duration"), Constants.ColorCyan)}: {ColorLabel(GetTotalDurationText(result?.PlayedCharts), "ffffffff")}",
                23,
                TextAnchor.MiddleRight);
            var totalRect = total.rectTransform;
            totalRect.anchorMin = new Vector2(0f, 1f);
            totalRect.anchorMax = new Vector2(1f, 1f);
            totalRect.pivot = new Vector2(0.5f, 1f);
            totalRect.offsetMin = new Vector2(30f, -62f);
            totalRect.offsetMax = new Vector2(-30f, -18f);

            var divider = CreateImage(section, "Divider", new Color(1f, 0.78f, 1f, 0.18f));
            divider.anchorMin = new Vector2(0f, 1f);
            divider.anchorMax = new Vector2(1f, 1f);
            divider.pivot = new Vector2(0.5f, 1f);
            divider.offsetMin = new Vector2(30f, -72f);
            divider.offsetMax = new Vector2(-30f, -69f);

            CreateChartScroll(section, result?.PlayedCharts);
        }

        private static void CreateChartScroll(RectTransform parent, SettlementChartEntry[] charts)
        {
            var viewport = new GameObject("ChartViewport");
            viewport.transform.SetParent(parent, false);
            var viewportRect = viewport.AddComponent<RectTransform>();
            viewportRect.anchorMin = new Vector2(0f, 1f);
            viewportRect.anchorMax = new Vector2(1f, 1f);
            viewportRect.pivot = new Vector2(0.5f, 1f);
            viewportRect.offsetMin = new Vector2(30f, -72f - ChartViewportHeight);
            viewportRect.offsetMax = new Vector2(-30f, -86f);

            var viewportImage = viewport.AddComponent<Image>();
            viewportImage.sprite = GetRoundedSprite();
            viewportImage.type = viewportImage.sprite != null ? Image.Type.Sliced : Image.Type.Simple;
            viewportImage.color = new Color(1f, 0.86f, 1f, 0.06f);
            viewportImage.raycastTarget = true;

            var mask = viewport.AddComponent<Mask>();
            mask.showMaskGraphic = true;

            var contentRoot = new GameObject("ChartContent");
            contentRoot.transform.SetParent(viewportRect, false);
            var contentRect = contentRoot.AddComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.pivot = new Vector2(0.5f, 1f);
            contentRect.offsetMin = new Vector2(24f, 0f);
            contentRect.offsetMax = new Vector2(-24f, 0f);

            var content = CreateText(contentRect, "ChartText", BuildChartList(charts), 23, TextAnchor.UpperLeft);
            content.color = Color.white;
            content.lineSpacing = 1.22f;

            var textRect = content.rectTransform;
            textRect.anchorMin = new Vector2(0f, 1f);
            textRect.anchorMax = new Vector2(1f, 1f);
            textRect.pivot = new Vector2(0.5f, 1f);
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            Canvas.ForceUpdateCanvases();
            var contentHeight = Mathf.Max(ChartViewportHeight, content.preferredHeight + 28f);
            contentRect.sizeDelta = new Vector2(0f, contentHeight);
            textRect.sizeDelta = new Vector2(0f, contentHeight);

            var scroll = viewport.AddComponent<ScrollRect>();
            scroll.content = contentRect;
            scroll.viewport = viewportRect;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 26f;
        }

        private static void CreateCloseButton(RectTransform parent)
        {
            var buttonObj = new GameObject("CloseButton");
            buttonObj.transform.SetParent(parent, false);

            var rect = buttonObj.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.sizeDelta = new Vector2(220f, 56f);
            rect.anchoredPosition = new Vector2(0f, 30f);
            _closeButtonRect = rect;

            var image = buttonObj.AddComponent<Image>();
            image.sprite = GetRoundedSprite();
            image.type = image.sprite != null ? Image.Type.Sliced : Image.Type.Simple;
            image.color = new Color(1f, 0.46f, 0.86f, 0.92f);
            image.raycastTarget = true;

            var button = buttonObj.AddComponent<Button>();
            button.targetGraphic = image;
            button.colors = new ColorBlock
            {
                normalColor = new Color(1f, 0.46f, 0.86f, 0.92f),
                highlightedColor = new Color(1f, 0.62f, 0.94f, 1f),
                pressedColor = new Color(0.78f, 0.26f, 0.74f, 1f),
                selectedColor = new Color(1f, 0.54f, 0.90f, 0.96f),
                disabledColor = new Color(0.45f, 0.30f, 0.48f, 0.72f),
                colorMultiplier = 1f,
                fadeDuration = 0.08f
            };
            button.onClick.AddListener((UnityAction)new Action(CloseWithSound));

            var label = CreateText(rect, "Label", I18nManager.T("settlement.confirm"), 27, TextAnchor.MiddleCenter);
            label.color = Color.white;
            label.fontStyle = FontStyle.Bold;
            var labelRect = label.rectTransform;
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;
        }

        private static Text CreateText(Transform parent, string name, string value, int fontSize, TextAnchor alignment)
        {
            var obj = new GameObject(name);
            obj.transform.SetParent(parent, false);
            var text = obj.AddComponent<Text>();

            text.font = GetGameFont();
            text.text = value;
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.supportRichText = true;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            return text;
        }

        private static RectTransform CreateImage(Transform parent, string name, Color color, bool raycastTarget = false)
        {
            var obj = new GameObject(name);
            obj.transform.SetParent(parent, false);
            var rect = obj.AddComponent<RectTransform>();
            var image = obj.AddComponent<Image>();
            image.sprite = GetRoundedSprite();
            image.type = image.sprite != null ? Image.Type.Sliced : Image.Type.Simple;
            image.color = color;
            image.raycastTarget = raycastTarget;
            return rect;
        }

        private static Font GetGameFont()
        {
            if (_cachedFont != null) return _cachedFont;

            var texts = UnityEngine.Resources.FindObjectsOfTypeAll<Text>();
            foreach (var text in texts)
            {
                if (text != null && text.font != null)
                {
                    _cachedFont = text.font;
                    return _cachedFont;
                }
            }

            _cachedFont = UnityEngine.Resources.GetBuiltinResource<Font>("Arial.ttf");
            return _cachedFont;
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

        private static string GetTotalDurationText(SettlementChartEntry[] charts)
        {
            if (charts == null || charts.Length == 0)
            {
                return "--:--";
            }

            var totalSeconds = 0f;
            var knownDurationCount = 0;

            foreach (var chart in charts)
            {
                var durationSeconds = GetDurationSeconds(chart);
                if (durationSeconds.HasValue)
                {
                    totalSeconds += durationSeconds.Value;
                    knownDurationCount++;
                }
            }

            return knownDurationCount > 0 ? FormatDuration(totalSeconds) : "--:--";
        }

        private static string BuildChartList(SettlementChartEntry[] charts)
        {
            if (charts == null || charts.Length == 0)
            {
                return I18nManager.T("settlement.empty");
            }

            var lines = new List<string>();
            for (var i = 0; i < charts.Length; i++)
            {
                var chart = charts[i];
                var name = EscapeRichText(string.IsNullOrWhiteSpace(chart?.ChartName)
                    ? I18nManager.Tf("chart.unknown_with_difficulty", chart?.Difficulty ?? 0)
                    : chart.ChartName);
                lines.Add($"{ColorLabel((i + 1).ToString("00"), Constants.ColorYellow)}  {ColorLabel(name, "ffffffff")}  {FormatDifficulty(chart?.Difficulty ?? 0)}");
            }

            return string.Join("\n", lines);
        }

        private static string BuildFallbackContent(SettlementResultPush result)
        {
            return $"{ColorLabel(I18nManager.T("settlement.award.dragon_coin"), "ffd700ff")}: {FormatNames(result?.DragonCoinUids, result)}\n" +
                   $"{ColorLabel(I18nManager.T("settlement.award.combo"), Constants.ColorBlue)}: {FormatNames(result?.ComboUids, result)}\n" +
                   $"{ColorLabel(I18nManager.T("settlement.award.perfect"), Constants.ColorPink)}: {FormatNames(result?.PerfectUids, result)}\n" +
                   $"{ColorLabel(I18nManager.T("settlement.award.sleepwalk"), "ff9f1aff")}: {FormatNames(result?.SleepwalkUids, result)}\n\n" +
                   $"{ColorLabel(I18nManager.T("settlement.total_duration"), Constants.ColorCyan)}: {ColorLabel(GetTotalDurationText(result?.PlayedCharts), "ffffffff")}\n" +
                   $"{ColorLabel(I18nManager.T("settlement.played_charts"), Constants.ColorBlue)}:\n" +
                   BuildChartList(result?.PlayedCharts);
        }

        private static float? GetDurationSeconds(SettlementChartEntry chart)
        {
            if (chart == null) return null;
            if (chart.DurationSeconds > 0f) return chart.DurationSeconds;
            return null;
        }

        private static string FormatDuration(float seconds)
        {
            var totalSeconds = Math.Max(0, (int)Math.Round(seconds));
            var minutes = totalSeconds / 60;
            var remainder = totalSeconds % 60;
            return $"{minutes:00}:{remainder:00}";
        }

        private static string FormatDifficulty(int difficulty)
        {
            return LobbyRuleTextFormatter.FormatDifficulty(difficulty, false);
        }

        private static string ColorLabel(string value, string color)
        {
            return $"<color=#{NormalizeRichTextColor(color)}>{value}</color>";
        }

        private static Color RichTextColorToUnityColor(string color, float alpha)
        {
            var normalized = NormalizeRichTextColor(color);
            if (normalized.Length >= 6 &&
                byte.TryParse(normalized.Substring(0, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var r) &&
                byte.TryParse(normalized.Substring(2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var g) &&
                byte.TryParse(normalized.Substring(4, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var b))
            {
                return new Color(r / 255f, g / 255f, b / 255f, alpha);
            }

            return new Color(1f, 0.55f, 0.90f, alpha);
        }

        private static string FormatNames(string[] uids, SettlementResultPush result)
        {
            if (uids == null || uids.Length == 0) return I18nManager.T("settlement.empty");

            var names = new System.Collections.Generic.List<string>();
            foreach (var uid in uids)
            {
                var name = EscapeRichText(GetPlayerName(uid, result));
                if (!string.IsNullOrWhiteSpace(name))
                {
                    names.Add($"<color=#ffffffff>{name}</color>");
                }
            }

            return names.Count == 0 ? I18nManager.T("settlement.empty") : string.Join(I18nManager.T("settlement.name_separator"), names);
        }

        private static string GetPlayerName(string uid, SettlementResultPush result)
        {
            if (result?.PlayerNames != null)
            {
                foreach (var player in result.PlayerNames)
                {
                    if (player?.Uid == uid && !string.IsNullOrWhiteSpace(player.Name))
                    {
                        return player.Name;
                    }
                }
            }

            var lobby = LobbyManager.CurrentLobby;
            if (lobby?.PlayerDetails != null)
            {
                foreach (var player in lobby.PlayerDetails)
                {
                    if (player?.Uid == uid && !string.IsNullOrWhiteSpace(player.Name))
                    {
                        return player.Name;
                    }
                }
            }

            return uid ?? I18nManager.T("common.unknown");
        }

        private static string EscapeRichText(string value)
        {
            return string.IsNullOrEmpty(value)
                ? string.Empty
                : value.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
        }

        private static string NormalizeRichTextColor(string color)
        {
            if (string.IsNullOrWhiteSpace(color)) return "ffffffff";
            return color.Trim().TrimStart('#');
        }

    }
}
