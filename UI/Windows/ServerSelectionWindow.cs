using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using PopupLib.UI.Windows;
using PopupLib.UI.Components;
using MDEN.Managers;
using MDEN.Network; // 仅引用 DTO
using MDEN.Protocol.Messages.System;
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
        private static List<string> _officialNodePlainNames = new List<string>();
        private static Dictionary<string, string> _customServerStatusDescriptions = new Dictionary<string, string>();

        private int _lastSelectedIndex = -1;
        private bool _isRefreshingNodes;

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
            RegisterWindowCleanup();

            await RefreshNodesAsync();
            await RebuildWindowOnMainThreadAsync();
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
                                txt.text = "节点列表";
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
        }

        private void RegisterWindowCleanup()
        {
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

        private void RemoveInjectedTitle()
        {
            var panel = GameObject.Find("UI/Forward/Tips/PnlBulletinNew");
            if (panel != null)
            {
                var titleTrans = panel.transform.Find("ImgBase/ScrollView/MDENTitle");
                if (titleTrans == null) titleTrans = panel.transform.Find("ImgBase/MDENTitle");
                if (titleTrans != null) UnityEngine.Object.Destroy(titleTrans.gameObject);
            }
        }

        private async Task RefreshNodesAsync(bool forceRefresh = false)
        {
            if (_isRefreshingNodes)
            {
                MelonLogger.Msg("Server refresh already in progress.");
                return;
            }

            _isRefreshingNodes = true;
            using var _ = UIManager.LockUI("Fetching server nodes...");
            CloudSyncIndicator.Start("正在获取节点...");

            try
            {
                if (forceRefresh)
                {
                    ServerManager.InvalidateCache();
                }

                var servers = new List<ApiServerEntry>();
                var fetchedServers = await ServerManager.GetOfficialServersAsync();
                if (fetchedServers != null)
                {
                    servers.AddRange(fetchedServers);
                }

                var displayData = new List<Tuple<string, string>>();
                var plainNames = new List<string>();
                foreach (var server in servers)
                {
                    var info = await ServerManager.PingServerAsync(server.Address);
                    string displayName = server.Name;
                    string desc = BuildServerStatsDescription(info);

                    if (info != null)
                    {
                        if (info.NodeId == "shanghai") displayName = "上海";
                        else if (info.NodeId == "shandong") displayName = "山东";
                        else if (info.NodeId == "hongkong") displayName = "香港";
                        else if (info.NodeId == "us") displayName = "美国";
                        else if (info.NodeId == "test") displayName = "内测节点";
                        else displayName = info.NodeId;
                    }

                    plainNames.Add(displayName);
                    displayData.Add(new Tuple<string, string>($"<color={Constants.ColorYellow}>{displayName}</color>", desc));
                }

                var customStatusDescriptions = new Dictionary<string, string>();
                ModConfigManager.LoadConfig();
                foreach (var customServer in ModConfigManager.CustomServers)
                {
                    if (customServer == null || string.IsNullOrWhiteSpace(customServer.Address)) continue;

                    var info = await ServerManager.PingServerAsync(customServer.Address);
                    customStatusDescriptions[customServer.Address] = BuildServerStatsDescription(info);
                }

                _officialServerData = servers;
                _officialNodeDisplayData = displayData;
                _officialNodePlainNames = plainNames;
                _customServerStatusDescriptions = customStatusDescriptions;
                CloudSyncIndicator.Finish(true);

            }
            catch (Exception e)
            {
                MelonLogger.Warning($"Fetch official nodes failed: {e.Message}");
                _officialServerData = new List<ApiServerEntry>();
                _officialNodeDisplayData = new List<Tuple<string, string>>();
                _officialNodePlainNames = new List<string>();
                _customServerStatusDescriptions = new Dictionary<string, string>();
                CloudSyncIndicator.Finish(false);
            }
            finally
            {
                _isRefreshingNodes = false;
            }
        }

        private Task RebuildWindowOnMainThreadAsync()
        {
            var completion = new TaskCompletionSource<bool>();
            if (IsDisposed)
            {
                completion.SetResult(true);
                return completion.Task;
            }

            MainThreadDispatcher.Enqueue(() =>
            {
                try
                {
                    if (!IsDisposed && _window != null)
                    {
                        RebuildWindow();
                    }
                    completion.TrySetResult(true);
                }
                catch (Exception ex)
                {
                    completion.TrySetException(ex);
                }
            });
            return completion.Task;
        }

        private void RebuildWindowOnMainThread()
        {
            if (IsDisposed) return;
            MainThreadDispatcher.Enqueue(() =>
            {
                if (!IsDisposed && _window != null)
                {
                    RebuildWindow();
                }
            });
        }

        private void RebuildCustomNodes()
        {
            _customNodes.Clear();
            ModConfigManager.LoadConfig();
            for (int i = 0; i < ModConfigManager.CustomServers.Count; i++)
            {
                var cs = ModConfigManager.CustomServers[i];
                var fo = new ForumObject(new LocalString($"<color={Constants.ColorBlue}>{cs.Name}</color>"), new LocalString(BuildCustomServerDescription(cs)));
                fo.Texture = ResourceManager.GetRandomBannerTexture() ?? ResourceManager.GetSprite("HomePanel.png")?.texture;
                _customNodes.Add(fo);
            }
        }

        private static string BuildServerStatsDescription(ServerInfoResponse info)
        {
            if (info == null) return "获取信息失败或服务器离线";

            return $"当前在线: <color={Constants.ColorYellow}>{info.PlayerCount}</color> 人\n房间数量: <color={Constants.ColorCyan}>{info.RoomCount}</color> 个";
        }

        private static string BuildCustomServerDescription(CustomServerInfo server)
        {
            if (server == null) return "节点不存在";

            var status = "当前在线: 未刷新\n房间数量: 未刷新";
            if (!string.IsNullOrWhiteSpace(server.Address) &&
                _customServerStatusDescriptions.TryGetValue(server.Address, out var cachedStatus))
            {
                status = cachedStatus;
            }

            return $"IP地址: {server.Address}\n{status}\n点击后管理该服务器";
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

            _btnBack = new ForumObject(new LocalString("- 返回 -"), new LocalString("回到上一个窗口"));
            _btnBack.Texture = ResourceManager.GetRandomBannerTexture() ?? ResourceManager.GetSprite("OptionsPanel.png")?.texture;
            _window.ForumObjects.Add(_btnBack);

            _btnRefresh = new ForumObject(new LocalString("- 刷新 -"), new LocalString("重新获取最新的服务器节点"));
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

            _btnAddServer = new ForumObject(new LocalString("- 添加服务器 -"), new LocalString("添加私人服务器长期到列表"));
            _btnAddServer.Texture = ResourceManager.GetRandomBannerTexture() ?? ResourceManager.GetSprite("OptionsPanel.png")?.texture;
            _window.ForumObjects.Add(_btnAddServer);

            _btnJoinServer = new ForumObject(new LocalString("- 加入服务器 -"), new LocalString("临时加入私人服务器"));
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
                await RefreshNodesAsync(true);
                await RebuildWindowOnMainThreadAsync();
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
                        await RefreshNodesAsync(true);
                    }
                    
                    // 刷新会重建 ForumWindow
                    RebuildWindowOnMainThread();
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
                    await JoinServerAsync(res, res, false);
                }

                    // 即使没输入也得把窗体重建回来
                    RebuildWindowOnMainThread();
                };
                input.Show();
            }
            else 
            {
                var customIndex = GetCustomServerIndexFromObjectIndex(objectIndex);
                if (customIndex >= 0)
                {
                    Close();
                    UIManager.OpenWindow(new CustomServerManagementWindow(customIndex));
                    return;
                }

                var officialAddress = GetOfficialServerAddressFromObjectIndex(objectIndex);
                if (!string.IsNullOrEmpty(officialAddress))
                {
                    await JoinServerAsync(officialAddress, GetOfficialServerNameFromObjectIndex(objectIndex), true);
                    return;
                }

                MelonLogger.Msg($"Selected node index: {objectIndex}");
            }
        }

        private int GetCustomServerIndexFromObjectIndex(int objectIndex)
        {
            var customStartIndex = 2 + _officialNodes.Count;
            var customIndex = objectIndex - customStartIndex;
            if (customIndex >= 0 && customIndex < _customNodes.Count)
            {
                return customIndex;
            }

            return -1;
        }

        private string GetOfficialServerAddressFromObjectIndex(int objectIndex)
        {
            var officialIndex = objectIndex - 2;
            if (officialIndex >= 0 && officialIndex < _officialServerData.Count)
            {
                return _officialServerData[officialIndex].Address;
            }

            return null;
        }

        private string GetOfficialServerNameFromObjectIndex(int objectIndex)
        {
            var officialIndex = objectIndex - 2;
            if (officialIndex >= 0 && officialIndex < _officialNodePlainNames.Count)
            {
                return _officialNodePlainNames[officialIndex];
            }

            return null;
        }

        private async Task JoinServerAsync(string address, string serverDisplayName = null, bool isOfficialServer = false)
        {
            using var _ = UIManager.LockUI("Connecting to server...");

            try
            {
                GameAccountManager.RefreshSnapshot();
                var response = await ConnectionManager.ConnectAndLoginAsync(address, serverDisplayName, isOfficialServer);
                if (IsDisposed) return;

                MelonLogger.Msg($"Connected to {address}, server version: {response.Version}");
                MainThreadDispatcher.Enqueue(() =>
                {
                    if (IsDisposed) return;
                    Close();
                    UIManager.OpenWindow(new RoomListWindow());
                });
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Join server failed: {ex.Message}");
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
