using System;
using MDEN.Managers;
using MDEN.Protocol.Messages.Lobby;
using MDEN.UI.Core;
using MelonLoader;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace MDEN.UI.Displays
{
    public sealed class RoomReadyDisplay
    {
        private GameObject _root;
        private Text _title;
        private Text _status;
        private Text _buttonText;
        private Button _button;
        private bool _busy;

        public void Refresh(LobbySyncPush lobby)
        {
            if (lobby == null || !lobby.Locked)
            {
                Destroy();
                return;
            }

            EnsureCreated();
            if (_root == null) return;

            var entry = ChartManager.ParseEntry(lobby.Playlist != null && lobby.Playlist.Length > 0 ? lobby.Playlist[0] : null);
            _root.SetActive(true);
            _title.text = entry == null ? "等待歌曲" : entry.DisplayName;
            _status.text = lobby.IsPlaying
                ? "游戏中"
                : $"准备: {lobby.ReadyPlayers?.Length ?? 0}/{lobby.Players?.Length ?? 0}";
            _buttonText.text = GetButtonText(lobby);
            _button.enabled = !_busy && !lobby.IsPlaying;
        }

        public void Destroy()
        {
            _busy = false;
            _title = null;
            _status = null;
            _buttonText = null;
            _button = null;

            if (_root != null)
            {
                UnityEngine.Object.Destroy(_root);
                _root = null;
            }
        }

        private void EnsureCreated()
        {
            if (_root != null) return;

            var parent = GameObject.Find("UI/Standerd")?.transform;
            if (parent == null) return;

            _root = new GameObject("MDENRoomReadyDisplay");
            var rect = _root.AddComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.localScale = Vector3.one;
            rect.anchorMin = new Vector2(1f, 0.5f);
            rect.anchorMax = new Vector2(1f, 0.5f);
            rect.pivot = new Vector2(1f, 0.5f);
            rect.anchoredPosition = new Vector2(-28f, -80f);
            rect.sizeDelta = new Vector2(360f, 168f);

            var image = _root.AddComponent<Image>();
            image.color = new Color(0.10f, 0.03f, 0.20f, 0.76f);

            _title = CreateText(_root.transform, "Title", new Vector2(0f, 42f), new Vector2(320f, 54f), 22, TextAnchor.MiddleCenter);
            _status = CreateText(_root.transform, "Status", new Vector2(0f, 4f), new Vector2(320f, 34f), 20, TextAnchor.MiddleCenter);
            CreateButton();
        }

        private Text CreateText(Transform parent, string name, Vector2 position, Vector2 size, int fontSize, TextAnchor anchor)
        {
            var obj = new GameObject(name);
            var rect = obj.AddComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.localScale = Vector3.one;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;

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

        private void CreateButton()
        {
            var obj = new GameObject("ReadyButton");
            var rect = obj.AddComponent<RectTransform>();
            rect.SetParent(_root.transform, false);
            rect.localScale = Vector3.one;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(0f, -52f);
            rect.sizeDelta = new Vector2(260f, 46f);

            var image = obj.AddComponent<Image>();
            image.color = new Color(1f, 0.92f, 0.08f, 0.92f);

            _button = obj.AddComponent<Button>();
            _button.onClick.AddListener((UnityAction)new Action(OnReadyClicked));
            _buttonText = CreateText(obj.transform, "ReadyText", Vector2.zero, new Vector2(250f, 42f), 22, TextAnchor.MiddleCenter);
            _buttonText.color = new Color(0.18f, 0.04f, 0.34f, 1f);
        }

        private async void OnReadyClicked()
        {
            if (_busy || LobbyManager.CurrentLobby?.IsPlaying == true) return;

            _busy = true;
            Refresh(LobbyManager.CurrentLobby);
            using var _ = UIManager.LockUI("Setting ready...");

            try
            {
                await PlaylistManager.SetReadyAsync(!PlaylistManager.IsLocalPlayerReady());
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Set ready failed: {ex.Message}");
            }
            finally
            {
                _busy = false;
                MainThreadDispatcher.Enqueue(() => Refresh(LobbyManager.CurrentLobby));
            }
        }

        private static string GetButtonText(LobbySyncPush lobby)
        {
            if (lobby.IsPlaying) return "游戏中";
            return PlaylistManager.IsLocalPlayerReady() ? "取消准备" : "准备";
        }
    }
}
