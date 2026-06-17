using System;
using Il2CppUI.Controls;
using MDEN.Managers;
using MDEN.Protocol.Messages.Battle;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace MDEN.UI.Core
{
    public static class SettlementResultDialog
    {
        private const string RootName = "MDENSettlementResultDialog";
        private static GameObject _root;

        public static void Show(SettlementResultPush result)
        {
            Destroy();

            var box = CreateCommonBox();
            if (box == null) return;

            _root = box.transform.root.gameObject;
            var fontTemplate = GetFontTemplate(box);
            HideNativeControls(box);
            box.ResetImgBgSize(new Vector2(860f, 430f));

            var contentParent = box.imgBgRect != null ? box.imgBgRect : box.transform as RectTransform;
            if (contentParent == null)
            {
                Destroy();
                return;
            }

            CreateTitle(fontTemplate, contentParent);
            CreateContent(fontTemplate, contentParent, result);
            CreateCloseButton(fontTemplate, contentParent);
        }

        public static void Destroy()
        {
            if (_root == null) return;

            UnityEngine.Object.Destroy(_root);
            _root = null;
        }

        private static CommonMessageBox CreateCommonBox()
        {
            var template = CommonMessageBox.s_Instance ?? GameObject.FindObjectOfType<CommonMessageBox>();
            if (template == null) return null;

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
            canvas.sortingOrder = 9999;
            root.AddComponent<GraphicRaycaster>();

            var shade = new GameObject("Shade");
            shade.transform.SetParent(root.transform, false);
            var shadeRect = shade.AddComponent<RectTransform>();
            shadeRect.anchorMin = Vector2.zero;
            shadeRect.anchorMax = Vector2.one;
            shadeRect.offsetMin = Vector2.zero;
            shadeRect.offsetMax = Vector2.zero;

            var shadeImage = shade.AddComponent<Image>();
            shadeImage.color = new Color(0f, 0f, 0f, 0.55f);
            shadeImage.raycastTarget = true;

            var original = CommonMessageBox.s_Instance;
            GameObject clone = null;
            try
            {
                clone = UnityEngine.Object.Instantiate(template.gameObject, root.transform);
            }
            finally
            {
                CommonMessageBox.s_Instance = original;
            }

            if (clone == null)
            {
                UnityEngine.Object.Destroy(root);
                return null;
            }

            clone.SetActive(true);
            var box = clone.GetComponent<CommonMessageBox>();
            if (box == null)
            {
                UnityEngine.Object.Destroy(root);
                return null;
            }

            box.ResetMsgBox();
            ClearTexts(clone);

            var rect = clone.GetComponent<RectTransform>();
            if (rect != null)
            {
                rect.anchoredPosition = Vector2.zero;
                rect.localScale = Vector3.one;
            }

            return box;
        }

        private static void HideNativeControls(CommonMessageBox box)
        {
            box.btnDefaultClose?.gameObject.SetActive(false);

            var buttons = box.GetComponentsInChildren<Button>(true);
            foreach (var button in buttons)
            {
                if (button != null) button.gameObject.SetActive(false);
            }
        }

        private static void ClearTexts(GameObject root)
        {
            var texts = root.GetComponentsInChildren<Text>(true);
            foreach (var text in texts)
            {
                if (text != null) text.text = string.Empty;
            }
        }

        private static Text GetFontTemplate(CommonMessageBox box)
        {
            var textComponents = box.textComponents;
            return textComponents != null && textComponents.Length > 0 ? textComponents[0] : null;
        }

        private static void CreateTitle(Text template, RectTransform parent)
        {
            var title = CreateText(template, parent, "Title", "结算", 36, TextAnchor.MiddleCenter);
            title.color = Color.white;
            var rect = title.rectTransform;
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.sizeDelta = new Vector2(760f, 54f);
            rect.anchoredPosition = new Vector2(0f, -28f);
        }

        private static void CreateContent(Text template, RectTransform parent, SettlementResultPush result)
        {
            var content = CreateText(template, parent, "Content", BuildContent(result), 28, TextAnchor.UpperLeft);
            content.color = Color.white;
            content.lineSpacing = 1.18f;
            var rect = content.rectTransform;
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.sizeDelta = new Vector2(720f, 250f);
            rect.anchoredPosition = new Vector2(0f, -95f);
        }

        private static void CreateCloseButton(Text template, RectTransform parent)
        {
            var buttonObj = new GameObject("CloseButton");
            buttonObj.transform.SetParent(parent, false);

            var rect = buttonObj.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.sizeDelta = new Vector2(260f, 58f);
            rect.anchoredPosition = new Vector2(0f, 30f);

            var image = buttonObj.AddComponent<Image>();
            image.color = new Color(1f, 0.73f, 0.16f, 0.96f);

            var button = buttonObj.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener((UnityAction)new Action(Destroy));

            var label = CreateText(template, rect, "Label", "确认", 30, TextAnchor.MiddleCenter);
            label.color = new Color(0.25f, 0.05f, 0.36f, 1f);
            label.fontStyle = FontStyle.Bold;
            var labelRect = label.rectTransform;
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;
        }

        private static Text CreateText(
            Text template,
            Transform parent,
            string name,
            string value,
            int fontSize,
            TextAnchor alignment)
        {
            var obj = new GameObject(name);
            obj.transform.SetParent(parent, false);
            var text = obj.AddComponent<Text>();
            if (template != null)
            {
                text.font = template.font;
                text.material = template.material;
            }

            text.text = value;
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.supportRichText = true;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            return text;
        }

        private static string BuildContent(SettlementResultPush result)
        {
            return $"{ColorLabel("龙币", "ffd700ff")}：{FormatNames(result?.DragonCoinUids, result)}\n" +
                   $"{ColorLabel("最能连之人", Constants.ColorBlue)}：{FormatNames(result?.ComboUids, result)}\n" +
                   $"{ColorLabel("P佬", Constants.ColorPink)}：{FormatNames(result?.PerfectUids, result)}\n" +
                   $"{ColorLabel("真·梦游少女", "ff9f1aff")}：{FormatNames(result?.SleepwalkUids, result)}";
        }

        private static string ColorLabel(string value, string color)
        {
            return $"<color=#{NormalizeRichTextColor(color)}>{value}</color>";
        }

        private static string FormatNames(string[] uids, SettlementResultPush result)
        {
            if (uids == null || uids.Length == 0) return "暂无";

            var names = new System.Collections.Generic.List<string>();
            foreach (var uid in uids)
            {
                var name = EscapeRichText(GetPlayerName(uid, result));
                if (!string.IsNullOrWhiteSpace(name))
                {
                    names.Add($"<color=#ffffffff>{name}</color>");
                }
            }

            return names.Count == 0 ? "暂无" : string.Join("，", names);
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

            return uid ?? "Unknown";
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
