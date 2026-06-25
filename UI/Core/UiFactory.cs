using System;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using UnityEngine.UI;

namespace MDEN.UI.Core
{
    /// <summary>
    /// Static helper class with reusable Unity UI construction methods.
    /// Ported and adapted from CustomAlbums' LibraryWindow.cs.
    /// All methods use <see cref="DesignTokens"/> for colors/sizes and
    /// <see cref="NativeFontCache"/> for font resolution.
    /// </summary>
    internal static class UiFactory
    {
        private static Sprite _roundedSprite;
        private static bool _roundedSpriteResolved;

        /// <summary>
        /// Creates a <see cref="Text"/> component with the native font applied,
        /// rich text enabled, word wrap, and no raycast targeting.
        /// </summary>
        public static Text CreateText(Transform parent, string name, string value, int fontSize, TextAnchor anchor)
        {
            var obj = new GameObject(name);
            obj.transform.SetParent(parent, false);
            var text = obj.AddComponent<Text>();
            NativeFontCache.ApplyTo(text);
            text.text = value;
            text.fontSize = fontSize;
            text.alignment = anchor;
            text.supportRichText = true;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            return text;
        }

        /// <summary>
        /// Creates a <see cref="RectTransform"/> with an <see cref="Image"/> using the
        /// game's rounded-square sprite (sliced) or a simple fill as fallback.
        /// </summary>
        public static RectTransform CreateImage(Transform parent, string name, Color color, bool raycastTarget)
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

        /// <summary>
        /// Creates a button with a colored background, white bold label text,
        /// color-tint transition, and no navigation. On click, clears the current
        /// EventSystem selection before invoking the action.
        /// </summary>
        public static RectTransform CreateButton(Transform parent, string name, string label, Color bgColor, Action onClick)
        {
            var rect = CreateImage(parent, name, bgColor, true);
            var image = rect.GetComponent<Image>();
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.colors = CreateButtonColors(bgColor);
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            button.onClick.AddListener((UnityAction)(() =>
            {
                ClearSelectedObject();
                onClick?.Invoke();
            }));

            var text = CreateText(rect, "Label", label, DesignTokens.LabelMd, TextAnchor.MiddleCenter);
            text.color = DesignTokens.OnPrimary;
            text.fontStyle = FontStyle.Bold;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            SetStretch(text.rectTransform, 0f, 0f, 0f, 0f);
            return rect;
        }

        /// <summary>
        /// Builds a <see cref="ColorBlock"/> for button color-tint transitions:
        /// slightly brighter on highlight, slightly darker on press, same on select.
        /// </summary>
        public static ColorBlock CreateButtonColors(Color normal)
        {
            return new ColorBlock
            {
                normalColor = normal,
                highlightedColor = Color.Lerp(normal, Color.white, 0.04f),
                pressedColor = Color.Lerp(normal, Color.black, 0.18f),
                selectedColor = normal,
                disabledColor = new Color(0.35f, 0.30f, 0.40f, 0.7f),
                colorMultiplier = 1f,
                fadeDuration = 0.03f
            };
        }

        /// <summary>
        /// Stretches a RectTransform to fill its parent with the given insets.
        /// left/top/right are positive insets from the edges (right/top effectively negative offsets).
        /// </summary>
        public static void SetStretch(RectTransform rect, float left, float top, float right, float bottom)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(-right, -top);
        }

        /// <summary>
        /// Positions a RectTransform at fixed coordinates from the top-left corner of its parent.
        /// x/y are distances from the top-left; y is positive downward.
        /// </summary>
        public static void SetFixedTop(RectTransform rect, float x, float y, float width, float height)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.sizeDelta = new Vector2(width, height);
            rect.anchoredPosition = new Vector2(x, -y);
        }

        /// <summary>
        /// Creates a scrollable viewport with a background image, mask, and ScrollRect.
        /// The viewport is positioned at fixed coordinates from the parent's top-left.
        /// </summary>
        public static RectTransform CreateScrollViewport(Transform parent, string name, float x, float top, float width, float height, bool horizontal)
        {
            var viewport = CreateImage(parent, name, DesignTokens.SurfaceContainer, true);
            viewport.anchorMin = new Vector2(0f, 1f);
            viewport.anchorMax = new Vector2(0f, 1f);
            viewport.pivot = new Vector2(0f, 1f);
            viewport.sizeDelta = new Vector2(width, height);
            viewport.anchoredPosition = new Vector2(x, -top);

            var mask = viewport.gameObject.AddComponent<Mask>();
            mask.showMaskGraphic = true;

            var scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport;
            scroll.horizontal = horizontal;
            scroll.vertical = !horizontal;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 34f;

            return viewport;
        }

        /// <summary>
        /// Creates a Content RectTransform inside a scroll viewport and wires it
        /// to the ScrollRect. For vertical scroll, content stretches horizontally
        /// and grows downward; for horizontal scroll, content grows rightward.
        /// </summary>
        public static RectTransform CreateContent(RectTransform viewport, bool vertical)
        {
            var content = new GameObject("Content");
            content.transform.SetParent(viewport, false);
            var rect = content.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = vertical ? new Vector2(1f, 1f) : new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.offsetMin = new Vector2(10f, 0f);
            rect.offsetMax = vertical ? new Vector2(-10f, 0f) : new Vector2(0f, 0f);
            viewport.GetComponent<ScrollRect>().content = rect;
            return rect;
        }

        /// <summary>
        /// Loads the game's rounded-square sprite from Addressables (cached).
        /// Returns null if the asset is unavailable; callers fall back to Image.Type.Simple.
        /// </summary>
        public static Sprite GetRoundedSprite()
        {
            if (_roundedSpriteResolved) return _roundedSprite;

            _roundedSpriteResolved = true;
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

        /// <summary>
        /// Creates a root-level Canvas GameObject with DontDestroyOnLoad,
        /// ScreenSpaceOverlay rendering, override sorting, CanvasScaler at 1920x1080,
        /// and a GraphicRaycaster. Returns the root GameObject.
        /// </summary>
        public static GameObject CreateRootCanvas(string name, int sortingOrder)
        {
            var root = new GameObject(name);
            UnityEngine.Object.DontDestroyOnLoad(root);

            var rect = root.AddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.overrideSorting = true;
            canvas.sortingOrder = sortingOrder;

            var scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(DesignTokens.ReferenceWidth, DesignTokens.ReferenceHeight);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            root.AddComponent<GraphicRaycaster>();

            return root;
        }

        /// <summary>
        /// Clears the current EventSystem selection so button highlight states
        /// don't persist after a click.
        /// </summary>
        public static void ClearSelectedObject()
        {
            if (EventSystem.current != null)
            {
                EventSystem.current.SetSelectedGameObject(null);
            }
        }
    }
}
