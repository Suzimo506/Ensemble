using MDEN.Managers;
using UnityEngine;
using UnityEngine.UI;

namespace MDEN.UI.Core
{
    internal static class BattleChartOwnerDisplay
    {
        private const float OwnerTextY = 382f;
        private static GameObject _root;
        private static Text _ownerText;

        public static void Show()
        {
            Destroy();

            var upPanel = FindBattleUpPanel();
            if (upPanel == null) return;

            _root = new GameObject("MDENBattleChartOwner");
            _root.transform.SetParent(upPanel, false);
            _root.transform.localPosition = new Vector3(720f, OwnerTextY, 0f);
            _root.transform.localScale = new Vector3(1f, 0.95f, 1f);

            var rect = _root.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(100f, 100f);

            var text = _root.AddComponent<Text>();
            ApplyGameFont(text);
            _ownerText = text;
            Refresh();
            text.fontSize = 32;
            text.lineSpacing = 0.8f;
            text.alignment = TextAnchor.UpperRight;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.supportRichText = true;
            text.raycastTarget = false;
            ConnectionManager.StateChanged -= OnConnectionStateChanged;
            ConnectionManager.StateChanged += OnConnectionStateChanged;
        }

        public static void Refresh()
        {
            if (_ownerText == null) return;
            _ownerText.text = FormatOwnerText();
        }

        public static void Destroy()
        {
            ConnectionManager.StateChanged -= OnConnectionStateChanged;
            if (_root == null) return;
            UnityEngine.Object.Destroy(_root);
            _root = null;
            _ownerText = null;
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
            var text = $"<size=22>选谱人: <color=#{Constants.ColorCyan}>{EscapeRichText(owner)}</color></size>";
            if (ConnectionManager.IsReconnecting)
            {
                text += $"\n<size=20><color=#{Constants.ColorRed}>网络质量差，尝试重连中...</color></size>";
            }

            return text;
        }

        private static void OnConnectionStateChanged(ConnectionLifecycleState state)
        {
            MainThreadDispatcher.Enqueue(Refresh);
        }

        private static void ApplyGameFont(Text text)
        {
            NativeFontCache.ApplyTo(text);
        }

        private static string EscapeRichText(string value)
        {
            return value?.Replace("<", "＜").Replace(">", "＞") ?? string.Empty;
        }
    }
}
