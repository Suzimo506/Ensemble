using System;
using Il2CppAssets.Scripts.PeroTools.Nice.Events;
using Il2CppAssets.Scripts.UI.Controls;
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
        private GameObject _buttonEquip;
        private Text _buttonMainText;
        private Text _buttonStopText;
        private Text _buttonEquipText;
        private Text _message;
        private bool _busy;
        private bool _stopBusy;
        private bool _equipBusy;

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

            var imgBaseTransform = _notification.transform.Find("ImgBase");
            if (imgBaseTransform == null)
            {
                UnityEngine.Object.Destroy(_notification);
                _notification = null;
                return;
            }

            _imgBase = imgBaseTransform.gameObject;

            var baseRect = _imgBase.GetComponent<RectTransform>();
            baseRect.anchorMin = new Vector2(1f, 0.5f);
            baseRect.anchorMax = new Vector2(1f, 0.5f);
            baseRect.pivot = new Vector2(1f, 0.5f);
            baseRect.anchoredPosition3D = new Vector3(-30f, 150f, 0f);
            baseRect.sizeDelta = new Vector2(350f, 370f);
            baseRect.localScale = Vector3.one;

            var messageBaseTransform = _imgBase.transform.Find("Synchronizing");
            var messageBase = messageBaseTransform == null
                ? null
                : messageBaseTransform.GetComponent<RectTransform>();
            if (messageBase == null)
            {
                Destroy();
                return;
            }

            messageBase.pivot = new Vector2(0.5f, 1f);
            messageBase.anchorMin = messageBase.pivot;
            messageBase.anchorMax = messageBase.pivot;
            messageBase.gameObject.SetActive(true);

            var messageTextTransform = messageBase.Find("TxtSynchronizing");
            _message = messageTextTransform == null ? null : messageTextTransform.GetComponent<Text>();
            if (_message == null)
            {
                Destroy();
                return;
            }

            ApplyGameFont(_message);
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
            buttonMainRect.sizeDelta = new Vector2(105f, 58f);

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
            _buttonMainText.fontSize = 26;
            _buttonMainText.horizontalOverflow = HorizontalWrapMode.Overflow;
            ApplyGameFont(_buttonMainText);

            _buttonStop = new GameObject("BtnStop");
            var buttonStopRect = _buttonStop.AddComponent<RectTransform>();
            buttonStopRect.SetParent(_imgBase.transform, false);
            buttonStopRect.anchoredPosition3D = new Vector3(-230f, 10f, 0f);
            buttonStopRect.pivot = new Vector2(1f, 0f);
            buttonStopRect.anchorMin = buttonStopRect.pivot;
            buttonStopRect.anchorMax = buttonStopRect.pivot;
            buttonStopRect.localScale = Vector3.one;
            buttonStopRect.sizeDelta = new Vector2(105f, 58f);

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
            ApplyGameFont(_buttonStopText);
            _buttonStopText.text = "停止游戏";
            _buttonStopText.color = Color.white;

            _buttonEquip = new GameObject("BtnEquip");
            var buttonEquipRect = _buttonEquip.AddComponent<RectTransform>();
            buttonEquipRect.SetParent(_imgBase.transform, false);
            buttonEquipRect.anchoredPosition3D = new Vector3(-120f, 10f, 0f);
            buttonEquipRect.pivot = new Vector2(1f, 0f);
            buttonEquipRect.anchorMin = buttonEquipRect.pivot;
            buttonEquipRect.anchorMax = buttonEquipRect.pivot;
            buttonEquipRect.localScale = Vector3.one;
            buttonEquipRect.sizeDelta = new Vector2(105f, 58f);

            var buttonEquipImg = _buttonEquip.AddComponent<Image>();
            buttonEquipImg.sprite = sprRoundedSquare;
            buttonEquipImg.type = Image.Type.Tiled;
            buttonEquipImg.color = new Color(0.98f, 0.62f, 0.1f, 1f);

            var equipButton = _buttonEquip.AddComponent<Button>();
            equipButton.onClick.AddListener((UnityAction)OnEquipClicked);

            var buttonEquipTextGo = new GameObject("BtnTxt");
            var buttonEquipTextRect = buttonEquipTextGo.AddComponent<RectTransform>();
            buttonEquipTextRect.SetParent(buttonEquipRect.transform, false);
            buttonEquipTextRect.anchoredPosition3D = Vector3.zero;
            buttonEquipTextRect.localScale = Vector3.one;
            buttonEquipTextRect.sizeDelta = buttonEquipRect.sizeDelta;

            _buttonEquipText = buttonEquipTextGo.AddComponent<Text>();
            _buttonEquipText.alignment = TextAnchor.MiddleCenter;
            _buttonEquipText.fontSize = 23;
            _buttonEquipText.horizontalOverflow = HorizontalWrapMode.Overflow;
            ApplyGameFont(_buttonEquipText);
            _buttonEquipText.text = "使用推荐";
            _buttonEquipText.color = Color.white;

            DestroyTransformObject(_imgBase.transform.Find("Synchronizing/TxtSynchronizing/ImgSynchronizing"));
            DestroyTransformObject(_imgBase.transform.Find("SynchronizingFail"));
            var closeObj = _imgBase.transform.Find("SynchronizingCompleted");
            DestroyTransformObject(closeObj);

            var customEventMsg = messageBase.GetComponent<OnCustomEvent>();
            if (customEventMsg != null) UnityEngine.Object.Destroy(customEventMsg);
        }

        private static void DestroyTransformObject(Transform transform)
        {
            if (transform == null) return;
            UnityEngine.Object.Destroy(transform.gameObject);
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

            var entry = PlaylistManager.GetCurrentPlaylistEntry();
            if (entry != null)
            {
                RecommendedConfigManager.Request(entry);
            }

            var chartTitle = entry == null ? "等待歌曲" : entry.DisplayName;
            var recommended = RecommendedConfigManager.GetDisplayText(entry);
            _message.text = $"<color=#F8DC51>Next:</color>\n{chartTitle}\n<color=#{Constants.ColorPink}>推荐配置:</color>\n{recommended}";

            bool isReady = PlaylistManager.IsLocalPlayerReady();
            _buttonMainText.text = lobby.IsPlaying ? "游戏中" : (isReady ? $"{lobby.ReadyPlayers?.Length ?? 0} / {lobby.Players?.Length ?? 0}" : "准备");

            RefreshButtonStates(lobby);
        }

        public void Update()
        {
            if (_notification == null || !_notification.activeInHierarchy) return;
            if (RoomHudController.IsChatConsumingInput) return;
            if (Input.GetKeyDown(KeyCode.RightBracket))
            {
                OnReadyClicked();
            }
        }

        public void Destroy()
        {
            _busy = false;
            _stopBusy = false;
            _equipBusy = false;
            
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

            if (_buttonEquip != null)
            {
                var btn = _buttonEquip.GetComponent<Button>();
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
            _buttonEquip = null;
            _buttonMainText = null;
            _buttonStopText = null;
            _buttonEquipText = null;
            _message = null;
        }

        private async void OnReadyClicked()
        {
            if (_busy || LobbyManager.CurrentLobby?.IsPlaying == true) return;
            if (CustomAlbumsWindowGuard.CloseIfOpen("ready"))
            {
                MainThreadDispatcher.Enqueue(() => ShowText.ShowInfo("已关闭自制谱窗口，请重新准备"));
                return;
            }

            _busy = true;
            RefreshButtonStates(LobbyManager.CurrentLobby);

            try
            {
                using var _ = WindowStackController.LockUI("Setting ready...");
                await PlaylistManager.SetReadyAsync(!PlaylistManager.IsLocalPlayerReady());
            }
            catch (Exception ex)
            {
                MDEN.Managers.ClientLogManager.Warning($"Set ready failed: {ex.Message}");
                MainThreadDispatcher.Enqueue(() => ShowText.ShowInfo($"准备失败：{ex.Message}"));
            }
            finally
            {
                _busy = false;
                MainThreadDispatcher.Enqueue(() => RefreshButtonStates(LobbyManager.CurrentLobby));
            }
        }

        private async void OnEquipClicked()
        {
            if (_equipBusy || RecommendedConfigManager.Current == null) return;
            if (LobbyManager.CurrentLobby?.IsPlaying == true) return;

            _equipBusy = true;
            RefreshButtonStates(LobbyManager.CurrentLobby);

            try
            {
                using var _ = WindowStackController.LockUI("Applying recommended config...");
                await RecommendedConfigManager.ApplyCurrentAsync();
            }
            catch (Exception ex)
            {
                MDEN.Managers.ClientLogManager.Warning($"Apply recommended config failed: {ex.Message}");
            }
            finally
            {
                _equipBusy = false;
                MainThreadDispatcher.Enqueue(() => RefreshButtonStates(LobbyManager.CurrentLobby));
            }
        }

        private async void OnStopClicked()
        {
            if (_stopBusy || LobbyManager.CurrentLobby?.HostUid != PlayerManager.CurrentUid) return;

            _stopBusy = true;
            RefreshButtonStates(LobbyManager.CurrentLobby);

            try
            {
                using var _ = WindowStackController.LockUI("Stopping lobby...");
                await PlaylistManager.StopLobbyAsync();
            }
            catch (Exception ex)
            {
                MDEN.Managers.ClientLogManager.Warning($"Stop lobby failed: {ex.Message}");
            }
            finally
            {
                _stopBusy = false;
                MainThreadDispatcher.Enqueue(() => RefreshButtonStates(LobbyManager.CurrentLobby));
            }
        }

        private void RefreshButtonStates(LobbySyncPush lobby)
        {
            if (_buttonMain == null || _buttonStop == null || _buttonEquip == null || lobby == null) return;

            var isReady = PlaylistManager.IsLocalPlayerReady();
            var mainDisabled = isReady || lobby.IsPlaying || _busy;
            var mainImage = _buttonMain.GetComponent<Image>();
            var mainButton = _buttonMain.GetComponent<Button>();
            if (mainImage != null)
            {
                mainImage.color = mainDisabled ? new Color(0.5f, 0.5f, 0.5f, 1f) : new Color(0f, 0.82f, 0.28f, 1f);
            }

            if (mainButton != null)
            {
                mainButton.interactable = !_busy && !lobby.IsPlaying;
            }

            if (_buttonMainText != null)
            {
                _buttonMainText.color = mainDisabled ? new Color(0.6f, 0.6f, 0.6f, 1f) : new Color(0.536f, 1f, 0.05f, 1f);
            }

            var canStop = lobby.HostUid == PlayerManager.CurrentUid && !_stopBusy;
            var stopButton = _buttonStop.GetComponent<Button>();
            var stopImage = _buttonStop.GetComponent<Image>();
            if (stopButton != null) stopButton.interactable = canStop;
            if (stopImage != null)
            {
                stopImage.color = canStop
                    ? new Color(0.85f, 0.08f, 0.08f, 1f)
                    : new Color(0.42f, 0.24f, 0.24f, 1f);
            }

            if (_buttonStopText != null)
            {
                _buttonStopText.color = canStop ? Color.white : new Color(0.68f, 0.68f, 0.68f, 1f);
            }

            var hasRecommendation = RecommendedConfigManager.Current != null;
            var recommendationEquipped = RecommendedConfigManager.IsCurrentEquipped();
            var canEquip = hasRecommendation && !recommendationEquipped && !lobby.IsPlaying && !_equipBusy;
            var equipButton = _buttonEquip.GetComponent<Button>();
            var equipImage = _buttonEquip.GetComponent<Image>();
            if (equipButton != null) equipButton.interactable = canEquip;
            if (equipImage != null)
            {
                equipImage.color = canEquip
                    ? new Color(0.98f, 0.62f, 0.1f, 1f)
                    : new Color(0.42f, 0.36f, 0.28f, 1f);
            }

            if (_buttonEquipText != null)
            {
                _buttonEquipText.text = recommendationEquipped ? "已选择" : "使用推荐";
                _buttonEquipText.color = canEquip ? Color.white : new Color(0.7f, 0.7f, 0.7f, 1f);
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
            var texts = Resources.FindObjectsOfTypeAll<Text>();
            foreach (var text in texts)
            {
                if (text == null || text.font == null) continue;
                var fontName = text.font.name ?? string.Empty;
                if (!fontName.Contains("Arial")) return text;
            }

            return null;
        }
    }
}
