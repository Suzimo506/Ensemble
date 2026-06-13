using System;
using MelonLoader;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using MDEN.Managers;
using MDEN.UI.Windows;

namespace MDEN.UI.Core
{
    // 在右上角注入联机大厅入口，避开原生设置按钮
    public static class NavigationButton
    {
        private static GameObject _multiplayerBtn;
        // 绑定到原生 UI 生命周期中调用
        public static void Create()
        {
            if (_multiplayerBtn != null) return;

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
                    UIManager.OpenWindow(new MainMenuWindow());
                }));
            }
            
            MelonLogger.Msg("Lobby entrance button injected successfully.");
        }
    }
}
