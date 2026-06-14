using System.Collections.Generic;
using MDEN.Managers;
using MDEN.Protocol.Messages.Lobby;
using MDEN.Protocol.Models;
using UnityEngine;
using UnityEngine.UI;

namespace MDEN.UI.Core
{
    public static class RoomSceneOverlay
    {
        private static readonly Dictionary<string, bool> OriginalStates = new Dictionary<string, bool>();
        private static GameObject _frame;
        private static Text _title;
        private static Text _status;
        private static Text _players;

        private static readonly string[] HiddenObjectPaths =
        {
            "UI/Standerd/PnlHome/PnlBgSwitchFsv",
            "UI/Standerd/PnlHome/ElfinShow",
            "UI/Standerd/PnlHome/MuseShow/BtnInteraction"
        };

        public static void Refresh(LobbySyncPush lobby)
        {
            if (lobby == null)
            {
                Destroy();
                return;
            }

            EnsureFrame();
            if (_frame == null) return;

            HideSinglePlayerControls();
            EnsureHomeStartButtonVisible();
            _frame.SetActive(true);
            _title.text = lobby.Name;
            _status.text =
                $"房主: {GetHostName(lobby)}\n" +
                $"人数: <color=#{Constants.ColorYellow}>{GetPlayerCount(lobby)}/{lobby.MaxPlayers}</color>\n" +
                $"状态: {(lobby.IsPlaying ? "游戏中" : "等待中")}";
            _players.text = FormatPlayers(lobby);
        }

        public static void Destroy()
        {
            RestoreSinglePlayerControls();

            if (_frame != null)
            {
                UnityEngine.Object.Destroy(_frame);
                _frame = null;
                _title = null;
                _status = null;
                _players = null;
            }
        }

        private static void EnsureFrame()
        {
            if (_frame != null) return;

            var parent = GameObject.Find("UI/Standerd/PnlHome")?.transform
                ?? GameObject.Find("UI/Standerd")?.transform;
            if (parent == null) return;

            _frame = new GameObject("MDENRoomSceneOverlay");
            var rect = _frame.AddComponent<RectTransform>();
            rect.SetParent(parent);
            rect.localScale = Vector3.one;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(565f, 190f);
            rect.sizeDelta = new Vector2(420f, 160f);

            var image = _frame.AddComponent<Image>();
            image.color = new Color(0.22f, 0.08f, 0.45f, 0.72f);

            _title = CreateText("Title", 0f, 52f, 32, TextAnchor.MiddleCenter);
            _status = CreateText("Status", -185f, 8f, 20, TextAnchor.UpperLeft);
            _players = CreateText("Players", 5f, 8f, 18, TextAnchor.UpperLeft);
        }

        private static Text CreateText(string name, float x, float y, int fontSize, TextAnchor anchor)
        {
            var obj = new GameObject(name);
            var rect = obj.AddComponent<RectTransform>();
            rect.SetParent(_frame.transform);
            rect.localScale = Vector3.one;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(x, y);
            rect.sizeDelta = new Vector2(500f, fontSize * 5f);

            var text = obj.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            text.fontSize = fontSize;
            text.alignment = anchor;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.supportRichText = true;
            text.color = Color.white;
            return text;
        }

        private static void HideSinglePlayerControls()
        {
            foreach (var path in HiddenObjectPaths)
            {
                var obj = GameObject.Find(path);
                if (obj == null) continue;

                if (!OriginalStates.ContainsKey(path))
                {
                    OriginalStates[path] = obj.activeSelf;
                }

                obj.SetActive(false);
            }
        }

        private static void EnsureHomeStartButtonVisible()
        {
            var startButton = GameObject.Find("UI/Standerd/PnlHome/Bottom/Btn");
            if (startButton != null && !startButton.activeSelf)
            {
                startButton.SetActive(true);
            }
        }

        private static void RestoreSinglePlayerControls()
        {
            foreach (var state in OriginalStates)
            {
                var obj = GameObject.Find(state.Key);
                if (obj != null)
                {
                    obj.SetActive(state.Value);
                }
            }

            OriginalStates.Clear();
        }

        private static string FormatPlayers(LobbySyncPush lobby)
        {
            if (lobby.PlayerDetails == null || lobby.PlayerDetails.Length == 0)
            {
                return "玩家列表同步中...";
            }

            var lines = new List<string>();
            foreach (var player in lobby.PlayerDetails)
            {
                if (string.IsNullOrEmpty(player?.Uid)) continue;
                var name = string.IsNullOrEmpty(player.Name) ? player.Uid : player.Name;
                var prefix = player.Uid == lobby.HostUid ? $"<color=#{Constants.ColorYellow}>[Host]</color> " : string.Empty;
                lines.Add($"{prefix}{name}  <color=#{Constants.ColorCyan}>{player.PingMS}ms</color>");
            }

            return string.Join("\n", lines);
        }

        private static string GetHostName(LobbySyncPush lobby)
        {
            if (!string.IsNullOrEmpty(lobby.HostName)) return lobby.HostName;
            if (lobby.PlayerDetails != null)
            {
                foreach (var player in lobby.PlayerDetails)
                {
                    if (player?.Uid == lobby.HostUid && !string.IsNullOrEmpty(player.Name))
                    {
                        return player.Name;
                    }
                }
            }

            return lobby.HostUid;
        }

        private static int GetPlayerCount(LobbySyncPush lobby)
        {
            if (lobby.PlayerDetails != null && lobby.PlayerDetails.Length > 0) return lobby.PlayerDetails.Length;
            return lobby.Players?.Length ?? 0;
        }
    }
}
