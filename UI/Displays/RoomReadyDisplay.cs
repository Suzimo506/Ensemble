using System;
using Il2CppAssets.Scripts.PeroTools.Nice.Events;
using MDEN.Managers;
using MDEN.Protocol.Messages.Lobby;
using MelonLoader;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Events;
using UnityEngine.UI;
using MDEN.UI.Core;

namespace MDEN.UI.Displays
{
    public sealed class RoomReadyDisplay
    {
        private GameObject _notification;
        private GameObject _imgBase;
        private GameObject _buttonMain;
        private GameObject _buttonStop;
        private Text _buttonMainText;
        private Text _buttonStopText;
        private Text _message;
        private bool _busy;
        private bool _stopBusy;

        private void EnsureCreated()
        {
            if (_notification != null) return;

            var pnlCloudMessage = GameObject.Find("UI/Standerd/PnlCloudMessage");
            if (pnlCloudMessage == null) return;

            _notification = UnityEngine.Object.Instantiate(pnlCloudMessage);
            _notification.transform.SetParent(null);
            _notification.name = "MDENReadyNotification";
            _notification.SetActive(false);

            var notifRect = _notification.GetComponent<RectTransform>();
            notifRect.anchorMin = Vector2.zero;
            notifRect.anchorMax = Vector2.one;
            notifRect.sizeDelta = Vector2.zero;
            notifRect.anchoredPosition3D = Vector3.zero;
            notifRect.localScale = Vector3.one;

            var nestedCanvas = _notification.GetComponent<Canvas>();
            if (nestedCanvas == null) nestedCanvas = _notification.AddComponent<Canvas>();
            nestedCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            nestedCanvas.sortingOrder = 32767;

            var scaler = _notification.GetComponent<CanvasScaler>();
            if (scaler == null) scaler = _notification.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            var nestedRaycaster = _notification.GetComponent<GraphicRaycaster>();
            if (nestedRaycaster == null) _notification.AddComponent<GraphicRaycaster>();

            _imgBase = _notification.transform.Find("ImgBase").gameObject;

            var baseRect = _imgBase.GetComponent<RectTransform>();
            baseRect.anchorMin = new Vector2(1f, 0.5f);
            baseRect.anchorMax = new Vector2(1f, 0.5f);
            baseRect.pivot = new Vector2(1f, 0.5f);
            baseRect.anchoredPosition3D = new Vector3(-30f, 150f, 0f);
            baseRect.sizeDelta = new Vector2(350f, 370f);
            baseRect.localScale = Vector3.one;

            var messageBase = _imgBase.transform.Find("Synchronizing").GetComponent<RectTransform>();
            messageBase.pivot = new Vector2(0.5f, 1f);
            messageBase.anchorMin = messageBase.pivot;
            messageBase.anchorMax = messageBase.pivot;
            messageBase.gameObject.SetActive(true);

            _message = messageBase.Find("TxtSynchronizing").GetComponent<Text>();
            _message.alignment = TextAnchor.UpperRight;
            _message.verticalOverflow = VerticalWrapMode.Overflow;

            Sprite sprRoundedSquare = null;
            try
            {
                sprRoundedSquare = Addressables.LoadAssetAsync<Sprite>("SprRoundedsquare").WaitForCompletion();
            }
            catch
            {
                // ignored
            }

            _buttonMain = new GameObject("BtnMain");
            var buttonMainRect = _buttonMain.AddComponent<RectTransform>();
            buttonMainRect.SetParent(_imgBase.transform, false);
            buttonMainRect.anchoredPosition3D = new Vector3(-10f, 10f, 0f);
            buttonMainRect.pivot = new Vector2(1f, 0f);
            buttonMainRect.anchorMin = buttonMainRect.pivot;
            buttonMainRect.anchorMax = buttonMainRect.pivot;
            buttonMainRect.localScale = Vector3.one;
            buttonMainRect.sizeDelta = new Vector2(300f, 60f);

            var buttonMainImg = _buttonMain.AddComponent<Image>();
            buttonMainImg.sprite = sprRoundedSquare;
            buttonMainImg.type = Image.Type.Tiled;

            var button = _buttonMain.AddComponent<Button>();
            button.onClick.AddListener((UnityAction)OnReadyClicked);

            var buttonMainTextGo = new GameObject("BtnTxt");
            var buttonMainTextRect = buttonMainTextGo.AddComponent<RectTransform>();
            buttonMainTextRect.SetParent(buttonMainRect.transform, false);
            buttonMainTextRect.anchoredPosition3D = Vector3.zero;
            buttonMainTextRect.localScale = Vector3.one;
            buttonMainTextRect.sizeDelta = buttonMainRect.sizeDelta;

            _buttonMainText = buttonMainTextGo.AddComponent<Text>();
            _buttonMainText.alignment = TextAnchor.MiddleCenter;
            _buttonMainText.fontSize = 28;
            _buttonMainText.horizontalOverflow = HorizontalWrapMode.Overflow;
            _buttonMainText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");

            _buttonStop = new GameObject("BtnStop");
            var buttonStopRect = _buttonStop.AddComponent<RectTransform>();
            buttonStopRect.SetParent(_imgBase.transform, false);
            buttonStopRect.anchoredPosition3D = new Vector3(-10f, 80f, 0f);
            buttonStopRect.pivot = new Vector2(1f, 0f);
            buttonStopRect.anchorMin = buttonStopRect.pivot;
            buttonStopRect.anchorMax = buttonStopRect.pivot;
            buttonStopRect.localScale = Vector3.one;
            buttonStopRect.sizeDelta = new Vector2(300f, 54f);

            var buttonStopImg = _buttonStop.AddComponent<Image>();
            buttonStopImg.sprite = sprRoundedSquare;
            buttonStopImg.type = Image.Type.Tiled;
            buttonStopImg.color = new Color(0.85f, 0.08f, 0.08f, 1f);

            var stopButton = _buttonStop.AddComponent<Button>();
            stopButton.onClick.AddListener((UnityAction)OnStopClicked);

            var buttonStopTextGo = new GameObject("BtnTxt");
            var buttonStopTextRect = buttonStopTextGo.AddComponent<RectTransform>();
            buttonStopTextRect.SetParent(buttonStopRect.transform, false);
            buttonStopTextRect.anchoredPosition3D = Vector3.zero;
            buttonStopTextRect.localScale = Vector3.one;
            buttonStopTextRect.sizeDelta = buttonStopRect.sizeDelta;

            _buttonStopText = buttonStopTextGo.AddComponent<Text>();
            _buttonStopText.alignment = TextAnchor.MiddleCenter;
            _buttonStopText.fontSize = 26;
            _buttonStopText.horizontalOverflow = HorizontalWrapMode.Overflow;
            _buttonStopText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            _buttonStopText.text = "停止游戏";
            _buttonStopText.color = Color.white;

            UnityEngine.Object.Destroy(_imgBase.transform.Find("Synchronizing/TxtSynchronizing/ImgSynchronizing")?.gameObject);
            UnityEngine.Object.Destroy(_imgBase.transform.Find("SynchronizingFail")?.gameObject);
            var closeObj = _imgBase.transform.Find("SynchronizingCompleted");
            if (closeObj != null)
            {
                UnityEngine.Object.Destroy(closeObj.gameObject);
            }

            var customEventMsg = messageBase.GetComponent<OnCustomEvent>();
            if (customEventMsg != null) UnityEngine.Object.Destroy(customEventMsg);
        }

        public void Refresh(LobbySyncPush lobby)
        {
            if (lobby == null || !lobby.Locked)
            {
                Destroy();
                return;
            }

            EnsureCreated();
            if (_notification == null) return;

            _notification.SetActive(true);

            var entry = ChartManager.ParseEntry(lobby.Playlist != null && lobby.Playlist.Length > 0 ? lobby.Playlist[0] : null);
            var chartTitle = entry == null ? "等待歌曲" : entry.DisplayName;
            _message.text = $"<color=#F8DC51>Next:</color>\n{chartTitle}";

            bool isReady = PlaylistManager.IsLocalPlayerReady();
            _buttonMainText.text = lobby.IsPlaying ? "游戏中" : (isReady ? $"{lobby.ReadyPlayers?.Length ?? 0} / {lobby.Players?.Length ?? 0}" : "准备");
            
            Color bgColor = (isReady || lobby.IsPlaying) ? new Color(0.5f, 0.5f, 0.5f, 1f) : new Color(0f, 0.82f, 0.28f, 1f);
            Color fgColor = (isReady || lobby.IsPlaying) ? new Color(0.6f, 0.6f, 0.6f, 1f) : new Color(0.536f, 1f, 0.05f, 1f);

            _buttonMainText.color = fgColor;
            _buttonMain.GetComponent<Image>().color = bgColor;
            _buttonMain.GetComponent<Button>().interactable = !_busy && !lobby.IsPlaying;

            bool canStop = lobby.HostUid == PlayerManager.CurrentUid;
            _buttonStop.GetComponent<Button>().interactable = canStop && !_stopBusy;
            _buttonStop.GetComponent<Image>().color = canStop
                ? new Color(0.85f, 0.08f, 0.08f, 1f)
                : new Color(0.42f, 0.24f, 0.24f, 1f);
            _buttonStopText.color = canStop ? Color.white : new Color(0.68f, 0.68f, 0.68f, 1f);
        }

        public void Update()
        {
            // The Cloud Message is static overlay, no sliding update needed
        }

        public void Destroy()
        {
            _busy = false;
            _stopBusy = false;
            
            if (_buttonMain != null)
            {
                var btn = _buttonMain.GetComponent<Button>();
                if (btn != null) btn.onClick.RemoveAllListeners();
            }

            if (_buttonStop != null)
            {
                var btn = _buttonStop.GetComponent<Button>();
                if (btn != null) btn.onClick.RemoveAllListeners();
            }

            if (_notification != null)
            {
                UnityEngine.Object.Destroy(_notification);
                _notification = null;
            }

            _imgBase = null;
            _buttonMain = null;
            _buttonStop = null;
            _buttonMainText = null;
            _buttonStopText = null;
            _message = null;
        }

        private async void OnReadyClicked()
        {
            if (_busy || LobbyManager.CurrentLobby?.IsPlaying == true) return;

            _busy = true;
            Refresh(LobbyManager.CurrentLobby);

            try
            {
                using var _ = UIManager.LockUI("Setting ready...");
                await PlaylistManager.SetReadyAsync(!PlaylistManager.IsLocalPlayerReady());
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Set ready failed: {ex.Message}");
            }
            finally
            {
                _busy = false;
                // Dispatch back to main thread to update UI
                MainThreadDispatcher.Enqueue(() => Refresh(LobbyManager.CurrentLobby));
            }
        }

        private async void OnStopClicked()
        {
            if (_stopBusy || LobbyManager.CurrentLobby?.HostUid != PlayerManager.CurrentUid) return;

            _stopBusy = true;
            Refresh(LobbyManager.CurrentLobby);

            try
            {
                using var _ = UIManager.LockUI("Stopping lobby...");
                await PlaylistManager.StopLobbyAsync();
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Stop lobby failed: {ex.Message}");
            }
            finally
            {
                _stopBusy = false;
                MainThreadDispatcher.Enqueue(() => Refresh(LobbyManager.CurrentLobby));
            }
        }
    }
}
