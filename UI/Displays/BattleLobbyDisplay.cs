using System;
using System.Collections.Generic;
using System.Globalization;
using Il2CppDG.Tweening;
using MDEN.Managers;
using MDEN.Protocol.Enums;
using MDEN.Protocol.Models;
using MDEN.Protocol.Rules;
using MDEN.UI.Core;
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
        private const int MaxMissPopupsPerRefresh = 1;
        private const int MissPopupCooldownFrames = 60;
        private const int StatePopupCooldownFrames = 15;
        private static readonly object ColorLock = new object();
        private static readonly Dictionary<string, string> PlayerColorCache = new Dictionary<string, string>();
        private static readonly HashSet<string> PendingColorRequests = new HashSet<string>();
        private static int ColorGeneration;
        private static GameObject _infoPlusLabel;
        private static int? _infoPlusInstanceId;
        private static bool? _infoPlusWasActive;

        private readonly Dictionary<string, Text> _entries = new Dictionary<string, Text>();
        private readonly Dictionary<string, BattleEntryState> _previousEntries = new Dictionary<string, BattleEntryState>();
        private readonly Dictionary<string, int> _lastPopupFrameByKey = new Dictionary<string, int>();
        private readonly List<string> _entryOrder = new List<string>();
        private readonly List<string> _uidsToRemove = new List<string>();
        private GameObject _frame;
        private Canvas _canvas;
        private int _lastRefreshHash;
        private int _lastRefreshPlayerCount = -1;
        private int _missPopupsThisRefresh;

        public bool IsCreated => _frame != null;

        public static void InvalidatePlayerColors()
        {
            lock (ColorLock)
            {
                PlayerColorCache.Clear();
                PendingColorRequests.Clear();
                unchecked { ColorGeneration++; }
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

            _canvas = _frame.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = OverlaySortingOrder;

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
                DestroyObject(_frame);
                _frame = null;
            }

            _canvas = null;
            RestoreInfoPlusLabel();
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
            var refreshHash = BuildRefreshHash(orderedPlayers);
            if (_lastRefreshPlayerCount == orderedPlayers.Length &&
                _lastRefreshHash == refreshHash &&
                HasAllEntries(orderedPlayers))
            {
                return;
            }

            _missPopupsThisRefresh = 0;
            PrimePlayerColors(orderedPlayers);
            for (var i = 0; i < orderedPlayers.Length; i++)
            {
                var player = orderedPlayers[i];
                SetEntry(player.Uid, FormatEntry(player, i + 1));
                ShowBattlePopupIfNeeded(player);
                _previousEntries[player.Uid] = BattleEntryState.From(player);
            }

            RemoveMissingEntries(orderedPlayers);
            SyncEntryOrder(orderedPlayers);
            PositionEntries();
            _lastRefreshHash = refreshHash;
            _lastRefreshPlayerCount = orderedPlayers.Length;
        }

        public void Clear()
        {
            foreach (var text in _entries.Values)
            {
                DestroyComponentObject(text);
            }

            _entries.Clear();
            _previousEntries.Clear();
            _lastPopupFrameByKey.Clear();
            _entryOrder.Clear();
            _uidsToRemove.Clear();
            _lastRefreshHash = 0;
            _lastRefreshPlayerCount = -1;
            _missPopupsThisRefresh = 0;
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

            if (text.text != value)
            {
                text.text = value;
            }
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
            var validPlayers = new List<BattlePlayerEntry>(players?.Length ?? 0);
            if (players != null)
            {
                for (var i = 0; i < players.Length; i++)
                {
                    var player = players[i];
                    if (player != null && !string.IsNullOrEmpty(player.Uid))
                    {
                        validPlayers.Add(player);
                    }
                }
            }

            validPlayers.Sort((left, right) => ComparePlayers(left, right, goal, playerOrder));
            return validPlayers.ToArray();
        }

        private static int GetPlayerSortIndex(string uid, string[] playerOrder)
        {
            var index = Array.IndexOf(playerOrder, uid);
            return index < 0 ? int.MaxValue : index;
        }

        private static int ComparePlayers(
            BattlePlayerEntry left,
            BattlePlayerEntry right,
            LobbyGoal goal,
            string[] playerOrder)
        {
            var result = CompareBoolDescending(left.Alive, right.Alive);
            if (result != 0) return result;

            if (goal == LobbyGoal.Score)
            {
                result = right.Score.CompareTo(left.Score);
                if (result != 0) return result;

                result = right.Accuracy.CompareTo(left.Accuracy);
                if (result != 0) return result;
            }
            else
            {
                result = right.Accuracy.CompareTo(left.Accuracy);
                if (result != 0) return result;

                result = (IsAp(left) ? GetEarlyLateCount(left) : int.MaxValue)
                    .CompareTo(IsAp(right) ? GetEarlyLateCount(right) : int.MaxValue);
                if (result != 0) return result;

                result = right.Score.CompareTo(left.Score);
                if (result != 0) return result;
            }

            result = GetPlayerSortIndex(left.Uid, playerOrder)
                .CompareTo(GetPlayerSortIndex(right.Uid, playerOrder));
            if (result != 0) return result;

            return StringComparer.Ordinal.Compare(left.Uid, right.Uid);
        }

        private static int CompareBoolDescending(bool left, bool right)
        {
            if (left == right) return 0;
            return left ? -1 : 1;
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
            return $"{FormatResultRank(player, rank)} {FormatResultNameAndInfo(player)}";
        }

        internal static string FormatResultRank(BattlePlayerEntry player, int rank)
        {
            if (LobbyPlayModeRules.IsRookie(LobbyManager.CurrentLobby?.PlayMode ?? 0))
            {
                var difficulty = player?.Difficulty ?? 0;
                if (difficulty > 0)
                {
                    return $"{LobbyRuleTextFormatter.FormatDifficulty(difficulty, true)} {FormatRank(player, rank)}";
                }
            }

            return FormatRank(player, rank);
        }

        internal static string FormatResultNameAndInfo(BattlePlayerEntry player)
        {
            var playerName = EscapeRichText(GetPlayerName(player.Uid));
            var nameColor = GetPlayerColor(player.Uid);
            return $"{ColorText(playerName, nameColor)} — {FormatBattleInfo(player, false)}";
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

            var result = new BattlePlayerEntry[byUid.Count];
            byUid.Values.CopyTo(result, 0);
            return result;
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
                TryPopup(ColorText("失去FC!", Constants.ColorBlue), player.Uid, "fc", StatePopupCooldownFrames, false);
            }
            else if (previous.AP && !IsAp(player))
            {
                TryPopup(ColorText("失去AP!", ColorGold), player.Uid, "ap", StatePopupCooldownFrames, false);
            }
            else if (previous.Alive && !player.Alive)
            {
                TryPopup(ColorText("Down", ColorRed), player.Uid, "down", StatePopupCooldownFrames, false);
            }
            else if (player.Misses > previous.Misses)
            {
                TryPopup("Missed!", player.Uid, "miss", MissPopupCooldownFrames, true);
            }
        }

        private void TryPopup(string value, string uid, string kind, int cooldownFrames, bool isMissPopup)
        {
            if (isMissPopup && _missPopupsThisRefresh >= MaxMissPopupsPerRefresh) return;

            var key = uid + ":" + kind;
            var frame = Time.frameCount;
            if (_lastPopupFrameByKey.TryGetValue(key, out var lastFrame) &&
                frame - lastFrame < cooldownFrames)
            {
                return;
            }

            if (!Popup(value, uid)) return;

            _lastPopupFrameByKey[key] = frame;
            if (isMissPopup)
            {
                _missPopupsThisRefresh++;
            }
        }

        private bool Popup(string value, string uid)
        {
            if (_frame == null || !_entries.TryGetValue(uid, out var owner) || owner == null) return false;

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
            return true;
        }

        private static bool IsAp(BattlePlayerEntry player)
        {
            return player.Alive && Math.Abs(player.Accuracy - 100f) < 0.005f;
        }

        private static bool IsTp(BattlePlayerEntry player)
        {
            return IsAp(player) && player.FC && player.Earlies == 0 && player.Lates == 0;
        }

        private static int GetEarlyLateCount(BattlePlayerEntry player)
        {
            return player.Earlies + player.Lates;
        }

        private static BattlePlayerEntry CreateDefaultBattleEntry(string uid)
        {
            return new BattlePlayerEntry
            {
                Uid = uid,
                Difficulty = LobbyManager.GetCurrentBattleDifficulty(uid),
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

        private static void PrimePlayerColors(BattlePlayerEntry[] players)
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
                unchecked { ColorGeneration++; }
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

        private void RemoveMissingEntries(BattlePlayerEntry[] activePlayers)
        {
            _uidsToRemove.Clear();
            foreach (var uid in _entries.Keys)
            {
                if (ContainsUid(activePlayers, uid)) continue;
                _uidsToRemove.Add(uid);
            }

            for (var i = 0; i < _uidsToRemove.Count; i++)
            {
                var uid = _uidsToRemove[i];
                if (_entries.TryGetValue(uid, out var text) && text != null)
                {
                    DestroyComponentObject(text);
                }

                _entries.Remove(uid);
                _entryOrder.Remove(uid);
                _previousEntries.Remove(uid);
            }

            _uidsToRemove.Clear();
        }

        private void SyncEntryOrder(BattlePlayerEntry[] orderedPlayers)
        {
            _entryOrder.Clear();
            foreach (var player in orderedPlayers)
            {
                var uid = player?.Uid;
                if (!string.IsNullOrEmpty(uid) && _entries.ContainsKey(uid))
                {
                    _entryOrder.Add(uid);
                }
            }
        }

        private bool HasAllEntries(BattlePlayerEntry[] players)
        {
            if (_entries.Count != players.Length) return false;

            for (var i = 0; i < players.Length; i++)
            {
                var uid = players[i]?.Uid;
                if (string.IsNullOrEmpty(uid) || !_entries.ContainsKey(uid)) return false;
            }

            return true;
        }

        private static bool ContainsUid(BattlePlayerEntry[] players, string uid)
        {
            if (string.IsNullOrEmpty(uid)) return false;

            for (var i = 0; i < players.Length; i++)
            {
                if (players[i]?.Uid == uid) return true;
            }

            return false;
        }

        private static int BuildRefreshHash(BattlePlayerEntry[] players)
        {
            unchecked
            {
                var lobby = LobbyManager.CurrentLobby;
                var hash = 17;
                hash = hash * 31 + (lobby?.Goal ?? 0);
                hash = hash * 31 + ColorGeneration;
                hash = hash * 31 + BuildPlayerDetailsHash(lobby?.PlayerDetails);
                for (var i = 0; i < players.Length; i++)
                {
                    var player = players[i];
                    hash = hash * 31 + GetStringHash(player?.Uid);
                    hash = hash * 31 + (player?.Difficulty ?? 0);
                    hash = hash * 31 + (int)(player?.Score ?? 0);
                    hash = hash * 31 + Mathf.RoundToInt((player?.Accuracy ?? 0f) * 1000f);
                    hash = hash * 31 + (player?.Perfects ?? 0);
                    hash = hash * 31 + (player?.Greats ?? 0);
                    hash = hash * 31 + (player?.Earlies ?? 0);
                    hash = hash * 31 + (player?.Lates ?? 0);
                    hash = hash * 31 + (player?.Misses ?? 0);
                    hash = hash * 31 + ((player?.FC ?? false) ? 1 : 0);
                    hash = hash * 31 + ((player?.Alive ?? false) ? 1 : 0);
                }

                return hash;
            }
        }

        private static int BuildPlayerDetailsHash(PlayerSyncEntry[] details)
        {
            if (details == null || details.Length == 0) return 0;

            unchecked
            {
                var hash = 17;
                for (var i = 0; i < details.Length; i++)
                {
                    var player = details[i];
                    hash = hash * 31 + GetStringHash(player?.Uid);
                    hash = hash * 31 + GetStringHash(player?.Name);
                    hash = hash * 31 + GetStringHash(player?.ChatColor);
                    hash = hash * 31 + GetStringHash(player?.AvatarName);
                }

                return hash;
            }
        }

        private static int GetStringHash(string value)
        {
            return string.IsNullOrEmpty(value) ? 0 : StringComparer.Ordinal.GetHashCode(value);
        }

        private void PositionEntries()
        {
            for (var i = 0; i < _entryOrder.Count; i++)
            {
                if (!_entries.TryGetValue(_entryOrder[i], out var text) || text == null) continue;
                var rect = text.GetComponent<RectTransform>();
                var position = new Vector2(24f, 30f + EntryHeight * (_entryOrder.Count - 1 - i));
                if (rect.anchoredPosition != position)
                {
                    rect.anchoredPosition = position;
                }
            }
        }

        private static void DestroyObject(GameObject obj)
        {
            if (obj == null) return;
            UnityEngine.Object.Destroy(obj);
        }

        private static void DestroyComponentObject(Component component)
        {
            if (component == null) return;
            DestroyObject(component.gameObject);
        }

        private void EnsureOverlayOrder()
        {
            if (_canvas != null) _canvas.sortingOrder = OverlaySortingOrder;
        }

        private static void HideInfoPlusLabel()
        {
            var infoPlusLabel = FindInfoPlusLabel();
            if (infoPlusLabel != null)
            {
                var instanceId = infoPlusLabel.GetInstanceID();
                if (_infoPlusInstanceId != instanceId)
                {
                    _infoPlusInstanceId = instanceId;
                    _infoPlusWasActive = infoPlusLabel.activeSelf;
                }

                infoPlusLabel.SetActive(false);
            }
        }

        private static void RestoreInfoPlusLabel()
        {
            var infoPlusLabel = FindInfoPlusLabel();
            if (infoPlusLabel != null && _infoPlusWasActive.HasValue)
            {
                infoPlusLabel.SetActive(_infoPlusWasActive.Value);
            }

            _infoPlusLabel = null;
            _infoPlusInstanceId = null;
            _infoPlusWasActive = null;
        }

        private static GameObject FindInfoPlusLabel()
        {
            if (_infoPlusLabel == null)
            {
                _infoPlusLabel = GameObject.Find("InfoPlus_TextLowerLeft");
            }

            return _infoPlusLabel;
        }

        private static void ApplyGameFont(Text text)
        {
            NativeFontCache.ApplyTo(text);
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
