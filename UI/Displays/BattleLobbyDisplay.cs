using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Il2CppDG.Tweening;
using MDEN.Managers;
using MDEN.Protocol.Enums;
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
        private const string ColorGold = "fff700ff";
        private const string ColorSilver = "d8d8e8ff";
        private const string ColorPurple = "9b55ffff";
        private const string ColorRed = "ff5555ff";
        private const string ColorWhite = "ffffffff";
        private static Text _fontTemplate;
        private static readonly object ColorLock = new object();
        private static readonly Dictionary<string, string> PlayerColorCache = new Dictionary<string, string>();
        private static readonly HashSet<string> PendingColorRequests = new HashSet<string>();

        private readonly Dictionary<string, Text> _entries = new Dictionary<string, Text>();
        private readonly Dictionary<string, BattleEntryState> _previousEntries = new Dictionary<string, BattleEntryState>();
        private readonly List<string> _entryOrder = new List<string>();
        private GameObject _frame;

        public bool IsCreated => _frame != null;

        public static void InvalidatePlayerColors()
        {
            lock (ColorLock)
            {
                PlayerColorCache.Clear();
                PendingColorRequests.Clear();
            }
        }

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

            var orderedPlayers = OrderPlayers(WithLobbyDefaults(players ?? Array.Empty<BattlePlayerEntry>()));
            PrimePlayerColors(orderedPlayers);
            for (var i = 0; i < orderedPlayers.Length; i++)
            {
                var player = orderedPlayers[i];
                SetEntry(player.Uid, FormatEntry(player, i + 1));
                ShowBattlePopupIfNeeded(player);
                _previousEntries[player.Uid] = BattleEntryState.From(player);
            }

            var activeUids = orderedPlayers.Select(player => player.Uid).ToArray();
            RemoveMissingEntries(activeUids);
            SyncEntryOrder(activeUids);
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
            _previousEntries.Clear();
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

        internal static BattlePlayerEntry[] OrderPlayers(BattlePlayerEntry[] players)
        {
            var lobby = LobbyManager.CurrentLobby;
            var playerOrder = lobby?.Players ?? Array.Empty<string>();
            var goal = (LobbyGoal)(lobby?.Goal ?? (byte)LobbyGoal.Accuracy);
            var validPlayers = players
                .Where(player => player != null && !string.IsNullOrEmpty(player.Uid))
                .ToArray();

            if (goal == LobbyGoal.Score)
            {
                return validPlayers
                    .OrderByDescending(player => player.Alive)
                    .ThenByDescending(player => player.Score)
                    .ThenByDescending(player => player.Accuracy)
                    .ThenBy(player => GetPlayerSortIndex(player.Uid, playerOrder))
                    .ToArray();
            }

            return validPlayers
                .OrderByDescending(player => player.Alive)
                .ThenByDescending(player => player.Accuracy)
                .ThenByDescending(player => player.Score)
                .ThenBy(player => GetPlayerSortIndex(player.Uid, playerOrder))
                .ToArray();
        }

        private static int GetPlayerSortIndex(string uid, string[] playerOrder)
        {
            var index = Array.IndexOf(playerOrder, uid);
            return index < 0 ? int.MaxValue : index;
        }

        private string FormatEntry(BattlePlayerEntry player, int rank)
        {
            var playerName = EscapeRichText(GetPlayerName(player.Uid));
            var nameColor = GetPlayerColor(player.Uid);
            var battleInfo = FormatBattleInfo(player, false);

            return $"{FormatRank(player, rank)} {ColorText(playerName, nameColor)} — {battleInfo}";
        }

        internal static string FormatResultEntry(BattlePlayerEntry player, int rank)
        {
            var playerName = EscapeRichText(GetPlayerName(player.Uid));
            var nameColor = GetPlayerColor(player.Uid);
            return $"{FormatRank(player, rank)} {ColorText(playerName, nameColor)} — {FormatResultAccuracy(player)}";
        }

        internal static BattlePlayerEntry[] WithLobbyDefaults(BattlePlayerEntry[] players)
        {
            var byUid = new Dictionary<string, BattlePlayerEntry>();
            foreach (var player in players ?? Array.Empty<BattlePlayerEntry>())
            {
                if (player == null || string.IsNullOrEmpty(player.Uid)) continue;
                byUid[player.Uid] = player;
            }

            var lobby = LobbyManager.CurrentLobby;
            var lobbyPlayers = lobby?.Players ?? Array.Empty<string>();
            foreach (var uid in lobbyPlayers)
            {
                if (string.IsNullOrEmpty(uid) || byUid.ContainsKey(uid)) continue;
                byUid[uid] = CreateDefaultBattleEntry(uid);
            }

            return byUid.Values.ToArray();
        }

        private static string FormatBattleInfo(BattlePlayerEntry player, bool forceAccuracy)
        {
            if (!player.Alive)
            {
                return ColorText("Down", ColorRed);
            }

            var lobby = LobbyManager.CurrentLobby;
            var goal = (LobbyGoal)(lobby?.Goal ?? (byte)LobbyGoal.Accuracy);
            if (!forceAccuracy && goal == LobbyGoal.Score)
            {
                return ColorText(player.Score.ToString(), Constants.ColorYellow);
            }

            if (IsTp(player))
            {
                return ColorText("TP", ColorRed);
            }

            if (IsAp(player))
            {
                return $"{ColorText("AP", ColorGold)}{FormatJudgementSuffix(player)}";
            }

            var accuracy = player.Accuracy.ToString("0.00", CultureInfo.InvariantCulture);
            var accuracyText = ColorText($"{accuracy}%", GetAccuracyColor(player.Accuracy));
            var result = player.FC ? $"{ColorText("FC", Constants.ColorBlue)} {accuracyText}" : accuracyText;
            return result + FormatJudgementSuffix(player);
        }

        private static string FormatResultAccuracy(BattlePlayerEntry player)
        {
            if (!player.Alive)
            {
                return ColorText("Down", ColorRed);
            }

            if (IsTp(player))
            {
                return ColorText("TP", ColorRed);
            }

            if (IsAp(player))
            {
                return $"{ColorText("AP", ColorGold)}{FormatJudgementSuffix(player)}";
            }

            var accuracy = player.Accuracy.ToString("0.00", CultureInfo.InvariantCulture);
            var accuracyText = ColorText($"{accuracy}%", GetAccuracyColor(player.Accuracy));
            var result = player.FC ? $"{ColorText("FC", Constants.ColorBlue)} {accuracyText}" : accuracyText;
            return result + FormatJudgementSuffix(player);
        }

        internal static string GetPlayerName(string uid)
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

        private void ShowBattlePopupIfNeeded(BattlePlayerEntry player)
        {
            if (player == null || string.IsNullOrEmpty(player.Uid)) return;
            if (!_previousEntries.TryGetValue(player.Uid, out var previous)) return;

            if (previous.FC && !player.FC)
            {
                Popup(ColorText("失去FC!", Constants.ColorBlue), player.Uid);
            }
            else if (previous.AP && !IsAp(player))
            {
                Popup(ColorText("失去AP!", ColorGold), player.Uid);
            }
            else if (previous.Alive && !player.Alive)
            {
                Popup(ColorText("Down", ColorRed), player.Uid);
            }
            else if (player.Misses > previous.Misses)
            {
                Popup("Missed!", player.Uid);
            }
        }

        private void Popup(string value, string uid)
        {
            if (_frame == null || !_entries.TryGetValue(uid, out var owner) || owner == null) return;

            var popup = UnityEngine.Object.Instantiate(owner.gameObject, _frame.transform);
            popup.name = "Popup_" + uid;

            var popupText = popup.GetComponent<Text>();
            popupText.text = value;

            var rect = popup.GetComponent<RectTransform>();
            var ownerRect = owner.GetComponent<RectTransform>();
            rect.anchoredPosition = ownerRect.anchoredPosition + new Vector2(owner.preferredWidth + 10f, 0f);
            rect.DOMoveX(50f, 1.5f).SetRelative().SetEase(Ease.OutSine).OnComplete((Action)(() =>
            {
                if (popup != null) UnityEngine.Object.Destroy(popup);
            }));
        }

        private static bool IsAp(BattlePlayerEntry player)
        {
            return player.Alive && Math.Abs(player.Accuracy - 100f) < 0.005f;
        }

        private static bool IsTp(BattlePlayerEntry player)
        {
            return IsAp(player) && player.FC && player.Earlies == 0 && player.Lates == 0;
        }

        private static BattlePlayerEntry CreateDefaultBattleEntry(string uid)
        {
            return new BattlePlayerEntry
            {
                Uid = uid,
                Accuracy = 0f,
                FC = false,
                Alive = true
            };
        }

        private static string GetPlayerColor(string uid)
        {
            if (uid == PlayerManager.CurrentUid)
            {
                return Constants.ColorPink;
            }

            if (!string.IsNullOrWhiteSpace(uid))
            {
                var lobbyColor = GetLobbyPlayerColor(uid);
                if (!string.IsNullOrEmpty(lobbyColor))
                {
                    lock (ColorLock)
                    {
                        PlayerColorCache[uid] = lobbyColor;
                    }

                    return lobbyColor;
                }

                lock (ColorLock)
                {
                    if (PlayerColorCache.TryGetValue(uid, out var cachedColor))
                    {
                        return cachedColor;
                    }
                }

                RequestPlayerColor(uid);
            }

            return ColorWhite;
        }

        private static string GetLobbyPlayerColor(string uid)
        {
            var details = LobbyManager.CurrentLobby?.PlayerDetails;
            if (details == null) return null;

            foreach (var player in details)
            {
                if (player?.Uid != uid) continue;
                return NormalizeHexColor(player.ChatColor);
            }

            return null;
        }

        private static string FormatRank(BattlePlayerEntry player, int rank)
        {
            var text = $"#{rank}";
            return player?.Uid == PlayerManager.CurrentUid
                ? ColorText(text, Constants.ColorPink)
                : text;
        }

        private static string FormatJudgementSuffix(BattlePlayerEntry player)
        {
            if (player == null) return string.Empty;

            if (IsAp(player))
            {
                var suffix = string.Empty;
                if (player.Earlies > 0) suffix += $" {ColorText($"{player.Earlies}E", Constants.ColorBlue)}";
                if (player.Lates > 0) suffix += $" {ColorText($"{player.Lates}L", Constants.ColorPink)}";
                return suffix;
            }

            var result = string.Empty;
            if (player.Misses > 0) result += $" {player.Misses}M";
            if (player.Greats > 0) result += $" {player.Greats}G";
            return result;
        }

        private static void PrimePlayerColors(IEnumerable<BattlePlayerEntry> players)
        {
            foreach (var player in players)
            {
                if (player == null || string.IsNullOrWhiteSpace(player.Uid)) continue;
                GetPlayerColor(player.Uid);
            }
        }

        private static async void RequestPlayerColor(string uid)
        {
            lock (ColorLock)
            {
                if (PendingColorRequests.Contains(uid)) return;
                PendingColorRequests.Add(uid);
            }

            var resolvedColor = ColorWhite;
            try
            {
                var profile = await PlayerManager.GetProfileAsync(uid);
                var color = NormalizeHexColor(profile?.ChatColor);
                resolvedColor = string.IsNullOrEmpty(color) ? ColorWhite : color;
            }
            catch
            {
                resolvedColor = ColorWhite;
            }

            lock (ColorLock)
            {
                PlayerColorCache[uid] = resolvedColor;
                PendingColorRequests.Remove(uid);
            }
        }

        private static string GetAccuracyColor(float accuracy)
        {
            if (accuracy >= 95f) return ColorSilver;
            if (accuracy >= 90f) return Constants.ColorPink;
            if (accuracy >= 80f) return ColorPurple;
            return Constants.ColorBlue;
        }

        private static string ColorText(string value, string color)
        {
            return $"<color=#{color}>{value}</color>";
        }

        private void RemoveMissingEntries(IEnumerable<string> activeUids)
        {
            var active = new HashSet<string>(activeUids);
            foreach (var uid in _entries.Keys.ToArray())
            {
                if (active.Contains(uid)) continue;

                if (_entries.TryGetValue(uid, out var text) && text != null)
                {
                    UnityEngine.Object.Destroy(text.gameObject);
                }

                _entries.Remove(uid);
                _entryOrder.Remove(uid);
                _previousEntries.Remove(uid);
            }
        }

        private void SyncEntryOrder(IEnumerable<string> orderedUids)
        {
            _entryOrder.Clear();
            foreach (var uid in orderedUids)
            {
                if (!string.IsNullOrEmpty(uid) && _entries.ContainsKey(uid))
                {
                    _entryOrder.Add(uid);
                }
            }
        }

        private void PositionEntries()
        {
            for (var i = 0; i < _entryOrder.Count; i++)
            {
                if (!_entries.TryGetValue(_entryOrder[i], out var text) || text == null) continue;
                var rect = text.GetComponent<RectTransform>();
                rect.anchoredPosition = new Vector2(24f, 30f + EntryHeight * (_entryOrder.Count - 1 - i));
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

        private static string NormalizeHexColor(string color)
        {
            if (string.IsNullOrWhiteSpace(color)) return null;

            var value = color.Trim().TrimStart('#');
            if (value.Length == 6) value += "ff";
            if (value.Length != 8) return null;

            for (var i = 0; i < value.Length; i++)
            {
                if (!Uri.IsHexDigit(value[i])) return null;
            }

            return value;
        }

        private sealed class BattleEntryState
        {
            public bool AP { get; private set; }
            public bool FC { get; private set; }
            public bool Alive { get; private set; }
            public ushort Misses { get; private set; }

            public static BattleEntryState From(BattlePlayerEntry player)
            {
                return new BattleEntryState
                {
                    AP = IsAp(player),
                    FC = player.FC,
                    Alive = player.Alive,
                    Misses = player.Misses
                };
            }
        }
    }
}
