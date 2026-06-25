using System;
using UnityEngine;
using UnityEngine.UI;
using PeroInputManager = Il2CppAssets.Scripts.PeroTools.Managers.InputManager;

namespace MDEN.UI.Core
{
    /// <summary>
    /// Native Unity UI replacement for PopupLib.UI.Windows.InputWindow.
    /// A smaller modal dialog for text input, rendered above list windows
    /// (DialogSortingOrder &gt; WindowSortingOrder).
    /// </summary>
    public sealed class NativeInputDialog : INativeBaseWindow
    {
        private const float PanelWidth = 600f;
        private const float PanelHeight = 280f;
        private const float TitleY = 32f;
        private const float TitleHeight = 36f;
        private const float InputY = 88f;
        private const float InputHeight = 48f;
        private const float InputPaddingH = 24f;
        private const float ButtonY = 160f;
        private const float ButtonHeight = 44f;
        private const float ButtonGap = DesignTokens.SpacingMd;
        private const float ButtonMarginH = 24f;
        private const float ButtonWidth = (PanelWidth - ButtonMarginH * 2 - ButtonGap) * 0.5f;
        private const float CancelButtonX = ButtonMarginH + ButtonWidth + ButtonGap;

        private GameObject _root;
        private RectTransform _panel;
        private InputField _inputField;

        /// <summary>
        /// The text entered by the user. Set to the input value on confirm,
        /// or null on cancel.
        /// </summary>
        public string Result { get; private set; }

        /// <summary>Replaces InputWindow.OnCompletion. Fires when either button is clicked.</summary>
        public event Action<INativeBaseWindow> OnCompletion;

        /// <summary>
        /// Builds the dialog UI, blocks native input, and displays the dialog.
        /// Accepts optional title and placeholder strings.
        /// </summary>
        public void Show(string title = "", string placeholder = "")
        {
            if (_root != null) return;

            _root = UiFactory.CreateRootCanvas("MDENInputDialogRoot", DesignTokens.DialogSortingOrder);

            CreateShade(_root.transform);
            _panel = CreatePanel(_root.transform);
            CreateTitle(_panel, title);
            _inputField = CreateInputField(_panel, placeholder);
            CreateButtons(_panel);

            BlockNativeInput();
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
            panel.sizeDelta = new Vector2(PanelWidth, PanelHeight);
            return panel;
        }

        private static void CreateTitle(RectTransform panel, string title)
        {
            var text = UiFactory.CreateText(panel, "Title",
                string.IsNullOrEmpty(title) ? string.Empty : title,
                DesignTokens.HeadlineMd, TextAnchor.MiddleCenter);
            text.color = DesignTokens.OnSurface;
            text.fontStyle = FontStyle.Bold;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.rectTransform.anchorMin = new Vector2(0.5f, 1f);
            text.rectTransform.anchorMax = new Vector2(0.5f, 1f);
            text.rectTransform.pivot = new Vector2(0.5f, 1f);
            text.rectTransform.sizeDelta = new Vector2(PanelWidth - InputPaddingH * 2, TitleHeight);
            text.rectTransform.anchoredPosition = new Vector2(0f, -TitleY);
        }

        private static InputField CreateInputField(RectTransform panel, string placeholder)
        {
            var bg = UiFactory.CreateImage(panel, "InputBg",
                DesignTokens.SurfaceContainer, true);
            bg.anchorMin = new Vector2(0f, 1f);
            bg.anchorMax = new Vector2(0f, 1f);
            bg.pivot = new Vector2(0f, 1f);
            bg.sizeDelta = new Vector2(PanelWidth - InputPaddingH * 2, InputHeight);
            bg.anchoredPosition = new Vector2(InputPaddingH, -InputY);

            var inputField = bg.gameObject.AddComponent<InputField>();
            inputField.targetGraphic = bg.GetComponent<Image>();

            var text = UiFactory.CreateText(bg, "Text", string.Empty,
                DesignTokens.BodyMd, TextAnchor.MiddleLeft);
            text.color = DesignTokens.OnSurface;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            UiFactory.SetStretch(text.rectTransform, 12f, 0f, 12f, 0f);

            var placeholderText = UiFactory.CreateText(bg, "Placeholder",
                string.IsNullOrEmpty(placeholder) ? "请输入..." : placeholder,
                DesignTokens.BodySm, TextAnchor.MiddleLeft);
            placeholderText.color = DesignTokens.OnSurfaceVariant;
            placeholderText.fontStyle = FontStyle.Italic;
            placeholderText.horizontalOverflow = HorizontalWrapMode.Overflow;
            UiFactory.SetStretch(placeholderText.rectTransform, 12f, 0f, 12f, 0f);

            inputField.textComponent = text;
            inputField.placeholder = placeholderText;

            return inputField;
        }

        private void CreateButtons(RectTransform panel)
        {
            var confirm = UiFactory.CreateButton(panel, "ConfirmButton", "确认",
                DesignTokens.Primary, ConfirmAndClose);
            confirm.anchorMin = new Vector2(0f, 1f);
            confirm.anchorMax = new Vector2(0f, 1f);
            confirm.pivot = new Vector2(0f, 1f);
            confirm.sizeDelta = new Vector2(ButtonWidth, ButtonHeight);
            confirm.anchoredPosition = new Vector2(ButtonMarginH, -ButtonY);

            var cancel = UiFactory.CreateButton(panel, "CancelButton", "取消",
                DesignTokens.SecondaryContainer, CancelAndClose);
            cancel.anchorMin = new Vector2(0f, 1f);
            cancel.anchorMax = new Vector2(0f, 1f);
            cancel.pivot = new Vector2(0f, 1f);
            cancel.sizeDelta = new Vector2(ButtonWidth, ButtonHeight);
            cancel.anchoredPosition = new Vector2(CancelButtonX, -ButtonY);

            // CreateButton uses OnPrimary (white) for label text; the cancel button
            // needs Secondary (blue) text per DESIGN.md.
            var cancelLabel = cancel.GetComponentInChildren<Text>();
            if (cancelLabel != null)
            {
                cancelLabel.color = DesignTokens.Secondary;
            }
        }

        // ─────────────────────────────────────────────────────────────────────────────
        //  Button Actions
        // ─────────────────────────────────────────────────────────────────────────────

        private void ConfirmAndClose()
        {
            Result = _inputField?.text;
            OnCompletion?.Invoke(this);
            DestroyRoot();
        }

        private void CancelAndClose()
        {
            Result = null;
            OnCompletion?.Invoke(this);
            DestroyRoot();
        }

        private void DestroyRoot()
        {
            if (_root == null) return;

            RestoreNativeInput();

            UnityEngine.Object.Destroy(_root);
            _root = null;
            _panel = null;
            _inputField = null;
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
    }
}
