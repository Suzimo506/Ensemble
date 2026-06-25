using System;
using UnityEngine;
using UnityEngine.UI;
using PeroInputManager = Il2CppAssets.Scripts.PeroTools.Managers.InputManager;

namespace MDEN.UI.Core
{
    /// <summary>
    /// Native confirm/cancel dialog built with our own Unity UI.
    /// Uses sortingOrder 32764 so it always renders above NativeListWindow (32762)
    /// and NativeInputDialog (32763).
    /// </summary>
    public static class NativeConfirmDialog
    {
        private const float PanelWidth = 520f;
        private const float PanelHeight = 280f;
        private const float MessagePadding = 24f;
        private const float ButtonHeight = 44f;
        private const float ButtonGap = 16f;
        private const float ButtonMarginH = 24f;
        private const float ButtonWidth = (PanelWidth - ButtonMarginH * 2 - ButtonGap) * 0.5f;
        private const float ButtonY = PanelHeight - ButtonHeight - 20f;
        private const int ConfirmSortingOrder = 32764;

        public static void Show(string title, string message, Action<bool> onCompleted)
        {
            var body = string.IsNullOrWhiteSpace(title) ? (message ?? string.Empty) : $"{title}\n{message}";

            var root = UiFactory.CreateRootCanvas("MDENConfirmDialogRoot", ConfirmSortingOrder);

            // Dim shade
            var shade = UiFactory.CreateImage(root.transform, "Shade", DesignTokens.Shade, true);
            UiFactory.SetStretch(shade, 0f, 0f, 0f, 0f);

            // Panel
            var panel = UiFactory.CreateImage(root.transform, "Panel", DesignTokens.Surface, true);
            panel.anchorMin = new Vector2(0.5f, 0.5f);
            panel.anchorMax = new Vector2(0.5f, 0.5f);
            panel.pivot = new Vector2(0.5f, 0.5f);
            panel.sizeDelta = new Vector2(PanelWidth, PanelHeight);

            // Message text (wraps, overflow allowed)
            var msg = UiFactory.CreateText(panel, "Message", body,
                DesignTokens.BodyMd, TextAnchor.UpperCenter);
            msg.color = DesignTokens.OnSurface;
            msg.lineSpacing = 1.5f;
            var msgRect = msg.rectTransform;
            msgRect.anchorMin = new Vector2(0f, 1f);
            msgRect.anchorMax = new Vector2(1f, 1f);
            msgRect.pivot = new Vector2(0.5f, 1f);
            msgRect.sizeDelta = new Vector2(PanelWidth - MessagePadding * 2, PanelHeight - ButtonHeight - 60f);
            msgRect.anchoredPosition = new Vector2(0f, -20f);

            // Confirm button (primary pink)
            var confirm = UiFactory.CreateButton(panel, "ConfirmButton", "确认",
                DesignTokens.Primary, () =>
                {
                    DestroyRoot(root);
                    onCompleted?.Invoke(true);
                });
            confirm.anchorMin = new Vector2(0f, 0f);
            confirm.anchorMax = new Vector2(0f, 0f);
            confirm.pivot = new Vector2(0f, 0f);
            confirm.sizeDelta = new Vector2(ButtonWidth, ButtonHeight);
            confirm.anchoredPosition = new Vector2(ButtonMarginH, 20f);

            // Cancel button (secondary blue)
            var cancel = UiFactory.CreateButton(panel, "CancelButton", "取消",
                DesignTokens.SecondaryContainer, () =>
                {
                    DestroyRoot(root);
                    onCompleted?.Invoke(false);
                });
            cancel.anchorMin = new Vector2(0f, 0f);
            cancel.anchorMax = new Vector2(0f, 0f);
            cancel.pivot = new Vector2(0f, 0f);
            cancel.sizeDelta = new Vector2(ButtonWidth, ButtonHeight);
            cancel.anchoredPosition = new Vector2(ButtonMarginH + ButtonWidth + ButtonGap, 20f);

            // Cancel button label color should be Secondary (blue), not OnPrimary (white)
            var cancelLabel = cancel.GetComponentInChildren<Text>();
            if (cancelLabel != null)
            {
                cancelLabel.color = DesignTokens.Secondary;
            }

            BlockNativeInput();
        }

        private static void DestroyRoot(GameObject root)
        {
            if (root == null) return;
            RestoreNativeInput();
            UnityEngine.Object.Destroy(root);
        }

        private static void BlockNativeInput()
        {
            try
            {
                if (PeroInputManager.instance != null)
                    PeroInputManager.instance.isStopKeyAction = true;
            }
            catch { /* manager unavailable during scene transitions */ }
        }

        private static void RestoreNativeInput()
        {
            try
            {
                if (PeroInputManager.instance != null)
                    PeroInputManager.instance.isStopKeyAction = false;
            }
            catch { /* manager unavailable during scene transitions */ }
        }
    }
}
