using System;
using MelonLoader;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using MDEN.Managers;
using MDEN.UI.Windows;
using Il2CppAssets.Scripts.UI.Controls;

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
        private const float NavigationButtonOffset = 132f;
        private const float LeftNavigationOffset = 192f;
        private const float ServerLabelOptionOffset = 420f;
        private const float ServerLabelWidth = 280f;
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
            var serverName = EscapeRichText(ConnectionManager.CurrentServerDisplayName);
            _serverLabel.text = $"<color={Constants.ColorYellow}>{serverName}</color>";
        }

        private static void EnsureServerLabel()
        {
            if (_serverLabelObj != null && _serverLabel != null)
            {
                PositionServerLabel();
                return;
            }

            var topPanel = GameObject.Find("UI/Standerd/PnlNavigation/Top")?.transform;
            var source = GameObject.Find("UI/Standerd/PnlNavigation/Top/BtnOption");
            if (topPanel == null || source == null) return;

            _serverLabelObj = new GameObject("TxtMDENCurrentServerNode");
            var rect = _serverLabelObj.AddComponent<RectTransform>();
            rect.SetParent(topPanel, false);
            rect.localScale = Vector3.one;

            _serverLabel = _serverLabelObj.AddComponent<Text>();
            ApplyGameFont(_serverLabel);
            _serverLabel.fontSize = 28;
            _serverLabel.alignment = TextAnchor.MiddleRight;
            _serverLabel.horizontalOverflow = HorizontalWrapMode.Overflow;
            _serverLabel.verticalOverflow = VerticalWrapMode.Overflow;
            _serverLabel.supportRichText = true;
            _serverLabel.raycastTarget = false;
            _serverLabel.color = Color.white;

            var shadow = _serverLabelObj.AddComponent<Shadow>();
            shadow.effectDistance = new Vector2(2f, -2f);
            shadow.effectColor = new Color(0f, 0f, 0f, 0.35f);

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
                sourceRect.anchoredPosition.y);
            rect.sizeDelta = new Vector2(ServerLabelWidth, Mathf.Max(58f, sourceRect.sizeDelta.y));
            rect.SetAsLastSibling();
        }

        private static void DestroyServerLabel()
        {
            if (_serverLabelObj != null)
            {
                GameObject.Destroy(_serverLabelObj);
            }

            _serverLabelObj = null;
            _serverLabel = null;
        }

        private static void ApplyGameFont(Text text)
        {
            if (text == null) return;

            var template = FindFontTemplate();
            if (template == null || template.font == null) return;

            text.font = template.font;
            text.material = template.material;
            text.lineSpacing = template.lineSpacing;
        }

        private static Text FindFontTemplate()
        {
            var allTexts = Resources.FindObjectsOfTypeAll<Text>();
            foreach (var text in allTexts)
            {
                if (text != null && text != _serverLabel && text.font != null)
                {
                    return text;
                }
            }

            return null;
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
            GameObject.Destroy(_myRoomBtn);
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
            var topPanel = GameObject.Find("UI/Standerd/PnlNavigation/Top")?.transform;
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
                GameObject.Destroy(_playlistBtn);
                _playlistBtn = null;
            }

            if (_startBtn != null)
            {
                GameObject.Destroy(_startBtn);
                _startBtn = null;
            }
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
