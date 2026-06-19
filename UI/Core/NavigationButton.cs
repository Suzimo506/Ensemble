using System;
using MelonLoader;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using MDEN.Managers;
using MDEN.UI.Windows;
using Il2CppAssets.Scripts.UI.Controls;
using UnityEngine.AddressableAssets;

namespace MDEN.UI.Core
{
    // 在右上角注入联机大厅入口，避开原生设置按钮
    public static class NavigationButton
    {
        private static GameObject _multiplayerBtn;
        private static GameObject _myRoomBtn;
        private static GameObject _playlistBtn;
        private static GameObject _startBtn;
        private static GameObject _serverLabelObj;
        private static Text _serverLabel;
        private static Sprite _serverLabelBackgroundSprite;
        private const float NavigationButtonOffset = 132f;
        private const float LeftNavigationOffset = 192f;
        private const float ServerLabelOptionOffset = 500f;
        private const float ServerLabelYOffset = -14f;
        private const float ServerLabelMinWidth = 104f;
        private const float ServerLabelMaxWidth = 210f;
        private const float ServerLabelHeight = 36f;
        private const float ServerLabelHorizontalPadding = 34f;
        private const float NavigationIconCenterOffset = 10f;
        private const float RoomActionIconCenterOffset = 8f;
        private const string NavigationButtonSpriteName = "PcSprButton_Img.png";
        private const string MultiplayerIconSpriteName = "Multiplayer_Img.png";
        private const string MyRoomIconSpriteName = "MyRoom_Img.png";
        private const string PlaylistIconSpriteName = "RoomPlaylist_Img.png";
        private const string StartIconSpriteName = "Play_Img.png";
        // 绑定到原生 UI 生命周期中调用
        public static void Create()
        {
            RecoverSceneObjects();
            if (_multiplayerBtn != null)
            {
                RefreshRoomButton();
                RefreshServerLabel();
                return;
            }

            var existing = GameObject.Find("UI/Standerd/PnlNavigation/Top/BtnMDENMultiplayer");
            if (existing != null)
            {
                _multiplayerBtn = existing;
                RefreshRoomButton();
                RefreshServerLabel();
                return;
            }

            var btnOptionObj = GameObject.Find("UI/Standerd/PnlNavigation/Top/BtnOption");
            if (btnOptionObj == null)
            {
                MelonLogger.Error("Failed to inject entrance: Cannot find native UI/Standerd/PnlNavigation/Top/BtnOption");
                return;
            }

            var topPanelObj = GameObject.Find("UI/Standerd/PnlNavigation/Top");
            if (topPanelObj == null) return;

            _multiplayerBtn = GameObject.Instantiate(btnOptionObj, topPanelObj.transform);
            _multiplayerBtn.name = "BtnMDENMultiplayer";
            _multiplayerBtn.SetActive(true);
            // 向左偏移，防止与原生按钮重叠
            var rect = _multiplayerBtn.GetComponent<RectTransform>();
            if (rect != null)
            {
                rect.anchoredPosition = new Vector2(rect.anchoredPosition.x - 132f, rect.anchoredPosition.y);
            }
            // 替换背景图
            var img = _multiplayerBtn.GetComponent<Image>();
            if (img != null)
            {
                ApplyNavigationButtonBackground(img);
            }
            // 替换前景图标
            var iconTrans = _multiplayerBtn.transform.Find("ImgIcon");
            if (iconTrans != null)
            {
                var iconImg = iconTrans.GetComponent<Image>();
                if (iconImg != null)
                {
                    ApplyNavigationIcon(iconImg, MultiplayerIconSpriteName, false);
                }
            }
            // 清理原生绑定的按键事件，防止误触发原生设置
            var keyBinding = _multiplayerBtn.GetComponent("InputKeyBinding");
            if (keyBinding != null) GameObject.Destroy(keyBinding);
            
            var eventTrigger = _multiplayerBtn.GetComponent<UnityEngine.EventSystems.EventTrigger>();
            if (eventTrigger != null) GameObject.Destroy(eventTrigger);
            // 绑定全新入口事件
            var button = _multiplayerBtn.GetComponent<Button>();
            if (button != null)
            {
                button.onClick = new Button.ButtonClickedEvent();
                button.onClick.AddListener((UnityAction)new Action(() => 
                {
                    WindowStackController.OpenWindow(LobbyManager.IsInLobby
                        ? new RoomListWindow(true)
                        : new MainMenuWindow());
                }));
            }
            
            MelonLogger.Msg("Lobby entrance button injected successfully.");
            RefreshRoomButton();
            RefreshServerLabel();
        }

        public static void RefreshRoomButton()
        {
            RecoverSceneObjects();
            if (_multiplayerBtn == null)
            {
                _myRoomBtn = null;
                _playlistBtn = null;
                _startBtn = null;
                _serverLabelObj = null;
                _serverLabel = null;
                return;
            }

            if (LobbyManager.IsInLobby)
            {
                CreateRoomButton();
                CreateRoomActionButtons();
            }
            else
            {
                DestroyRoomButton();
                DestroyRoomActionButtons();
            }

            RefreshServerLabel();
        }

        public static void RefreshServerLabel()
        {
            if (!ConnectionManager.IsLoggedIn || string.IsNullOrWhiteSpace(ConnectionManager.CurrentServerDisplayName))
            {
                DestroyServerLabel();
                return;
            }

            EnsureServerLabel();
            if (_serverLabelObj == null || _serverLabel == null) return;

            _serverLabelObj.SetActive(true);
            ApplyGameFont(_serverLabel);
            var serverName = EscapeRichText(ConnectionManager.CurrentServerDisplayName);
            var serverColor = ConnectionManager.CurrentServerIsOfficial
                ? Constants.ColorYellow
                : Constants.ColorBlue;
            _serverLabel.text = $"<color=#{serverColor}>{serverName}</color>";
            PositionServerLabel();
        }

        private static void EnsureServerLabel()
        {
            if (_serverLabelObj != null && _serverLabel != null)
            {
                PositionServerLabel();
                return;
            }

            var topPanelObj = GameObject.Find("UI/Standerd/PnlNavigation/Top");
            var topPanel = topPanelObj == null ? null : topPanelObj.transform;
            var source = GameObject.Find("UI/Standerd/PnlNavigation/Top/BtnOption");
            if (topPanel == null || source == null) return;

            _serverLabelObj = new GameObject("TxtMDENCurrentServerNode");
            var rect = _serverLabelObj.AddComponent<RectTransform>();
            rect.SetParent(topPanel, false);
            rect.localScale = Vector3.one;

            var background = _serverLabelObj.AddComponent<Image>();
            background.sprite = GetServerLabelBackgroundSprite();
            background.type = Image.Type.Sliced;
            background.color = new Color(0.55f, 0.38f, 0.88f, 0.92f);
            background.raycastTarget = false;

            var textObj = new GameObject("TxtNodeName");
            var textRect = textObj.AddComponent<RectTransform>();
            textRect.SetParent(rect, false);
            textRect.localScale = Vector3.one;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(12f, 0f);
            textRect.offsetMax = new Vector2(-12f, 0f);

            _serverLabel = textObj.AddComponent<Text>();
            ApplyGameFont(_serverLabel);
            _serverLabel.fontSize = 18;
            _serverLabel.alignment = TextAnchor.MiddleCenter;
            _serverLabel.horizontalOverflow = HorizontalWrapMode.Overflow;
            _serverLabel.verticalOverflow = VerticalWrapMode.Overflow;
            _serverLabel.supportRichText = true;
            _serverLabel.raycastTarget = false;
            _serverLabel.color = new Color(0.78f, 1f, 0.18f, 1f);

            var shadow = _serverLabelObj.AddComponent<Shadow>();
            shadow.effectDistance = new Vector2(1f, -1f);
            shadow.effectColor = new Color(0.08f, 0.02f, 0.18f, 0.42f);

            PositionServerLabel();
        }

        private static void PositionServerLabel()
        {
            if (_serverLabelObj == null) return;

            var rect = _serverLabelObj.GetComponent<RectTransform>();
            var sourceRect = GameObject.Find("UI/Standerd/PnlNavigation/Top/BtnOption")?.GetComponent<RectTransform>();
            if (rect == null || sourceRect == null) return;

            rect.localScale = Vector3.one;
            rect.anchorMin = sourceRect.anchorMin;
            rect.anchorMax = sourceRect.anchorMax;
            rect.pivot = new Vector2(1f, sourceRect.pivot.y);
            rect.anchoredPosition = new Vector2(
                sourceRect.anchoredPosition.x - ServerLabelOptionOffset,
                sourceRect.anchoredPosition.y + ServerLabelYOffset);
            rect.sizeDelta = new Vector2(GetServerLabelWidth(), ServerLabelHeight);
            rect.SetAsLastSibling();
        }

        private static float GetServerLabelWidth()
        {
            if (_serverLabel == null) return ServerLabelMinWidth;

            var preferredWidth = _serverLabel.preferredWidth + ServerLabelHorizontalPadding;
            return Mathf.Clamp(preferredWidth, ServerLabelMinWidth, ServerLabelMaxWidth);
        }

        private static void DestroyServerLabel()
        {
            if (_serverLabelObj != null)
            {
                DestroyObject(_serverLabelObj);
            }

            _serverLabelObj = null;
            _serverLabel = null;
        }

        private static void ApplyGameFont(Text text)
        {
            if (text == null) return;

            var template = FindFontTemplate();
            if (template == null || template.font == null)
            {
                text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
                return;
            }

            text.font = template.font;
            text.material = template.material;
            text.lineSpacing = template.lineSpacing;
        }

        private static Text FindFontTemplate()
        {
            var inputFieldTemplate = FindInputFieldFontTemplate();
            if (inputFieldTemplate != null) return inputFieldTemplate;

            var allTexts = Resources.FindObjectsOfTypeAll<Text>();
            Text fallback = null;
            foreach (var text in allTexts)
            {
                if (text == null || text == _serverLabel || text.font == null) continue;
                fallback ??= text;

                var fontName = text.font.name ?? string.Empty;
                if (!fontName.Contains("Arial"))
                {
                    return text;
                }
            }

            return fallback;
        }

        private static Text FindInputFieldFontTemplate()
        {
            var inputTemplates = Resources.FindObjectsOfTypeAll<Il2CppAssets.Scripts.UI.PeroInputField>();
            if (inputTemplates == null) return null;

            foreach (var template in inputTemplates)
            {
                if (template == null) continue;

                var inputField = template.GetComponent<InputField>();
                var text = inputField?.textComponent;
                if (text != null && text.font != null) return text;
            }

            return null;
        }

        private static Sprite GetServerLabelBackgroundSprite()
        {
            if (_serverLabelBackgroundSprite != null) return _serverLabelBackgroundSprite;

            try
            {
                _serverLabelBackgroundSprite = Addressables.LoadAssetAsync<Sprite>("SprRoundedsquare").WaitForCompletion();
            }
            catch
            {
                _serverLabelBackgroundSprite = null;
            }

            return _serverLabelBackgroundSprite;
        }

        private static string EscapeRichText(string value)
        {
            return value?.Replace("<", "＜").Replace(">", "＞") ?? string.Empty;
        }

        private static void CreateRoomButton()
        {
            if (_myRoomBtn != null) return;

            var topPanelObj = GameObject.Find("UI/Standerd/PnlNavigation/Top");
            if (topPanelObj == null) return;

            _myRoomBtn = GameObject.Instantiate(_multiplayerBtn, topPanelObj.transform);
            _myRoomBtn.name = "BtnMDENMyRoom";
            _myRoomBtn.SetActive(true);

            var rect = _myRoomBtn.GetComponent<RectTransform>();
            if (rect != null)
            {
                rect.anchoredPosition = new Vector2(rect.anchoredPosition.x - 132f, rect.anchoredPosition.y);
            }

            var img = _myRoomBtn.GetComponent<Image>();
            if (img != null)
            {
                ApplyNavigationButtonBackground(img);
            }

            var icon = _myRoomBtn.transform.Find("ImgIcon")?.GetComponent<Image>();
            if (icon != null)
            {
                ApplyNavigationIcon(icon, MyRoomIconSpriteName, false);
            }

            var button = _myRoomBtn.GetComponent<Button>();
            if (button != null)
            {
                button.onClick = new Button.ButtonClickedEvent();
                button.onClick.AddListener((UnityAction)new Action(() =>
                {
                    WindowStackController.OpenWindow(new MyRoomWindow());
                }));
            }
        }

        private static void DestroyRoomButton()
        {
            if (_myRoomBtn == null) return;
            DestroyObject(_myRoomBtn);
            _myRoomBtn = null;
        }

        private static void CreateRoomActionButtons()
        {
            if (_playlistBtn == null)
            {
                _playlistBtn = CreateTopActionButton("BtnMDENPlaylist", 1, "歌曲列表", () =>
                {
                    WindowStackController.OpenWindow(new RoomPlaylistWindow());
                });
            }

            if (_startBtn == null)
            {
                _startBtn = CreateTopActionButton("BtnMDENStartGame", 2, "开始游戏", () =>
                {
                    if (LobbyManager.CurrentLobby?.HostUid != PlayerManager.CurrentUid)
                    {
                        ShowText.ShowInfo("只有房主可以开始游戏哦");
                        MelonLogger.Warning("No permission to start lobby.");
                        return;
                    }

                    NativeConfirmDialog.Show("开始游戏", "确认开始多人准备吗？", confirmed =>
                    {
                        if (!confirmed) return;
                        _ = StartPrepareAsync();
                    });
                });
            }
        }

        private static async System.Threading.Tasks.Task StartPrepareAsync()
        {
            using var _ = WindowStackController.LockUI("Starting lobby...");

            try
            {
                await PlaylistManager.StartPrepareAsync();
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Start lobby prepare failed: {ex.Message}");
                MainThreadDispatcher.Enqueue(() => ShowText.ShowInfo($"开始失败：{ex.Message}"));
            }
        }

        private static GameObject CreateTopActionButton(string name, int position, string label, Action action)
        {
            var topPanelObj = GameObject.Find("UI/Standerd/PnlNavigation/Top");
            var topPanel = topPanelObj == null ? null : topPanelObj.transform;
            var source = GameObject.Find("UI/Standerd/PnlNavigation/Top/BtnOption");
            if (topPanel == null || source == null)
            {
                MelonLogger.Warning($"Cannot create {name}: native navigation button source not found.");
                return null;
            }

            var buttonObj = GameObject.Instantiate(source, topPanel);
            buttonObj.name = name;
            buttonObj.SetActive(true);

            var rect = buttonObj.GetComponent<RectTransform>();
            if (rect != null)
            {
                rect.anchoredPosition = new Vector2(
                    -rect.anchoredPosition.x + LeftNavigationOffset + NavigationButtonOffset * position,
                    rect.anchoredPosition.y);
                rect.pivot = new Vector2(0f, rect.pivot.y);
                rect.anchorMin = new Vector2(0f, rect.anchorMin.y);
                rect.anchorMax = new Vector2(0f, rect.anchorMax.y);
                rect.localScale = new Vector3(-Mathf.Abs(rect.localScale.x), rect.localScale.y, rect.localScale.z);
            }

            var image = buttonObj.GetComponent<Image>();
            if (image != null)
            {
                ApplyNavigationButtonBackground(image);
            }

            var icon = buttonObj.transform.Find("ImgIcon")?.GetComponent<Image>();
            if (icon != null)
            {
                ApplyNavigationIcon(icon, position == 1 ? PlaylistIconSpriteName : StartIconSpriteName, true);
            }

            RemoveNativeBindings(buttonObj);

            var button = buttonObj.GetComponent<Button>();
            if (button != null)
            {
                button.onClick = new Button.ButtonClickedEvent();
                button.onClick.AddListener((UnityAction)(() => action.Invoke()));
            }

            return buttonObj;
        }

        private static void RemoveNativeBindings(GameObject target)
        {
            var keyBinding = target.GetComponent("InputKeyBinding");
            if (keyBinding != null) GameObject.Destroy(keyBinding);

            var eventTrigger = target.GetComponent<UnityEngine.EventSystems.EventTrigger>();
            if (eventTrigger != null) GameObject.Destroy(eventTrigger);
        }

        private static void ApplyNavigationButtonBackground(Image image)
        {
            if (image == null) return;

            image.sprite = ResourceManager.GetSprite(NavigationButtonSpriteName);
        }

        private static void ApplyNavigationIcon(Image icon, string spriteName, bool mirrorParent)
        {
        if (icon == null) return;

        icon.sprite = ResourceManager.GetSprite(spriteName);
        ApplyNativeNavigationIconStyle(icon);
        icon.preserveAspect = false;

        var iconRect = icon.GetComponent<RectTransform>();
        if (iconRect != null)
        {
            CenterIcon(iconRect, mirrorParent);
        }
    }

    private static void ApplyNativeNavigationIconStyle(Image icon)
    {
        var nativeIcon = GameObject.Find("UI/Standerd/PnlNavigation/Top/BtnOption")?
            .transform.Find("ImgIcon")?
            .GetComponent<Image>();
        if (nativeIcon != null)
        {
            icon.color = nativeIcon.color;
            icon.material = nativeIcon.material;
            return;
        }

        icon.color = Color.white;
        icon.material = null;
    }

    private static void CenterIcon(RectTransform iconRect, bool mirrorParent)
        {
            iconRect.anchorMin = new Vector2(0.5f, 0.5f);
            iconRect.anchorMax = new Vector2(0.5f, 0.5f);
            iconRect.pivot = new Vector2(0.5f, 0.5f);
            iconRect.anchoredPosition = new Vector2(mirrorParent ? RoomActionIconCenterOffset : NavigationIconCenterOffset, 0f);
            var scale = iconRect.localScale;
            iconRect.localScale = new Vector3(
                mirrorParent ? -Mathf.Abs(scale.x) : Mathf.Abs(scale.x),
                scale.y,
                scale.z);
        }

        private static void DestroyRoomActionButtons()
        {
            if (_playlistBtn != null)
            {
                DestroyObject(_playlistBtn);
                _playlistBtn = null;
            }

            if (_startBtn != null)
            {
                DestroyObject(_startBtn);
                _startBtn = null;
            }
        }

        private static void RecoverSceneObjects()
        {
            if (_multiplayerBtn == null)
            {
                _multiplayerBtn = GameObject.Find("UI/Standerd/PnlNavigation/Top/BtnMDENMultiplayer");
            }

            if (_myRoomBtn == null)
            {
                _myRoomBtn = GameObject.Find("UI/Standerd/PnlNavigation/Top/BtnMDENMyRoom");
            }

            if (_playlistBtn == null)
            {
                _playlistBtn = GameObject.Find("UI/Standerd/PnlNavigation/Top/BtnMDENPlaylist");
            }

            if (_startBtn == null)
            {
                _startBtn = GameObject.Find("UI/Standerd/PnlNavigation/Top/BtnMDENStartGame");
            }

            if (_serverLabelObj == null)
            {
                _serverLabelObj = GameObject.Find("UI/Standerd/PnlNavigation/Top/TxtMDENCurrentServerNode");
                if (_serverLabelObj != null)
                {
                    var labelTransform = _serverLabelObj.transform.Find("TxtNodeName");
                    _serverLabel = labelTransform == null
                        ? _serverLabelObj.GetComponent<Text>()
                        : labelTransform.GetComponent<Text>();
                }
            }
        }

        private static void DestroyObject(GameObject obj)
        {
            if (obj == null) return;
            GameObject.Destroy(obj);
        }

        public static void ResetSceneObjects()
        {
            _multiplayerBtn = null;
            _myRoomBtn = null;
            _playlistBtn = null;
            _startBtn = null;
            _serverLabelObj = null;
            _serverLabel = null;
        }
    }
}
