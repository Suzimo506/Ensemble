using System;
using System.Threading.Tasks;
using MDEN.Managers;
using MDEN.Network;
using MDEN.UI.Core;
using MelonLoader;
using UnityEngine;

namespace MDEN.UI.Windows
{
    public class CustomServerManagementWindow : MDENWindowBase
    {
        private readonly int _customServerIndex;
        private NativeListWindow _window;
        private NativeListItem _btnBack;
        private NativeListItem _btnJoin;
        private NativeListItem _btnRename;
        private NativeListItem _btnDelete;
        private int _lastSelectedIndex = -1;

        public CustomServerManagementWindow(int customServerIndex)
        {
            _customServerIndex = customServerIndex;
        }

        public override void Show()
        {
            _window = new NativeListWindow();
            _window.AutoReset = true;
            BuildList();
            _window.OnSelectionChanged += OnSelectionChanged;
            _window.Title = "管理自定义节点";
            _window.Show();
            _lastSelectedIndex = -1;

            RegisterEventCleanup(() =>
            {
                if (_window != null)
                {
                    _window.OnSelectionChanged -= OnSelectionChanged;
                }
            });
        }

        private void BuildList()
        {
            _window.Items.Clear();
            _lastSelectedIndex = -1;

            var selectedServer = GetSelectedCustomServer();
            var serverName = selectedServer?.Name ?? "自定义节点";
            var serverAddress = selectedServer?.Address ?? "节点不存在";

            _btnBack = new NativeListItem("- 返回 -", "回到节点列表");
            _btnBack.Texture = ResourceManager.GetRandomBannerTexture() ?? ResourceManager.GetSprite("OptionsPanel.png")?.texture;
            _window.Items.Add(_btnBack);

            _btnJoin = new NativeListItem("- 加入 -", $"加入节点: {serverAddress}");
            _btnJoin.Texture = ResourceManager.GetRandomBannerTexture() ?? ResourceManager.GetSprite("RoomList.png")?.texture;
            _window.Items.Add(_btnJoin);

            _btnRename = new NativeListItem("- 重命名 -", $"重命名节点: {serverName}");
            _btnRename.Texture = ResourceManager.GetRandomBannerTexture() ?? ResourceManager.GetSprite("OptionsPanel.png")?.texture;
            _window.Items.Add(_btnRename);

            _btnDelete = new NativeListItem("- 删除 -", $"从列表中删除节点: {serverName}");
            _btnDelete.Texture = ResourceManager.GetRandomBannerTexture() ?? ResourceManager.GetSprite("HomePanel.png")?.texture;
            _window.Items.Add(_btnDelete);
        }

        private async void OnSelectionChanged(INativeListWindow window, int objectIndex)
        {
            if (_window == null || objectIndex < 0 || objectIndex >= _window.Items.Count) return;

            // 第一次点击只会选中并且展示右侧文本，第二次点击才生效
            if (_lastSelectedIndex != objectIndex)
            {
                _lastSelectedIndex = objectIndex;
                return;
            }

            var button = _window.Items[objectIndex];
            var selectedServer = GetSelectedCustomServer();

            if (button == _btnBack)
            {
                Close();
                WindowStackController.OpenWindow(new ServerSelectionWindow());
            }
            else if (selectedServer == null)
            {
                Close();
                WindowStackController.OpenWindow(new ServerSelectionWindow());
            }
            else if (button == _btnJoin)
            {
                await JoinServerAsync(selectedServer.Address, selectedServer.Name);
            }
            else if (button == _btnRename)
            {
                ShowRenameWindow();
            }
            else if (button == _btnDelete)
            {
                ModConfigManager.DeleteCustomServer(_customServerIndex);
                Close();
                WindowStackController.OpenWindow(new ServerSelectionWindow());
            }
        }

        private void ShowRenameWindow()
        {
            if (_window != null)
            {
                _window.ForceClose();
            }

            var input = new NativeInputDialog();
            input.OnCompletion += (w) =>
            {
                var res = input.Result;
                if (!string.IsNullOrEmpty(res))
                {
                    ModConfigManager.RenameCustomServer(_customServerIndex, res);
                }

                Close();
                WindowStackController.OpenWindow(new CustomServerManagementWindow(_customServerIndex));
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

        private async Task JoinServerAsync(string address, string serverDisplayName)
        {
            IDisposable uiLock = null;
            try
            {
                await MainThreadDispatcher.InvokeAsync(() =>
                {
                    uiLock = WindowStackController.LockUI("Connecting to server...");
                    GameAccountManager.RefreshSnapshot();
                });

                var response = await ConnectionManager.ConnectAndLoginAsync(address, serverDisplayName, false);
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
