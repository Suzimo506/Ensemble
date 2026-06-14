using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using PopupLib.UI.Windows;
using PopupLib.UI.Components;
using MDEN.Managers;
using MDEN.Network; // 仅引用 DTO
using LocalizeLib;
using MDEN.UI.Core;
using MelonLoader;

namespace MDEN.UI.Windows
{
    public class ServerSelectionWindow : MDENWindowBase
    {
        private ForumWindow _window;
        private ForumObject _btnBack;
        private ForumObject _btnRefresh;
        private List<ForumObject> _officialNodes = new List<ForumObject>();
        private List<ForumObject> _customNodes = new List<ForumObject>();
        private ForumObject _btnAddServer;
        private ForumObject _btnJoinServer;

        private static List<ApiServerEntry> _officialServerData = new List<ApiServerEntry>();
        private static List<Tuple<string, string>> _officialNodeDisplayData = new List<Tuple<string, string>>();
        private static bool _hasFetchedNodes = false;

        private int _lastSelectedIndex = -1;

        public override async void Show()
        {
            _window = new ForumWindow();
            _window.AutoReset = true;
            
            // 构建无官方节点的初始列表
            RebuildCustomNodes();
            BuildList();

            _window.OnSelectionChanged += OnSelectionChanged;
            _window.OnInternalShow += OnInternalShowInjectTitle;
            _window.Show();
            _lastSelectedIndex = -1; // 在 Show 之后重置选择索引

            if (!_hasFetchedNodes)
            {
                await RefreshNodesAsync();
            }
        }

        private void InjectTitle()
        {
            var uiForward = GameObject.Find("UI/Forward");
            if (uiForward != null)
            {
                var pnlBulletin = uiForward.transform.Find("Tips/PnlBulletinNew");
                if (pnlBulletin != null)
                {
                    var imgBase = pnlBulletin.Find("ImgBase");
                    if (imgBase != null)
                    {
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
                                txt.text = "选择节点";
                                txt.alignment = UnityEngine.TextAnchor.MiddleCenter;
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
                    }
                }
            }

            RegisterEventCleanup(() => 
            {
                if (_window != null)
                {
                    _window.OnSelectionChanged -= OnSelectionChanged;
                }
                
                var panel = GameObject.Find("UI/Forward/Tips/PnlBulletinNew");
                if (panel != null)
                {
                    var titleTrans = panel.transform.Find("ImgBase/ScrollView/MDENTitle");
                    if (titleTrans == null) titleTrans = panel.transform.Find("ImgBase/MDENTitle");
                    if (titleTrans != null) UnityEngine.Object.Destroy(titleTrans.gameObject);
                }
            });
        }

        private async Task RefreshNodesAsync()
        {
            using var _ = UIManager.LockUI("正在获取节点列表...");

            try
            {
                // 获取最新节点列表
                _officialServerData = await ServerManager.GetOfficialServersAsync();
                _hasFetchedNodes = true;
            }
            catch (Exception e)
            {
                MelonLogger.Warning($"获取官方节点失败: {e.Message}");
                _officialServerData = new List<MDEN.Network.ApiServerEntry>();
            }

            // 临时硬编码测试节点
            _officialServerData.Insert(0, new ApiServerEntry 
            { 
                Id = "test_hardcoded",
                Name = "测试硬编码节点",
                Address = "mdcn2.xmjjs.top:30110"
            });
            
            _officialNodeDisplayData.Clear();
            foreach (var server in _officialServerData)
            {
                var info = await ServerManager.PingServerAsync(server.Address);
                string displayName = server.Name;
                string desc = "获取信息失败或服务器离线";

                if (info != null)
                {
                    if (info.NodeId == "shanghai") displayName = "上海";
                    else if (info.NodeId == "shandong") displayName = "山东";
                    else if (info.NodeId == "hongkong") displayName = "香港";
                    else if (info.NodeId == "us") displayName = "美国";
                    else if (info.NodeId == "test") displayName = "内测节点";
                    else displayName = info.NodeId;

                    desc = $"当前在线: <color={Constants.ColorYellow}>{info.PlayerCount}</color> 人\n房间数量: <color={Constants.ColorCyan}>{info.RoomCount}</color> 个";
                }

                // 官方节点名字显示黄色
                displayName = $"<color={Constants.ColorYellow}>{displayName}</color>";

                _officialNodeDisplayData.Add(new Tuple<string, string>(displayName, desc));
            }

            // 【重点修复】：从异步网络线程切换回 Unity 主线程执行 UI 构建！
            MainThreadDispatcher.Enqueue(() =>
            {
                RebuildWindow();
            });
        }

        private void RebuildCustomNodes()
        {
            _customNodes.Clear();
            ModConfigManager.LoadConfig();
            for (int i = 0; i < ModConfigManager.CustomServers.Count; i++)
            {
                var cs = ModConfigManager.CustomServers[i];
                var fo = new ForumObject(new LocalString($"<color={Constants.ColorBlue}>{cs.Name}</color>"), new LocalString($"IP地址: {cs.Address}\n如需重命名或修改请直接编辑 Ensemble.json"));
                fo.Texture = ResourceManager.GetRandomBannerTexture() ?? ResourceManager.GetSprite("HomePanel.png")?.texture;
                _customNodes.Add(fo);
            }
        }

        private void RebuildWindow()
        {
            if (_window != null) 
            {
                _window.OnSelectionChanged -= OnSelectionChanged;
                _window.OnInternalShow -= OnInternalShowInjectTitle;
                _window.ForceClose();
                _window = new ForumWindow();
                _window.AutoReset = true;
                RebuildCustomNodes();
                BuildList();
                _window.OnSelectionChanged += OnSelectionChanged;
                _window.OnInternalShow += OnInternalShowInjectTitle;
                _window.Show();
                _lastSelectedIndex = -1; // 在 Show 之后重置选择索引
            }
        }

        private void OnInternalShowInjectTitle(PopupLib.UI.Windows.Abstract.BaseWindow w)
        {
            InjectTitle();
        }

        private void BuildList()
        {
            _window.ForumObjects.Clear();
            _lastSelectedIndex = -1; // 重建时清除选中状态

            _btnBack = new ForumObject(new LocalString("返回"), new LocalString("回到上一个窗口"));
            _btnBack.Texture = ResourceManager.GetRandomBannerTexture() ?? ResourceManager.GetSprite("OptionsPanel.png")?.texture;
            _window.ForumObjects.Add(_btnBack);

            _btnRefresh = new ForumObject(new LocalString("刷新"), new LocalString("重新获取最新的服务器节点"));
            _btnRefresh.Texture = ResourceManager.GetRandomBannerTexture() ?? ResourceManager.GetSprite("RoomList.png")?.texture;
            _window.ForumObjects.Add(_btnRefresh);

            _officialNodes.Clear();
            foreach (var data in _officialNodeDisplayData)
            {
                var fo = new ForumObject(new LocalString(data.Item1), new LocalString(data.Item2));
                fo.Texture = ResourceManager.GetRandomBannerTexture() ?? ResourceManager.GetSprite("PlayerCard.png")?.texture;
                _officialNodes.Add(fo);
            }

            foreach (var node in _officialNodes)
            {
                _window.ForumObjects.Add(node);
            }

            foreach (var node in _customNodes)
            {
                _window.ForumObjects.Add(node);
            }

            _btnAddServer = new ForumObject(new LocalString("添加服务器"), new LocalString("添加私人服务器长期到列表"));
            _btnAddServer.Texture = ResourceManager.GetRandomBannerTexture() ?? ResourceManager.GetSprite("OptionsPanel.png")?.texture;
            _window.ForumObjects.Add(_btnAddServer);

            _btnJoinServer = new ForumObject(new LocalString("加入服务器"), new LocalString("临时加入私人服务器"));
            _btnJoinServer.Texture = ResourceManager.GetRandomBannerTexture() ?? ResourceManager.GetSprite("SocialNetwork.png")?.texture;
            _window.ForumObjects.Add(_btnJoinServer);
        }

        private async void OnSelectionChanged(PopupLib.UI.Windows.Interfaces.IListWindow window, int objectIndex)
        {
            if (objectIndex < 0 || objectIndex >= _window.ForumObjects.Count) return;

            // 第一次点击只会选中并且展示右侧文本，第二次点击才生效
            if (_lastSelectedIndex != objectIndex)
            {
                _lastSelectedIndex = objectIndex;
                return;
            }

            var button = _window.ForumObjects[objectIndex];

            if (button == _btnBack)
            {
                Close();
                UIManager.OpenWindow(new MainMenuWindow());
            }
            else if (button == _btnRefresh)
            {
                await RefreshNodesAsync();
            }
            else if (button == _btnAddServer)
            {
                if (_window != null) _window.ForceClose(); // 必须先关闭当前窗体，否则会被挡住

                var input = new InputWindow();
                input.OnCompletion += async (w) => 
                {
                    var res = input.Result;
                    if (!string.IsNullOrEmpty(res))
                    {
                        ModConfigManager.AddCustomServer(res);
                        MelonLogger.Msg($"Added custom server: {res}");
                    }
                    
                    // 刷新会重建 ForumWindow
                    await RefreshNodesAsync();
                };
                input.Show();
            }
            else if (button == _btnJoinServer)
            {
                if (_window != null) _window.ForceClose();

                var input = new InputWindow();
                input.OnCompletion += async (w) => 
                {
                    var res = input.Result;
                    if (!string.IsNullOrEmpty(res))
                    {
                        MelonLogger.Msg($"Joining server: {res}");
                        // 临时加入直连逻辑预留
                    }

                    // 即使没输入也得把窗体重建回来
                    await RefreshNodesAsync();
                };
                input.Show();
            }
            else 
            {
                // 这里预留双击具体的官方/自建节点的逻辑
                MelonLogger.Msg($"Selected node index: {objectIndex}");
            }
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
