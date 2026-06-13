using System;
using UnityEngine;
using PopupLib.UI.Windows;
using PopupLib.UI.Components;
using LocalizeLib;
using MDEN.UI.Core;
using MDEN.Managers;

namespace MDEN.UI.Windows
{
    // 左右分栏的大厅主菜单，遵守防错窗与生命周期管理规范
    public class MainMenuWindow : MDENWindowBase
    {
        private ForumWindow _window;
        private ForumObject _btnProfile;
        private ForumObject _btnFriends;
        private ForumObject _btnLobbies;
        private ForumObject _btnSettings;
        private ForumObject _btnAbout;

        public override void Show()
        {
            _window = new ForumWindow();
            _window.AutoReset = true;
            
            _btnProfile = new ForumObject(new LocalString("个人信息"), new LocalString("更改自己的名字、个人简介、以及个性化修改"));
            _btnProfile.Texture = ResourceManager.GetSprite("PlayerCard.png")?.texture;
            _window.ForumObjects.Add(_btnProfile);

            _btnFriends = new ForumObject(new LocalString("好友列表"), new LocalString("查看自己的好友，与好友一起玩吧！"));
            _btnFriends.Texture = ResourceManager.GetSprite("SocialNetwork.png")?.texture;
            _window.ForumObjects.Add(_btnFriends);

            _btnLobbies = new ForumObject(new LocalString("联机大厅"), new LocalString("加入服务器，与服务器的其他人一起愉快的组队吧！"));
            _btnLobbies.Texture = ResourceManager.GetSprite("RoomList.png")?.texture;
            _window.ForumObjects.Add(_btnLobbies);

            _btnSettings = new ForumObject(new LocalString("设置"), new LocalString("更改游戏的各种设置喵"));
            _btnSettings.Texture = ResourceManager.GetSprite("OptionsPanel.png")?.texture;
            _window.ForumObjects.Add(_btnSettings);

            _btnAbout = new ForumObject(new LocalString("关于"), new LocalString("来看看这个模组的前世今生吧"));
            _btnAbout.Texture = ResourceManager.GetSprite("HomePanel.png")?.texture;
            _window.ForumObjects.Add(_btnAbout);

            _window.OnSelectionChanged += OnSelectionChanged;

            _window.Show();

            // 动态注入界面增强元素至弹窗
            var uiForward = GameObject.Find("UI/Forward");
            if (uiForward != null)
            {
                var pnlBulletin = uiForward.transform.Find("Tips/PnlBulletinNew");
                if (pnlBulletin != null)
                {
                    var imgBase = pnlBulletin.Find("ImgBase");
                    if (imgBase != null)
                    {
                        // 1. 注入标题
                        var oldTitle = imgBase.Find("MDENTitle");
                        if (oldTitle != null) UnityEngine.Object.Destroy(oldTitle.gameObject);
                        var oldTitleInScroll = imgBase.Find("ScrollView/MDENTitle");
                        if (oldTitleInScroll != null) UnityEngine.Object.Destroy(oldTitleInScroll.gameObject);

                        var txtTittleObj = pnlBulletin.Find("TxtTittle");
                        if (txtTittleObj != null)
                        {
                            var newTitle = GameObject.Instantiate(txtTittleObj.gameObject, imgBase);
                            newTitle.name = "MDENTitle";
                            newTitle.SetActive(true);

                            var loc = newTitle.GetComponent<Il2CppAssets.Scripts.PeroTools.GeneralLocalization.Localization>();
                            if (loc != null) UnityEngine.Object.Destroy(loc);

                            var txt = newTitle.GetComponent<UnityEngine.UI.Text>();
                            if (txt != null)
                            {
                                txt.text = "一起合奏吧";
                                txt.alignment = UnityEngine.TextAnchor.MiddleCenter;
                            }

                            // 恢复绝对中心位置，去除人工偏移
                            var titleRect = newTitle.GetComponent<RectTransform>();
                            if (titleRect != null)
                            {
                                titleRect.anchorMin = new Vector2(0.5f, 1f);
                                titleRect.anchorMax = new Vector2(0.5f, 1f);
                                titleRect.pivot = new Vector2(0.5f, 0.5f);
                                titleRect.anchoredPosition = new Vector2(0f, 12f);
                            }
                        }

                        // 2. 注入右上角关闭按钮
                        var oldBtn = imgBase.Find("BtnMDENClose");
                        if (oldBtn != null) UnityEngine.Object.Destroy(oldBtn.gameObject);

                        var closeBtnObj = new GameObject("BtnMDENClose");
                        closeBtnObj.transform.SetParent(imgBase, false);
                        var rect = closeBtnObj.AddComponent<RectTransform>();
                        rect.anchorMax = new Vector2(1f, 1f);
                        rect.anchorMin = new Vector2(1f, 1f);
                        rect.pivot = new Vector2(1f, 1f);
                        rect.anchoredPosition = new Vector2(-15f, -15f); 
                        // 进一步缩小叉号
                        rect.sizeDelta = new Vector2(36f, 36f);

                        // 直接将叉号挂载上来，选用亮粉紫/霓虹粉颜色，与深紫色底板形成对比且契合色系
                        var iconImg = closeBtnObj.AddComponent<UnityEngine.UI.Image>();
                        iconImg.sprite = ResourceManager.GetSprite("CloseBtn.png");
                        iconImg.color = new Color32(255, 140, 200, 255);

                        var btn = closeBtnObj.AddComponent<UnityEngine.UI.Button>();
                        btn.onClick.AddListener((UnityEngine.Events.UnityAction)(() =>
                        {
                            Close();
                        }));
                    }
                }
            }

            // 核心规范：所有按键绑定必须交由基类回收，防止幽灵按键和 UI 污染
            RegisterEventCleanup(() => 
            {
                if (_window != null)
                {
                    _window.OnSelectionChanged -= OnSelectionChanged;
                }
                
                var panel = GameObject.Find("UI/Forward/Tips/PnlBulletinNew");
                if (panel != null)
                {
                    var btnTrans = panel.transform.Find("ImgBase/BtnMDENClose");
                    if (btnTrans != null) UnityEngine.Object.Destroy(btnTrans.gameObject);

                    var titleTrans = panel.transform.Find("ImgBase/ScrollView/MDENTitle");
                    if (titleTrans == null) titleTrans = panel.transform.Find("ImgBase/MDENTitle");
                    if (titleTrans != null) UnityEngine.Object.Destroy(titleTrans.gameObject);
                }
            });
        }

        private void OnSelectionChanged(PopupLib.UI.Windows.Interfaces.IListWindow window, int objectIndex)
        {
            if (objectIndex < 0 || objectIndex >= _window.ForumObjects.Count) return;
            var button = _window.ForumObjects[objectIndex];

            // 点击具体的菜单项，此处可扩展后续界面（UI Flow 控制）
            // 例如：if (button == _btnLobbies) UIManager.OpenWindow(new LobbyWindow());
            
            // 为了防止菜单无限堆叠，通常会在打开新页面前先关闭自己
            // _window.ForceClose(); 
        }

        public override void Close()
        {
            if (_window != null)
            {
                _window.ForceClose();
                _window = null;
            }
        }
    }
}
