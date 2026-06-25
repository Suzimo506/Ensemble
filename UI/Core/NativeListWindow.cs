using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using PeroInputManager = Il2CppAssets.Scripts.PeroTools.Managers.InputManager;

namespace MDEN.UI.Core
{
    /// <summary>
    /// Native Unity UI replacement for PopupLib.UI.Windows.ForumWindow.
    /// Builds a full-screen modal with a centered panel containing a scrollable list
    /// (left ~55%) and a description area (right ~45%). All visuals use
    /// <see cref="DesignTokens"/> and <see cref="UiFactory"/>.
    /// </summary>
    public sealed class NativeListWindow : INativeListWindow, INativeBaseWindow
    {
        private const float HeaderHeight = 60f;
        private const float CloseButtonSize = 40f;
        private const float CloseButtonMargin = 12f;
        private const float ItemVisualHeight = DesignTokens.ListItemHeight - DesignTokens.SpacingXs;
        private const float ItemTopPadding = DesignTokens.SpacingSm;
        private const float TitleLeftPadding = DesignTokens.SpacingMd;

        // Layout derived from DesignTokens
        private static readonly float BodyTop = HeaderHeight + DesignTokens.SpacingSm;
        private static readonly float BodyHeight = DesignTokens.PanelHeight - BodyTop - DesignTokens.SpacingLg;
        private static readonly float ListWidth = (DesignTokens.PanelWidth - DesignTokens.SpacingLg * 2 - DesignTokens.SpacingMd) * 0.55f;
        private static readonly float DescX = DesignTokens.SpacingLg + ListWidth + DesignTokens.SpacingMd;
        private static readonly float DescWidth = DesignTokens.PanelWidth - DescX - DesignTokens.SpacingLg;

        private GameObject _root;
        private RectTransform _panel;
        private RectTransform _listContent;
        private RectTransform _descContent;
        private Text _descriptionText;
        private List<ItemRow> _itemRows;
        private int _selectedIndex = -1;

        /// <summary>
        /// Kept for API compatibility. Selection highlight and description now always
        /// persist after a click — the two-click pattern is handled by subscribing
        /// windows via their own _lastSelectedIndex tracking.
        /// </summary>
        public bool AutoReset { get; set; }

        /// <summary>
        /// The list of items to display. Starts empty; windows call Items.Add(...) before Show().
        /// Replaces ForumWindow.ForumObjects.
        /// </summary>
        public List<NativeListItem> Items { get; }

        /// <summary>True when the window is shown and active.</summary>
        public bool Activated { get; private set; }

        /// <summary>
        /// Window title displayed in the header bar. Set before calling Show().
        /// Replaces the old OnInternalShowInjectTitle pattern.
        /// </summary>
        public string Title { get; set; }

        /// <summary>Replaces ForumWindow.OnSelectionChanged. Fires on every item click.</summary>
        public event Action<INativeListWindow, int> OnSelectionChanged;

        /// <summary>Replaces ForumWindow.OnInternalShow. Fires after Show() completes.</summary>
        public event Action<INativeBaseWindow> OnInternalShow;

        /// <summary>Replaces ForumWindow.OnCompletion. Fires when the user closes the window.</summary>
        public event Action<INativeBaseWindow> OnCompletion;

        public NativeListWindow()
        {
            Items = new List<NativeListItem>();
            Title = string.Empty;
        }

        // ─────────────────────────────────────────────────────────────────────────────
        //  Public API
        // ─────────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Builds the entire UI, sets Activated=true, blocks native input,
        /// and fires OnInternalShow.
        /// </summary>
        public void Show()
        {
            if (_root != null) return;

            _root = UiFactory.CreateRootCanvas("MDENListWindowRoot", DesignTokens.WindowSortingOrder);

            CreateShade(_root.transform);
            _panel = CreatePanel(_root.transform);
            CreateHeader(_panel);
            CreateBody(_panel);
            RebuildListItems();

            _selectedIndex = -1;
            UpdateDescription();

            Activated = true;
            BlockNativeInput();
            OnInternalShow?.Invoke(this);
        }

        /// <summary>
        /// Destroys the root GameObject, nulls all cached fields, sets Activated=false,
        /// and restores native input. Does NOT fire OnCompletion. Idempotent.
        /// </summary>
        public void ForceClose()
        {
            if (_root == null) return;

            RestoreNativeInput();

            UnityEngine.Object.Destroy(_root);
            _root = null;
            _panel = null;
            _listContent = null;
            _descContent = null;
            _descriptionText = null;
            _itemRows = null;
            _selectedIndex = -1;
            Activated = false;
        }

        /// <summary>
        /// Updates each list item's title Text and the description area
        /// from the current <see cref="Items"/> data — without rebuilding the window.
        /// Used by TenziDraw animation: items' Title/Content are mutated
        /// externally, then RefreshItems() pushes changes to the visual components.
        /// </summary>
        public void RefreshItems()
        {
            if (!Activated || _itemRows == null) return;

            for (var i = 0; i < _itemRows.Count && i < Items.Count; i++)
            {
                var row = _itemRows[i];
                var item = Items[i];
                if (row == null || item == null) continue;

                if (row.Title != null)
                {
                    row.Title.text = item.Title ?? string.Empty;
                }
            }

            UpdateDescription();
        }

        // ─────────────────────────────────────────────────────────────────────────────
        //  UI Construction
        // ─────────────────────────────────────────────────────────────────────────────

        private static void CreateShade(Transform parent)
        {
            var shade = UiFactory.CreateImage(parent, "Shade", DesignTokens.Shade, true);
            UiFactory.SetStretch(shade, 0f, 0f, 0f, 0f);
        }

        private static RectTransform CreatePanel(Transform parent)
        {
            var panel = UiFactory.CreateImage(parent, "Panel", DesignTokens.Surface, true);
            panel.anchorMin = new Vector2(0.5f, 0.5f);
            panel.anchorMax = new Vector2(0.5f, 0.5f);
            panel.pivot = new Vector2(0.5f, 0.5f);
            panel.sizeDelta = new Vector2(DesignTokens.PanelWidth, DesignTokens.PanelHeight);
            return panel;
        }

        private void CreateHeader(RectTransform panel)
        {
            var header = UiFactory.CreateImage(panel, "Header", DesignTokens.SurfaceBright, true);
            UiFactory.SetFixedTop(header, 0f, 0f, DesignTokens.PanelWidth, HeaderHeight);

            var title = UiFactory.CreateText(header, "Title", Title ?? string.Empty,
                DesignTokens.HeadlineMd, TextAnchor.MiddleCenter);
            title.color = DesignTokens.OnSurface;
            title.fontStyle = FontStyle.Bold;
            title.horizontalOverflow = HorizontalWrapMode.Wrap;
            title.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            title.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            title.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            title.rectTransform.sizeDelta = new Vector2(DesignTokens.PanelWidth - CloseButtonSize - CloseButtonMargin * 3, HeaderHeight);
            title.rectTransform.anchoredPosition = Vector2.zero;

            CreateCloseButton(header);
        }

        private void CreateCloseButton(RectTransform header)
        {
            var rect = UiFactory.CreateImage(header, "CloseButton", new Color(0f, 0f, 0f, 0f), true);
            rect.anchorMin = new Vector2(1f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 1f);
            rect.sizeDelta = new Vector2(CloseButtonSize, CloseButtonSize);
            rect.anchoredPosition = new Vector2(-CloseButtonMargin, -DesignTokens.SpacingXs);

            var image = rect.GetComponent<Image>();
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.transition = Selectable.Transition.ColorTint;
            button.colors = new ColorBlock
            {
                normalColor = new Color(0f, 0f, 0f, 0f),
                highlightedColor = DesignTokens.SurfaceContainerHigh,
                pressedColor = DesignTokens.SurfaceContainerHighest,
                selectedColor = new Color(0f, 0f, 0f, 0f),
                disabledColor = new Color(0.35f, 0.30f, 0.40f, 0.7f),
                colorMultiplier = 1f,
                fadeDuration = 0.03f
            };
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            button.onClick.AddListener((UnityAction)(() =>
            {
                UiFactory.ClearSelectedObject();
                OnCompletion?.Invoke(this);
                ForceClose();
            }));

            var label = UiFactory.CreateText(rect, "Label", "✕", DesignTokens.BodyLg, TextAnchor.MiddleCenter);
            label.color = DesignTokens.OnSurface;
            label.fontStyle = FontStyle.Bold;
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            UiFactory.SetStretch(label.rectTransform, 0f, 0f, 0f, 0f);
        }

        private void CreateBody(RectTransform panel)
        {
            // Left: scrollable list
            var listViewport = UiFactory.CreateScrollViewport(panel, "ListViewport",
                DesignTokens.SpacingLg, BodyTop, ListWidth, BodyHeight, false);
            _listContent = UiFactory.CreateContent(listViewport, true);

            // Right: scrollable description area
            var descViewport = UiFactory.CreateScrollViewport(panel, "DescriptionViewport",
                DescX, BodyTop, DescWidth, BodyHeight, false);
            _descContent = UiFactory.CreateContent(descViewport, true);

            _descriptionText = UiFactory.CreateText(_descContent, "Description", string.Empty,
                DesignTokens.BodyMd, TextAnchor.UpperLeft);
            _descriptionText.color = DesignTokens.OnSurface;
            _descriptionText.lineSpacing = 1.5f;
            UiFactory.SetStretch(_descriptionText.rectTransform,
                DesignTokens.SpacingMd, DesignTokens.SpacingMd,
                DesignTokens.SpacingMd, DesignTokens.SpacingMd);
        }

        private void RebuildListItems()
        {
            if (_listContent == null) return;

            ClearChildren(_listContent);
            _itemRows = new List<ItemRow>(Items.Count);

            for (var i = 0; i < Items.Count; i++)
            {
                var row = CreateListItem(_listContent, i, Items[i]);
                _itemRows.Add(row);
            }

            var totalHeight = ItemTopPadding * 2 + Items.Count * DesignTokens.ListItemHeight;
            _listContent.sizeDelta = new Vector2(0f, Mathf.Max(0f, totalHeight));
        }

        private ItemRow CreateListItem(Transform parent, int index, NativeListItem item)
        {
            var row = new ItemRow();

            var rect = UiFactory.CreateImage(parent, "Item_" + index,
                DesignTokens.SurfaceContainer, true);
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.sizeDelta = new Vector2(0f, ItemVisualHeight);
            rect.anchoredPosition = new Vector2(0f, -ItemTopPadding - index * DesignTokens.ListItemHeight);
            row.Rect = rect;
            row.Background = rect.GetComponent<Image>();

            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = row.Background;
            button.transition = Selectable.Transition.ColorTint;
            button.colors = UiFactory.CreateButtonColors(DesignTokens.SurfaceContainer);
            button.navigation = new Navigation { mode = Navigation.Mode.None };

            var capturedIndex = index;
            button.onClick.AddListener((UnityAction)(() =>
            {
                UiFactory.ClearSelectedObject();
                HandleItemClick(capturedIndex);
            }));
            row.Button = button;

            // Title text (full width, no thumbnail)
            var title = UiFactory.CreateText(rect, "Title", item?.Title ?? string.Empty,
                DesignTokens.BodyMd, TextAnchor.MiddleLeft);
            title.color = DesignTokens.OnSurface;
            title.verticalOverflow = VerticalWrapMode.Truncate;
            UiFactory.SetStretch(title.rectTransform, TitleLeftPadding, 0f, DesignTokens.SpacingSm, 0f);
            row.Title = title;

            return row;
        }

        // ─────────────────────────────────────────────────────────────────────────────
        //  Selection & Visual Updates
        // ─────────────────────────────────────────────────────────────────────────────

        private void HandleItemClick(int index)
        {
            if (index < 0 || index >= Items.Count) return;

            _selectedIndex = index;
            UpdateSelectionVisuals();
            UpdateDescription();

            OnSelectionChanged?.Invoke(this, index);
        }

        private void UpdateSelectionVisuals()
        {
            if (_itemRows == null) return;

            for (var i = 0; i < _itemRows.Count; i++)
            {
                var row = _itemRows[i];
                if (row == null || row.Background == null) continue;

                var selected = i == _selectedIndex;
                var bgColor = selected ? DesignTokens.PrimaryContainer : DesignTokens.SurfaceContainer;
                row.Background.color = bgColor;
                row.Button.colors = UiFactory.CreateButtonColors(bgColor);

                if (row.Title != null)
                {
                    row.Title.color = selected ? DesignTokens.Primary : DesignTokens.OnSurface;
                }
            }
        }

        private void UpdateDescription()
        {
            if (_descriptionText == null) return;

            if (_selectedIndex >= 0 && _selectedIndex < Items.Count)
            {
                _descriptionText.text = Items[_selectedIndex].Content ?? string.Empty;
            }
            else
            {
                _descriptionText.text = "请从左侧列表选择一项";
            }

            // Resize scroll content to fit text so the ScrollRect can scroll
            if (_descContent != null)
            {
                var textHeight = _descriptionText.preferredHeight;
                _descContent.sizeDelta = new Vector2(0f, textHeight + DesignTokens.SpacingMd * 2);
            }
        }

        // ─────────────────────────────────────────────────────────────────────────────
        //  Native Input Blocking
        // ─────────────────────────────────────────────────────────────────────────────

        private static void BlockNativeInput()
        {
            try
            {
                if (PeroInputManager.instance != null)
                {
                    PeroInputManager.instance.isStopKeyAction = true;
                }
            }
            catch
            {
                // Native input manager may be unavailable during scene transitions.
            }
        }

        private static void RestoreNativeInput()
        {
            try
            {
                if (PeroInputManager.instance != null)
                {
                    PeroInputManager.instance.isStopKeyAction = false;
                }
            }
            catch
            {
                // Native input manager may be unavailable during scene transitions.
            }
        }

        // ─────────────────────────────────────────────────────────────────────────────
        //  Utilities
        // ─────────────────────────────────────────────────────────────────────────────

        private static void ClearChildren(Transform parent)
        {
            if (parent == null) return;
            for (var i = parent.childCount - 1; i >= 0; i--)
            {
                UnityEngine.Object.Destroy(parent.GetChild(i).gameObject);
            }
        }

        /// <summary>
        /// Cached references to a single list item's visual components,
        /// used for efficient updates in <see cref="RefreshItems"/> and
        /// <see cref="UpdateSelectionVisuals"/>.
        /// </summary>
        private sealed class ItemRow
        {
            public RectTransform Rect;
            public Image Background;
            public Text Title;
            public Button Button;
        }
    }
}
