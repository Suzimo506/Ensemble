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
        private const float NavigationButtonOffset = 132f;
        private const float LeftNavigationOffset = 192f;
        // 绑定到原生 UI 生命周期中调用
        public static void Create()
        {
            if (_multiplayerBtn != null)
            {
                RefreshRoomButton();
                return;
            }

            var existing = GameObject.Find("UI/Standerd/PnlNavigation/Top/BtnMDENMultiplayer");
            if (existing != null)
            {
                _multiplayerBtn = existing;
                RefreshRoomButton();
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
                img.sprite = ResourceManager.GetSprite("PcSprButton_Img.png");
            }
            // 替换前景图标
            var iconTrans = _multiplayerBtn.transform.Find("ImgIcon");
            if (iconTrans != null)
            {
                var iconImg = iconTrans.GetComponent<Image>();
                if (iconImg != null)
                {
                    iconImg.sprite = ResourceManager.GetSprite("Globe_Img.png");
                }
                var iconRect = iconTrans.GetComponent<RectTransform>();
                if (iconRect != null)
                {
                    iconRect.anchoredPosition = new Vector2(iconRect.anchoredPosition.x - 10f, iconRect.anchoredPosition.y);
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
                    UIManager.OpenWindow(LobbyManager.IsInLobby
                        ? new RoomListWindow()
                        : new MainMenuWindow());
                }));
            }
            
            MelonLogger.Msg("Lobby entrance button injected successfully.");
            RefreshRoomButton();
        }

        public static void RefreshRoomButton()
        {
            if (_multiplayerBtn == null)
            {
                _myRoomBtn = null;
                _playlistBtn = null;
                _startBtn = null;
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
                img.sprite = ResourceManager.GetSprite("PcSprButton_Img.png");
                img.color = new Color(0.52f, 0.25f, 0.95f, 1f);
            }

            var button = _myRoomBtn.GetComponent<Button>();
            if (button != null)
            {
                button.onClick = new Button.ButtonClickedEvent();
                button.onClick.AddListener((UnityAction)new Action(() =>
                {
                    UIManager.OpenWindow(new MyRoomWindow());
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
                    UIManager.OpenWindow(new RoomPlaylistWindow());
                });
            }

            if (_startBtn == null)
            {
                _startBtn = CreateTopActionButton("BtnMDENStartGame", 2, "开始游戏", () =>
                {
                    if (LobbyManager.CurrentLobby?.HostUid != PlayerManager.CurrentUid)
                    {
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
            using var _ = UIManager.LockUI("Starting lobby...");

            try
            {
                await PlaylistManager.StartPrepareAsync();
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Start lobby prepare failed: {ex.Message}");
                MainThreadDispatcher.Enqueue(() => ShowText.ShowInfo(ex.Message));
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
                image.sprite = ResourceManager.GetSprite("PcSprButton_Img.png");
                image.color = position == 1
                    ? new Color(0.52f, 0.25f, 0.95f, 1f)
                    : new Color(1f, 0.92f, 0.08f, 1f);
            }

            var icon = buttonObj.transform.Find("ImgIcon")?.GetComponent<Image>();
            if (icon != null)
            {
                icon.sprite = ResourceManager.GetSprite(position == 1 ? "Playlist_Img.png" : "Play_Img.png");
                icon.preserveAspect = true;
                icon.transform.localScale = new Vector3(-Mathf.Abs(icon.transform.localScale.x), icon.transform.localScale.y, icon.transform.localScale.z);
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
        }
    }
}
