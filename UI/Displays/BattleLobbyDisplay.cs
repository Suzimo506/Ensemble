using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using MDEN.Managers;
using MDEN.Protocol.Messages.Lobby;
using MDEN.Protocol.Models;
using UnityEngine;
using UnityEngine.UI;

namespace MDEN.UI.Displays
{
    internal sealed class BattleLobbyDisplay
    {
        private const int OverlaySortingOrder = 32767;
        private const int FontSize = 26;
        private const float EntryWidth = 640f;
        private const float EntryHeight = 34f;
        private static Text _fontTemplate;

        private readonly Dictionary<string, Text> _entries = new Dictionary<string, Text>();
        private readonly List<string> _entryOrder = new List<string>();
        private GameObject _frame;

        public bool IsCreated => _frame != null;

        public void Create()
        {
            if (_frame != null) return;

            _frame = new GameObject("MDENBattleLobbyDisplay");
            var rect = _frame.AddComponent<RectTransform>();
            rect.localScale = Vector3.one;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition3D = Vector3.zero;
            rect.sizeDelta = Vector2.zero;

            var canvas = _frame.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = OverlaySortingOrder;

            var scaler = _frame.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            HideInfoPlusLabel();
        }

        public void Destroy()
        {
            Clear();
            if (_frame != null)
            {
                UnityEngine.Object.Destroy(_frame);
                _frame = null;
            }
        }

        public void Refresh(BattlePlayerEntry[] players)
        {
            if (!LobbyManager.IsInLobby)
            {
                Destroy();
                return;
            }

            Create();
            if (_frame == null) return;

            HideInfoPlusLabel();
            EnsureOverlayOrder();

            var orderedPlayers = OrderPlayers(players ?? Array.Empty<BattlePlayerEntry>());
            foreach (var player in orderedPlayers)
            {
                SetEntry(player.Uid, FormatEntry(player));
            }

            RemoveMissingEntries(orderedPlayers.Select(player => player.Uid));
            PositionEntries();
        }

        public void Clear()
        {
            foreach (var text in _entries.Values)
            {
                if (text != null)
                {
                    UnityEngine.Object.Destroy(text.gameObject);
                }
            }

            _entries.Clear();
            _entryOrder.Clear();
        }

        private void SetEntry(string uid, string value)
        {
            if (string.IsNullOrEmpty(uid) || _frame == null) return;

            if (!_entries.TryGetValue(uid, out var text) || text == null)
            {
                text = CreateText(uid);
                _entries[uid] = text;
                _entryOrder.Add(uid);
            }

            text.text = value;
        }

        private Text CreateText(string uid)
        {
            var obj = new GameObject("Entry_" + uid);
            var rect = obj.AddComponent<RectTransform>();
            rect.SetParent(_frame.transform);
            rect.localScale = Vector3.one;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.zero;
            rect.pivot = Vector2.zero;
            rect.anchoredPosition = new Vector2(24f, 30f);
            rect.sizeDelta = new Vector2(EntryWidth, EntryHeight);

            var text = obj.AddComponent<Text>();
            ApplyGameFont(text);
            text.fontSize = FontSize;
            text.alignment = TextAnchor.LowerLeft;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.supportRichText = true;
            text.raycastTarget = false;
            text.color = Color.white;

            var shadow = obj.AddComponent<Shadow>();
            shadow.effectDistance = new Vector2(2f, -2f);
            shadow.effectColor = new Color(0f, 0f, 0f, 0.35f);

            return text;
        }

        private BattlePlayerEntry[] OrderPlayers(BattlePlayerEntry[] players)
        {
            var lobby = LobbyManager.CurrentLobby;
            var playerOrder = lobby?.Players ?? Array.Empty<string>();
            return players
                .Where(player => player != null && !string.IsNullOrEmpty(player.Uid))
                .OrderBy(player => GetPlayerSortIndex(player.Uid, playerOrder))
                .ToArray();
        }

        private static int GetPlayerSortIndex(string uid, string[] playerOrder)
        {
            if (uid == PlayerManager.CurrentUid) return -1;
            var index = Array.IndexOf(playerOrder, uid);
            return index < 0 ? int.MaxValue : index;
        }

        private string FormatEntry(BattlePlayerEntry player)
        {
            var playerName = EscapeRichText(GetPlayerName(player.Uid));
            var nameColor = player.Uid == PlayerManager.CurrentUid ? Constants.ColorYellow : "ffffffff";
            var battleInfo = player.Alive
                ? $"{player.Accuracy.ToString("0.00", CultureInfo.InvariantCulture)}%  {player.Score}"
                : $"<color=#ff5555ff>FAILED</color>  {player.Accuracy.ToString("0.00", CultureInfo.InvariantCulture)}%";

            return $"<color=#{nameColor}>{playerName}</color> — {battleInfo}";
        }

        private static string GetPlayerName(string uid)
        {
            var lobby = LobbyManager.CurrentLobby;
            if (lobby?.PlayerDetails != null)
            {
                foreach (var player in lobby.PlayerDetails)
                {
                    if (player?.Uid == uid && !string.IsNullOrEmpty(player.Name))
                    {
                        return player.Name;
                    }
                }
            }

            if (uid == PlayerManager.CurrentUid && !string.IsNullOrEmpty(PlayerManager.CurrentProfile?.Name))
            {
                return PlayerManager.CurrentProfile.Name;
            }

            return uid ?? "Unknown";
        }

        private void RemoveMissingEntries(IEnumerable<string> activeUids)
        {
            var active = new HashSet<string>(activeUids);
            foreach (var uid in _entryOrder.ToArray())
            {
                if (active.Contains(uid)) continue;

                if (_entries.TryGetValue(uid, out var text) && text != null)
                {
                    UnityEngine.Object.Destroy(text.gameObject);
                }

                _entries.Remove(uid);
                _entryOrder.Remove(uid);
            }
        }

        private void PositionEntries()
        {
            for (var i = 0; i < _entryOrder.Count; i++)
            {
                if (!_entries.TryGetValue(_entryOrder[i], out var text) || text == null) continue;
                var rect = text.GetComponent<RectTransform>();
                rect.anchoredPosition = new Vector2(24f, 30f + EntryHeight * i);
            }
        }

        private void EnsureOverlayOrder()
        {
            if (_frame == null) return;
            var canvas = _frame.GetComponent<Canvas>();
            if (canvas != null) canvas.sortingOrder = OverlaySortingOrder;
        }

        private static void HideInfoPlusLabel()
        {
            var infoPlusLabel = GameObject.Find("InfoPlus_TextLowerLeft");
            if (infoPlusLabel != null)
            {
                infoPlusLabel.SetActive(false);
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
            if (_fontTemplate != null && _fontTemplate.font != null) return _fontTemplate;

            var candidates = Resources.FindObjectsOfTypeAll<Text>();
            foreach (var text in candidates)
            {
                if (text == null || text.font == null) continue;
                var fontName = text.font.name ?? string.Empty;
                if (!fontName.Contains("Arial"))
                {
                    _fontTemplate = text;
                    return _fontTemplate;
                }
            }

            return null;
        }

        private static string EscapeRichText(string value)
        {
            return value?.Replace("<", "＜").Replace(">", "＞") ?? string.Empty;
        }
    }
}
