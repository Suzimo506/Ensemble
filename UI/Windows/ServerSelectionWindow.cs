using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using MDEN.Managers;
using MDEN.Network; // 仅引用 DTO
using MDEN.Protocol.Messages.System;
using MDEN.UI.Core;
using MelonLoader;

namespace MDEN.UI.Windows
{
    public class ServerSelectionWindow : MDENWindowBase
    {
        private NativeListWindow _window;
        private NativeListItem _btnBack;
        private NativeListItem _btnRefresh;
        private List<NativeListItem> _officialNodes = new List<NativeListItem>();
        private List<NativeListItem> _customNodes = new List<NativeListItem>();
        private NativeListItem _btnAddServer;
        private NativeListItem _btnJoinServer;
        private static List<ApiServerEntry> _officialServerData = new List<ApiServerEntry>();
        private static List<Tuple<string, string>> _officialNodeDisplayData = new List<Tuple<string, string>>();
        private static List<string> _officialNodePlainNames = new List<string>();
        private static Dictionary<string, string> _customServerStatusDescriptions = new Dictionary<string, string>();

        private int _lastSelectedIndex = -1;
        private bool _isRefreshingNodes;

        public override void Show()
        {
            _window = new NativeListWindow();
            _window.AutoReset = true;
            
            // 构建无官方节点的初始列表
            RebuildCustomNodes();
            BuildList();

            _window.OnSelectionChanged += OnSelectionChanged;
            _window.Title = "节点列表";
            _window.Show();
            _lastSelectedIndex = -1; // 在 Show 之后重置选择索引
            RegisterWindowCleanup();

            _ = ShowAsync();
        }

        private async Task ShowAsync()
        {
            try
            {
                await RefreshNodesAsync();
                await RebuildWindowOnMainThreadAsync();
            }
            catch (Exception ex)
            {
                MDEN.Managers.ClientLogManager.Warning($"Server selection show failed: {ex.Message}");
            }
        }

        private void RegisterWindowCleanup()
        {
            RegisterEventCleanup(() => 
            {
                if (_window != null)
                {
                    _window.OnSelectionChanged -= OnSelectionChanged;
                }
            });
        }

        private async Task RefreshNodesAsync(bool forceRefresh = false)
        {
            if (_isRefreshingNodes)
            {
                MDEN.Managers.ClientLogManager.Msg("Server refresh already in progress.");
                return;
            }

            _isRefreshingNodes = true;
            IDisposable uiLock = WindowStackController.LockUI("Fetching server nodes...");
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
                    string displayName = NormalizeServerDisplayName(server.Name);
                    string desc = BuildServerStatsDescription(info);

                    if (info != null && string.IsNullOrWhiteSpace(displayName))
                    {
                        displayName = GetServerDisplayNameFromNodeId(info.NodeId);
                    }

                    if (string.IsNullOrWhiteSpace(displayName))
                    {
                        displayName = server.Address;
                    }

                    plainNames.Add(displayName);
                    displayData.Add(new Tuple<string, string>($"<color={Constants.ColorYellow}>{EscapeRichText(displayName)}</color>", desc));
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
                MainThreadDispatcher.Enqueue(() => CloudSyncIndicator.Finish(true));

            }
            catch (Exception e)
            {
                MDEN.Managers.ClientLogManager.Warning($"Fetch official nodes failed: {e.Message}");
                _officialServerData = new List<ApiServerEntry>();
                _officialNodeDisplayData = new List<Tuple<string, string>>();
                _officialNodePlainNames = new List<string>();
                _customServerStatusDescriptions = new Dictionary<string, string>();
                MainThreadDispatcher.Enqueue(() => CloudSyncIndicator.Finish(false));
            }
            finally
            {
                _isRefreshingNodes = false;
                await MainThreadDispatcher.InvokeAsync(() => uiLock?.Dispose());
            }
        }

        private Task RebuildWindowOnMainThreadAsync()
        {
            var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
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
                var fo = new NativeListItem($"<color={Constants.ColorBlue}>{cs.Name}</color>", BuildCustomServerDescription(cs));
                fo.Texture = ResourceManager.GetRandomBannerTexture() ?? ResourceManager.GetSprite("HomePanel.png")?.texture;
                _customNodes.Add(fo);
            }
        }

        private static string BuildServerStatsDescription(ServerInfoResponse info)
        {
            if (info == null) return "获取信息失败或服务器离线";

            var version = string.IsNullOrWhiteSpace(info.Version) ? "未知" : info.Version;
            return $"版本: <color={Constants.ColorBlue}>{version}</color>\n当前在线: <color={Constants.ColorYellow}>{info.PlayerCount}</color> 人\n房间数量: <color={Constants.ColorCyan}>{info.RoomCount}</color> 个";
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

        private static string NormalizeServerDisplayName(string name)
        {
            return string.IsNullOrWhiteSpace(name) ? null : name.Trim();
        }

        private static string GetServerDisplayNameFromNodeId(string nodeId)
        {
            if (string.IsNullOrWhiteSpace(nodeId)) return null;

            if (nodeId == "shanghai") return "上海";
            if (nodeId == "shandong") return "山东";
            if (nodeId == "hubei") return "湖北";
            if (nodeId == "hongkong") return "香港";
            if (nodeId == "us") return "美国";
            if (nodeId == "test") return "内测节点";
            return nodeId;
        }

        private static string EscapeRichText(string value)
        {
            return value?.Replace("<", "＜").Replace(">", "＞") ?? string.Empty;
        }

        private void RebuildWindow()
        {
            if (_window != null) 
            {
                _window.OnSelectionChanged -= OnSelectionChanged;
                _window.ForceClose();
                _window = new NativeListWindow();
                _window.AutoReset = true;
                _window.Title = "节点列表";
                RebuildCustomNodes();
                BuildList();
                _window.OnSelectionChanged += OnSelectionChanged;
                _window.Show();
                _lastSelectedIndex = -1; // 在 Show 之后重置选择索引
            }
        }

        private void BuildList()
        {
            _window.Items.Clear();
            _lastSelectedIndex = -1; // 重建时清除选中状态

            _btnBack = new NativeListItem("- 返回 -", "回到上一个窗口");
            _btnBack.Texture = ResourceManager.GetRandomBannerTexture() ?? ResourceManager.GetSprite("OptionsPanel.png")?.texture;
            _window.Items.Add(_btnBack);

            _btnRefresh = new NativeListItem("- 刷新 -", "重新获取最新的服务器节点");
            _btnRefresh.Texture = ResourceManager.GetRandomBannerTexture() ?? ResourceManager.GetSprite("RoomList.png")?.texture;
            _window.Items.Add(_btnRefresh);

            _officialNodes.Clear();
            foreach (var data in _officialNodeDisplayData)
            {
                var fo = new NativeListItem(data.Item1, data.Item2);
                fo.Texture = ResourceManager.GetRandomBannerTexture() ?? ResourceManager.GetSprite("PlayerCard.png")?.texture;
                _officialNodes.Add(fo);
            }

            foreach (var node in _officialNodes)
            {
                _window.Items.Add(node);
            }

            foreach (var node in _customNodes)
            {
                _window.Items.Add(node);
            }

            _btnAddServer = new NativeListItem("- 添加服务器 -", "添加私人服务器长期到列表");
            _btnAddServer.Texture = ResourceManager.GetRandomBannerTexture() ?? ResourceManager.GetSprite("OptionsPanel.png")?.texture;
            _window.Items.Add(_btnAddServer);

            _btnJoinServer = new NativeListItem("- 加入服务器 -", "临时加入私人服务器");
            _btnJoinServer.Texture = ResourceManager.GetRandomBannerTexture() ?? ResourceManager.GetSprite("SocialNetwork.png")?.texture;
            _window.Items.Add(_btnJoinServer);
        }

        private async void OnSelectionChanged(INativeListWindow window, int objectIndex)
        {
            if (objectIndex < 0 || objectIndex >= _window.Items.Count) return;

            // 第一次点击只会选中并且展示右侧文本，第二次点击才生效
            if (_lastSelectedIndex != objectIndex)
            {
                _lastSelectedIndex = objectIndex;
                return;
            }

            var button = _window.Items[objectIndex];

            if (button == _btnBack)
            {
                Close();
                WindowStackController.OpenWindow(new MainMenuWindow());
            }
            else if (button == _btnRefresh)
            {
                await RefreshNodesAsync(true);
                await RebuildWindowOnMainThreadAsync();
            }
            else if (button == _btnAddServer)
            {
                if (_window != null) _window.ForceClose(); // 必须先关闭当前窗体，否则会被挡住

                var input = new NativeInputDialog();
                input.OnCompletion += async (w) => 
                {
                    var res = input.Result;
                    if (!string.IsNullOrEmpty(res))
                    {
                        ModConfigManager.AddCustomServer(res);
                        MDEN.Managers.ClientLogManager.Msg($"Added custom server: {res}");
                        await RefreshNodesAsync(true);
                    }
                    
                    // 刷新会重建 NativeListWindow
                    RebuildWindowOnMainThread();
                };
                input.Show();
            }
            else if (button == _btnJoinServer)
            {
                if (_window != null) _window.ForceClose();

                var input = new NativeInputDialog();
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
                    WindowStackController.OpenWindow(new CustomServerManagementWindow(customIndex));
                    return;
                }

                var officialAddress = GetOfficialServerAddressFromObjectIndex(objectIndex);
                if (!string.IsNullOrEmpty(officialAddress))
                {
                    await JoinServerAsync(officialAddress, GetOfficialServerNameFromObjectIndex(objectIndex), true);
                    return;
                }

                MDEN.Managers.ClientLogManager.Msg($"Selected node index: {objectIndex}");
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
            IDisposable uiLock = null;
            try
            {
                await MainThreadDispatcher.InvokeAsync(() =>
                {
                    uiLock = WindowStackController.LockUI("Connecting to server...");
                    GameAccountManager.RefreshSnapshot();
                });

                var response = await ConnectionManager.ConnectAndLoginAsync(address, serverDisplayName, isOfficialServer);
                if (IsDisposed) return;

                MDEN.Managers.ClientLogManager.Msg($"Connected to {address}, server version: {response.Version}");
                await MainThreadDispatcher.InvokeAsync(() =>
                {
                    if (IsDisposed) return;
                    Close();
                    WindowStackController.OpenWindow(new RoomListWindow());
                });
            }
            catch (Exception ex)
            {
                MDEN.Managers.ClientLogManager.Warning($"Join server failed: {ex.Message}");
            }
            finally
            {
                await MainThreadDispatcher.InvokeAsync(() => uiLock?.Dispose());
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
