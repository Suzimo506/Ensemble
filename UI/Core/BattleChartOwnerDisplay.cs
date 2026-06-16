using MDEN.Managers;
using UnityEngine;
using UnityEngine.UI;

namespace MDEN.UI.Core
{
    internal static class BattleChartOwnerDisplay
    {
        private static GameObject _root;
        private static Text _fontTemplate;

        public static void Show()
        {
            Destroy();

            var upPanel = FindBattleUpPanel();
            if (upPanel == null) return;

            _root = new GameObject("MDENBattleChartOwner");
            _root.transform.SetParent(upPanel, false);
            _root.transform.localPosition = new Vector3(720f, 395f, 0f);
            _root.transform.localScale = new Vector3(1f, 0.95f, 1f);

            var rect = _root.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(100f, 100f);

            var text = _root.AddComponent<Text>();
            ApplyGameFont(text);
            text.text = FormatOwnerText();
            text.fontSize = 32;
            text.lineSpacing = 0.8f;
            text.alignment = TextAnchor.UpperRight;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.supportRichText = true;
            text.raycastTarget = false;
        }

        public static void Destroy()
        {
            if (_root == null) return;
            UnityEngine.Object.Destroy(_root);
            _root = null;
        }

        private static Transform FindBattleUpPanel()
        {
            var battleRoot = GameObject.Find("UI_2D/Standard/PnlBattle/PnlBattleUI");
            if (battleRoot == null) return null;

            for (var i = 0; i < battleRoot.transform.childCount; i++)
            {
                var child = battleRoot.transform.GetChild(i);
                if (!child.gameObject.activeInHierarchy) continue;

                var upPanel = child.Find("Up");
                if (upPanel != null) return upPanel;
            }

            return null;
        }

        private static string FormatOwnerText()
        {
            var entry = PlaylistManager.GetCurrentPlaylistEntry();
            var owner = string.IsNullOrWhiteSpace(entry?.OwnerName) ? "Unknown" : entry.OwnerName;
            return $"<size=22>选谱人: <color=#{Constants.ColorCyan}>{EscapeRichText(owner)}</color></size>";
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
            if (_fontTemplate != null && _fontTemplate.font != null) return _fontTemplate;

            var candidates = Resources.FindObjectsOfTypeAll<Text>();
            foreach (var text in candidates)
            {
                if (text == null || text.font == null) continue;
                var fontName = text.font.name ?? string.Empty;
                if (fontName.Contains("Arial")) continue;

                _fontTemplate = text;
                return _fontTemplate;
            }

            return null;
        }

        private static string EscapeRichText(string value)
        {
            return value?.Replace("<", "＜").Replace(">", "＞") ?? string.Empty;
        }
    }
}
