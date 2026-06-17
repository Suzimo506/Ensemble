using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
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
        private const int SortingOrder = 32760;
        private const float PanelWidth = 980f;
        private const float PanelHeight = 700f;
        private const float ContentWidth = 800f;
        private const float ContentViewportHeight = 450f;

        private static GameObject _root;
        private static Font _cachedFont;
        private static Sprite _roundedSprite;

        public static void Show(SettlementResultPush result)
        {
            try
            {
                Destroy();

                _root = CreateRoot();
                var panel = CreatePanel(_root.transform);
                if (panel == null)
                {
                    Destroy();
                    return;
                }

                CreateTitle(panel);
                CreateContent(panel, result);
                CreateCloseButton(panel);
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Show settlement result dialog failed: {ex}");
                Destroy();
            }
        }

        public static void Destroy()
        {
            if (_root == null) return;

            UnityEngine.Object.Destroy(_root);
            _root = null;
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
            shadeImage.color = new Color(0.02f, 0f, 0.05f, 0.62f);
            shadeImage.raycastTarget = true;

            var shadeButton = shade.AddComponent<Button>();
            shadeButton.targetGraphic = shadeImage;
            shadeButton.transition = Selectable.Transition.None;
            shadeButton.onClick.AddListener((UnityAction)new Action(Destroy));

            return root;
        }

        private static RectTransform CreatePanel(Transform root)
        {
            var border = new GameObject("PanelBorder");
            border.transform.SetParent(root, false);
            var borderRect = border.AddComponent<RectTransform>();
            borderRect.anchorMin = new Vector2(0.5f, 0.5f);
            borderRect.anchorMax = new Vector2(0.5f, 0.5f);
            borderRect.pivot = new Vector2(0.5f, 0.5f);
            borderRect.sizeDelta = new Vector2(PanelWidth + 12f, PanelHeight + 12f);
            borderRect.anchoredPosition = Vector2.zero;

            var borderImage = border.AddComponent<Image>();
            borderImage.sprite = GetRoundedSprite();
            borderImage.type = borderImage.sprite != null ? Image.Type.Sliced : Image.Type.Simple;
            borderImage.color = new Color(0.98f, 0.82f, 0.22f, 0.98f);
            borderImage.raycastTarget = true;

            var panel = new GameObject("Panel");
            panel.transform.SetParent(border.transform, false);
            var panelRect = panel.AddComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = new Vector2(PanelWidth, PanelHeight);
            panelRect.anchoredPosition = Vector2.zero;

            var panelImage = panel.AddComponent<Image>();
            panelImage.sprite = GetRoundedSprite();
            panelImage.type = panelImage.sprite != null ? Image.Type.Sliced : Image.Type.Simple;
            panelImage.color = new Color(0.28f, 0.15f, 0.52f, 0.98f);
            panelImage.raycastTarget = true;

            var header = CreateImage(panelRect, "HeaderGlow", new Color(0.45f, 0.27f, 0.74f, 0.92f));
            header.anchorMin = new Vector2(0f, 1f);
            header.anchorMax = new Vector2(1f, 1f);
            header.pivot = new Vector2(0.5f, 1f);
            header.offsetMin = new Vector2(34f, -92f);
            header.offsetMax = new Vector2(-34f, -18f);

            return panelRect;
        }

        private static void CreateTitle(RectTransform parent)
        {
            var title = CreateText(parent, "Title", ColorLabel("结算", "5f7bffff"), 46, TextAnchor.MiddleCenter);
            title.color = Color.white;
            title.fontStyle = FontStyle.Bold;
            title.lineSpacing = 1f;

            var outline = title.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0.08f, 0.03f, 0.24f, 0.72f);
            outline.effectDistance = new Vector2(2f, -2f);

            var rect = title.rectTransform;
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.sizeDelta = new Vector2(ContentWidth, 70f);
            rect.anchoredPosition = new Vector2(0f, -26f);
        }

        private static void CreateContent(RectTransform parent, SettlementResultPush result)
        {
            var viewport = new GameObject("ContentViewport");
            viewport.transform.SetParent(parent, false);
            var viewportRect = viewport.AddComponent<RectTransform>();
            viewportRect.anchorMin = new Vector2(0.5f, 1f);
            viewportRect.anchorMax = new Vector2(0.5f, 1f);
            viewportRect.pivot = new Vector2(0.5f, 1f);
            viewportRect.sizeDelta = new Vector2(ContentWidth, ContentViewportHeight);
            viewportRect.anchoredPosition = new Vector2(0f, -120f);

            var viewportImage = viewport.AddComponent<Image>();
            viewportImage.sprite = GetRoundedSprite();
            viewportImage.type = viewportImage.sprite != null ? Image.Type.Sliced : Image.Type.Simple;
            viewportImage.color = new Color(0.17f, 0.08f, 0.34f, 0.38f);
            viewportImage.raycastTarget = true;

            var mask = viewport.AddComponent<Mask>();
            mask.showMaskGraphic = true;

            var contentRoot = new GameObject("ContentRoot");
            contentRoot.transform.SetParent(viewportRect, false);
            var contentRect = contentRoot.AddComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.pivot = new Vector2(0.5f, 1f);
            contentRect.offsetMin = new Vector2(26f, 0f);
            contentRect.offsetMax = new Vector2(-26f, 0f);

            var content = CreateText(contentRect, "Content", BuildContent(result), 24, TextAnchor.UpperLeft);
            content.color = Color.white;
            content.lineSpacing = 1.18f;
            content.horizontalOverflow = HorizontalWrapMode.Wrap;
            content.verticalOverflow = VerticalWrapMode.Overflow;

            var textRect = content.rectTransform;
            textRect.anchorMin = new Vector2(0f, 1f);
            textRect.anchorMax = new Vector2(1f, 1f);
            textRect.pivot = new Vector2(0.5f, 1f);
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            Canvas.ForceUpdateCanvases();
            var contentHeight = Mathf.Max(ContentViewportHeight, content.preferredHeight + 28f);
            contentRect.sizeDelta = new Vector2(0f, contentHeight);
            textRect.sizeDelta = new Vector2(0f, contentHeight);

            var scroll = viewport.AddComponent<ScrollRect>();
            scroll.content = contentRect;
            scroll.viewport = viewportRect;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 28f;
        }

        private static void CreateCloseButton(RectTransform parent)
        {
            var buttonObj = new GameObject("CloseButton");
            buttonObj.transform.SetParent(parent, false);

            var rect = buttonObj.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.sizeDelta = new Vector2(280f, 62f);
            rect.anchoredPosition = new Vector2(0f, 34f);

            var image = buttonObj.AddComponent<Image>();
            image.sprite = GetRoundedSprite();
            image.type = image.sprite != null ? Image.Type.Sliced : Image.Type.Simple;
            image.color = new Color(1f, 0.69f, 0.15f, 1f);

            var button = buttonObj.AddComponent<Button>();
            button.targetGraphic = image;
            button.colors = new ColorBlock
            {
                normalColor = new Color(1f, 0.69f, 0.15f, 1f),
                highlightedColor = new Color(1f, 0.79f, 0.25f, 1f),
                pressedColor = new Color(0.91f, 0.49f, 0.08f, 1f),
                selectedColor = new Color(1f, 0.73f, 0.18f, 1f),
                disabledColor = new Color(0.55f, 0.45f, 0.38f, 0.8f),
                colorMultiplier = 1f,
                fadeDuration = 0.08f
            };
            button.onClick.AddListener((UnityAction)new Action(Destroy));

            var label = CreateText(rect, "Label", "确认", 30, TextAnchor.MiddleCenter);
            label.color = new Color(0.20f, 0.04f, 0.31f, 1f);
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

        private static RectTransform CreateImage(Transform parent, string name, Color color)
        {
            var obj = new GameObject(name);
            obj.transform.SetParent(parent, false);
            var rect = obj.AddComponent<RectTransform>();
            var image = obj.AddComponent<Image>();
            image.sprite = GetRoundedSprite();
            image.type = image.sprite != null ? Image.Type.Sliced : Image.Type.Simple;
            image.color = color;
            image.raycastTarget = false;
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

        private static string BuildContent(SettlementResultPush result)
        {
            var builder = new StringBuilder();
            builder.AppendLine($"{ColorLabel("龙币", "ffd700ff")}：{FormatNames(result?.DragonCoinUids, result)}");
            builder.AppendLine($"{ColorLabel("最能连之人", Constants.ColorBlue)}：{FormatNames(result?.ComboUids, result)}");
            builder.AppendLine($"{ColorLabel("P佬", Constants.ColorPink)}：{FormatNames(result?.PerfectUids, result)}");
            builder.AppendLine($"{ColorLabel("真·梦游少女", "ff9f1aff")}：{FormatNames(result?.SleepwalkUids, result)}");
            builder.AppendLine();
            builder.Append(BuildPlayedCharts(result?.PlayedCharts));
            return builder.ToString();
        }

        private static string BuildPlayedCharts(SettlementChartEntry[] charts)
        {
            if (charts == null || charts.Length == 0)
            {
                return $"{ColorLabel("本次游玩时长", Constants.ColorCyan)}：--:--\n" +
                       $"{ColorLabel("游玩曲目", Constants.ColorBlue)}：暂无";
            }

            var lines = new List<string>();
            var totalSeconds = 0f;
            var hasUnknownDuration = false;

            for (var i = 0; i < charts.Length; i++)
            {
                var chart = charts[i];
                var name = EscapeRichText(string.IsNullOrWhiteSpace(chart?.ChartName)
                    ? $"Unknown Chart {chart?.Difficulty ?? 0}"
                    : chart.ChartName);
                var durationSeconds = GetDurationSeconds(chart);
                if (durationSeconds.HasValue)
                {
                    totalSeconds += durationSeconds.Value;
                }
                else
                {
                    hasUnknownDuration = true;
                }

                lines.Add($"{ColorLabel((i + 1).ToString(), Constants.ColorYellow)}. {ColorLabel(name, "ffffffff")} {FormatDifficulty(chart?.Difficulty ?? 0)}");
            }

            var total = hasUnknownDuration ? "--:--" : FormatDuration(totalSeconds);
            return $"{ColorLabel("本次游玩时长", Constants.ColorCyan)}：{ColorLabel(total, "ffffffff")}\n" +
                   $"{ColorLabel("游玩曲目", Constants.ColorBlue)}：\n" +
                   string.Join("\n", lines);
        }

        private static float? GetDurationSeconds(SettlementChartEntry chart)
        {
            if (chart == null || string.IsNullOrWhiteSpace(chart.ChartKey)) return null;

            object musicInfo = null;
            try
            {
                musicInfo = ChartManager.GetMusicInfo(chart.ChartKey);
            }
            catch
            {
                return null;
            }

            if (musicInfo == null) return null;

            var value = TryReadDurationValue(musicInfo);
            if (!value.HasValue || value <= 0f) return null;

            return value > 10000f ? value / 1000f : value;
        }

        private static float? TryReadDurationValue(object source)
        {
            var names = new[]
            {
                "duration", "Duration", "musicDuration", "MusicDuration",
                "musicLength", "MusicLength", "musicTime", "MusicTime",
                "songLength", "SongLength", "songTime", "SongTime",
                "length", "Length", "time", "Time"
            };

            var type = source.GetType();
            foreach (var name in names)
            {
                try
                {
                    var property = type.GetProperty(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (TryConvertDuration(property?.GetValue(source), out var seconds)) return seconds;

                    var field = type.GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (TryConvertDuration(field?.GetValue(source), out seconds)) return seconds;
                }
                catch
                {
                    // Some Il2Cpp-backed members throw when reflected during scene transitions.
                }
            }

            var methodNames = new[]
            {
                "GetDuration", "GetMusicDuration", "GetSongDuration",
                "GetLength", "GetMusicLength", "GetSongLength",
                "GetTime", "GetMusicTime", "GetSongTime"
            };
            foreach (var name in methodNames)
            {
                try
                {
                    var method = type.GetMethod(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, null, Type.EmptyTypes, null);
                    if (TryConvertDuration(method?.Invoke(source, null), out var seconds)) return seconds;
                }
                catch
                {
                    // Some Il2Cpp-backed methods throw when reflected during scene transitions.
                }
            }

            return null;
        }

        private static bool TryConvertDuration(object value, out float seconds)
        {
            seconds = 0f;
            if (value == null) return false;

            try
            {
                seconds = Convert.ToSingle(value);
                return seconds > 0f;
            }
            catch
            {
                return false;
            }
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
            return difficulty switch
            {
                1 => ColorLabel("萌新", "00d45aff"),
                2 => ColorLabel("高手", Constants.ColorBlue),
                3 => ColorLabel("大触", "9b55ffff"),
                4 => ColorLabel("隐藏", "ff5555ff"),
                _ => ColorLabel($"难度{difficulty}", "b8b8b8ff")
            };
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
