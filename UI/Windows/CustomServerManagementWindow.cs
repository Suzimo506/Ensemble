using System;
using System.Threading.Tasks;
using LocalizeLib;
using MDEN.Managers;
using MDEN.Network;
using MDEN.UI.Core;
using MelonLoader;
using PopupLib.UI.Components;
using PopupLib.UI.Windows;
using UnityEngine;

namespace MDEN.UI.Windows
{
    public class CustomServerManagementWindow : MDENWindowBase
    {
        private readonly int _customServerIndex;
        private ForumWindow _window;
        private ForumObject _btnBack;
        private ForumObject _btnJoin;
        private ForumObject _btnRename;
        private ForumObject _btnDelete;

        public CustomServerManagementWindow(int customServerIndex)
        {
            _customServerIndex = customServerIndex;
        }

        public override void Show()
        {
            _window = new ForumWindow();
            _window.AutoReset = true;
            BuildList();
            _window.OnSelectionChanged += OnSelectionChanged;
            _window.OnInternalShow += OnInternalShowInjectTitle;
            _window.Show();

            RegisterEventCleanup(() =>
            {
                if (_window != null)
                {
                    _window.OnSelectionChanged -= OnSelectionChanged;
                    _window.OnInternalShow -= OnInternalShowInjectTitle;
                }

                RemoveInjectedTitle();
            });
        }

        private void BuildList()
        {
            _window.ForumObjects.Clear();

            var selectedServer = GetSelectedCustomServer();
            var serverName = selectedServer?.Name ?? "自定义节点";
            var serverAddress = selectedServer?.Address ?? "节点不存在";

            _btnBack = new ForumObject(new LocalString("返回"), new LocalString("回到节点列表"));
            _btnBack.Texture = ResourceManager.GetRandomBannerTexture() ?? ResourceManager.GetSprite("OptionsPanel.png")?.texture;
            _window.ForumObjects.Add(_btnBack);

            _btnJoin = new ForumObject(new LocalString("加入"), new LocalString($"加入节点: {serverAddress}"));
            _btnJoin.Texture = ResourceManager.GetRandomBannerTexture() ?? ResourceManager.GetSprite("RoomList.png")?.texture;
            _window.ForumObjects.Add(_btnJoin);

            _btnRename = new ForumObject(new LocalString("重命名"), new LocalString($"重命名节点: {serverName}"));
            _btnRename.Texture = ResourceManager.GetRandomBannerTexture() ?? ResourceManager.GetSprite("OptionsPanel.png")?.texture;
            _window.ForumObjects.Add(_btnRename);

            _btnDelete = new ForumObject(new LocalString("删除"), new LocalString($"从列表中删除节点: {serverName}"));
            _btnDelete.Texture = ResourceManager.GetRandomBannerTexture() ?? ResourceManager.GetSprite("HomePanel.png")?.texture;
            _window.ForumObjects.Add(_btnDelete);
        }

        private async void OnSelectionChanged(PopupLib.UI.Windows.Interfaces.IListWindow window, int objectIndex)
        {
            if (_window == null || objectIndex < 0 || objectIndex >= _window.ForumObjects.Count) return;

            var button = _window.ForumObjects[objectIndex];
            var selectedServer = GetSelectedCustomServer();

            if (button == _btnBack)
            {
                Close();
                UIManager.OpenWindow(new ServerSelectionWindow());
            }
            else if (selectedServer == null)
            {
                Close();
                UIManager.OpenWindow(new ServerSelectionWindow());
            }
            else if (button == _btnJoin)
            {
                await JoinServerAsync(selectedServer.Address);
            }
            else if (button == _btnRename)
            {
                ShowRenameWindow();
            }
            else if (button == _btnDelete)
            {
                ModConfigManager.DeleteCustomServer(_customServerIndex);
                Close();
                UIManager.OpenWindow(new ServerSelectionWindow());
            }
        }

        private void ShowRenameWindow()
        {
            if (_window != null)
            {
                _window.ForceClose();
            }

            var input = new InputWindow();
            input.OnCompletion += (w) =>
            {
                var res = input.Result;
                if (!string.IsNullOrEmpty(res))
                {
                    ModConfigManager.RenameCustomServer(_customServerIndex, res);
                }

                Close();
                UIManager.OpenWindow(new CustomServerManagementWindow(_customServerIndex));
            };
            input.Show();
        }

        private CustomServerInfo GetSelectedCustomServer()
        {
            ModConfigManager.LoadConfig();
            if (_customServerIndex >= 0 && _customServerIndex < ModConfigManager.CustomServers.Count)
            {
                return ModConfigManager.CustomServers[_customServerIndex];
            }

            return null;
        }

        private async Task JoinServerAsync(string address)
        {
            using var _ = UIManager.LockUI("Connecting to server...");

            try
            {
                var response = await ConnectionManager.ConnectAndLoginAsync(address);
                MelonLogger.Msg($"Connected to {address}, server version: {response.Version}");
                MainThreadDispatcher.Enqueue(() =>
                {
                    Close();
                    UIManager.OpenWindow(new RoomListWindow());
                });
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Join server failed: {ex.Message}");
            }
        }

        private void OnInternalShowInjectTitle(PopupLib.UI.Windows.Abstract.BaseWindow w)
        {
            var uiForward = GameObject.Find("UI/Forward");
            if (uiForward == null) return;

            var pnlBulletin = uiForward.transform.Find("Tips/PnlBulletinNew");
            if (pnlBulletin == null) return;

            var imgBase = pnlBulletin.Find("ImgBase");
            if (imgBase == null) return;

            var oldTitle = imgBase.Find("MDENTitle");
            if (oldTitle != null) UnityEngine.Object.Destroy(oldTitle.gameObject);
            var oldTitleInScroll = imgBase.Find("ScrollView/MDENTitle");
            if (oldTitleInScroll != null) UnityEngine.Object.Destroy(oldTitleInScroll.gameObject);

            var txtTittleObj = pnlBulletin.Find("TxtTittle");
            if (txtTittleObj == null) return;

            var newTitle = GameObject.Instantiate(txtTittleObj.gameObject, imgBase);
            newTitle.name = "MDENTitle";
            newTitle.SetActive(true);

            var loc = newTitle.GetComponent<Il2CppAssets.Scripts.PeroTools.GeneralLocalization.Localization>();
            if (loc != null) UnityEngine.Object.Destroy(loc);

            var txt = newTitle.GetComponent<UnityEngine.UI.Text>();
            if (txt != null)
            {
                txt.text = "管理自定义节点";
                txt.alignment = TextAnchor.MiddleCenter;
            }

            var titleRect = newTitle.GetComponent<RectTransform>();
            if (titleRect != null)
            {
                titleRect.anchorMin = new Vector2(0.5f, 1f);
                titleRect.anchorMax = new Vector2(0.5f, 1f);
                titleRect.pivot = new Vector2(0.5f, 0.5f);
                titleRect.anchoredPosition = new Vector2(0f, 12f);
            }
        }

        private void RemoveInjectedTitle()
        {
            var panel = GameObject.Find("UI/Forward/Tips/PnlBulletinNew");
            if (panel == null) return;

            var titleTrans = panel.transform.Find("ImgBase/ScrollView/MDENTitle");
            if (titleTrans == null) titleTrans = panel.transform.Find("ImgBase/MDENTitle");
            if (titleTrans != null) UnityEngine.Object.Destroy(titleTrans.gameObject);
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
