using System;
using System.Threading.Tasks;
using Il2CppAssets.Scripts.UI.Controls;
using MDEN.Managers;
using MDEN.Protocol.Enums;
using MDEN.UI.Core;
using MelonLoader;
using UnityEngine;

namespace MDEN.UI.Windows
{
    public class CreateRoomWindow : MDENWindowBase
    {
        private NativeListWindow _window;
        private NativeListItem _btnBack;
        private NativeListItem _btnName;
        private NativeListItem _btnMaxPlayers;
        private NativeListItem _btnPlayMode;
        private NativeListItem _btnPlaylistSize;
        private NativeListItem _btnGoal;
        private NativeListItem _btnSettlement;
        private NativeListItem _btnPassword;
        private NativeListItem _btnCreate;
        private int _lastSelectedIndex = -1;
        private bool _createInProgress;

        private string _roomName = "联机房间";
        private ushort _maxPlayers = 4;
        private LobbyPlayMode _playMode = LobbyPlayMode.Normal;
        private ushort _playlistSize = 12;
        private LobbyGoal _goal = LobbyGoal.Accuracy;
        private bool _settlementEnabled;
        private string _password;

        public override void Show()
        {
            _window = new NativeListWindow();
            _window.AutoReset = true;
            BuildList();
            _window.OnSelectionChanged += OnSelectionChanged;
            _window.Title = "创建房间";
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

            _btnBack = new NativeListItem("- 返回 -", "回到房间列表");
            _btnBack.Texture = ResourceManager.GetRandomBannerTexture() ?? ResourceManager.GetSprite("OptionsPanel.png")?.texture;
            _window.Items.Add(_btnBack);

            _btnName = new NativeListItem("房间名称", $"房间名称: {HighlightValue(EscapeRichText(_roomName))}\n点击后输入房间名称，24字上限");
            _btnName.Texture = ResourceManager.GetRandomBannerTexture() ?? ResourceManager.GetSprite("HomePanel.png")?.texture;
            _window.Items.Add(_btnName);

            _btnMaxPlayers = new NativeListItem("人数", $"最多人数: {HighlightValue(_maxPlayers)}\n点击后输入人数，范围 2-10");
            _btnMaxPlayers.Texture = ResourceManager.GetRandomBannerTexture() ?? ResourceManager.GetSprite("PlayerCard.png")?.texture;
            _window.Items.Add(_btnMaxPlayers);

            _btnPlayMode = new NativeListItem(
                "游玩模式",
                $"游玩模式: {FormatPlayMode(_playMode)}\n点击切换为{FormatPlayMode(LobbyRuleTextFormatter.GetNextPlayMode((byte)_playMode))}");
            _btnPlayMode.Texture = ResourceManager.GetRandomBannerTexture() ?? ResourceManager.GetSprite("OptionsPanel.png")?.texture;
            _window.Items.Add(_btnPlayMode);

            _btnPlaylistSize = new NativeListItem("歌曲列表长度", $"列表长度: {HighlightValue(_playlistSize)}\n点击后输入歌曲列表长度，范围 2-32");
            _btnPlaylistSize.Texture = ResourceManager.GetRandomBannerTexture() ?? ResourceManager.GetSprite("RoomList.png")?.texture;
            _window.Items.Add(_btnPlaylistSize);

            _btnGoal = new NativeListItem("获胜方式", $"获胜方式: {HighlightValue(GetGoalName())}\n点击在准确率和分数间切换");
            _btnGoal.Texture = ResourceManager.GetRandomBannerTexture() ?? ResourceManager.GetSprite("SocialNetwork.png")?.texture;
            _window.Items.Add(_btnGoal);

            _btnSettlement = new NativeListItem("结算功能", $"结算功能: {HighlightValue(GetSettlementNamePlain())}\n开启后每五首歌弹出一次结算");
            _btnSettlement.Texture = ResourceManager.GetRandomBannerTexture() ?? ResourceManager.GetSprite("OptionsPanel.png")?.texture;
            _window.Items.Add(_btnSettlement);

            _btnPassword = new NativeListItem("房间密码", $"密码: {HighlightValue(string.IsNullOrWhiteSpace(_password) ? "无" : "已设置")}\n输入空内容可清除密码，16字上限");
            _btnPassword.Texture = ResourceManager.GetRandomBannerTexture() ?? ResourceManager.GetSprite("SocialNetwork.png")?.texture;
            _window.Items.Add(_btnPassword);

            var createTitle = _createInProgress
                ? $"<color={Constants.ColorYellow}>- 创建中... -</color>"
                : $"<color={Constants.ColorYellow}>- 确认创建 -</color>";
            var createDescription = _createInProgress
                ? $"请求已提交，正在等待服务器回应\n名称: {HighlightValue(EscapeRichText(_roomName))}"
                : BuildSummary();
            _btnCreate = new NativeListItem(createTitle, createDescription);
            _btnCreate.Texture = ResourceManager.GetRandomBannerTexture() ?? ResourceManager.GetSprite("RoomList.png")?.texture;
            _window.Items.Add(_btnCreate);
        }

        private async void OnSelectionChanged(INativeListWindow window, int objectIndex)
        {
            if (_window == null || objectIndex < 0 || objectIndex >= _window.Items.Count) return;

            if (_createInProgress)
            {
                ShowText.ShowInfo("正在创建房间，请稍候");
                return;
            }

            if (_lastSelectedIndex != objectIndex)
            {
                _lastSelectedIndex = objectIndex;
                return;
            }

            var button = _window.Items[objectIndex];
            if (button == _btnBack)
            {
                Close();
                WindowStackController.OpenWindow(new RoomListWindow());
            }
            else if (button == _btnName)
            {
                ShowNameInput();
            }
            else if (button == _btnMaxPlayers)
            {
                ShowMaxPlayersInput();
            }
            else if (button == _btnPlayMode)
            {
                _playMode = LobbyRuleTextFormatter.GetNextPlayMode((byte)_playMode);
                RebuildWindow();
            }
            else if (button == _btnPlaylistSize)
            {
                ShowPlaylistSizeInput();
            }
            else if (button == _btnGoal)
            {
                _goal = _goal == LobbyGoal.Accuracy ? LobbyGoal.Score : LobbyGoal.Accuracy;
                RebuildWindow();
            }
            else if (button == _btnSettlement)
            {
                _settlementEnabled = !_settlementEnabled;
                RebuildWindow();
            }
            else if (button == _btnPassword)
            {
                ShowPasswordInput();
            }
            else if (button == _btnCreate)
            {
                await CreateLobbyAsync();
            }
        }

        private void ShowNameInput()
        {
            if (_window != null)
            {
                _window.ForceClose();
            }

            var input = new NativeInputDialog();
            input.OnCompletion += (w) =>
            {
                var value = input.Result?.Trim();
                if (!string.IsNullOrWhiteSpace(value) && value.Length <= 24)
                {
                    _roomName = value;
                }
                else if (!string.IsNullOrEmpty(value))
                {
                    MDEN.Managers.ClientLogManager.Warning("Room name is too long. Max length is 24.");
                }

                RebuildWindow();
            };
            input.Show();
        }

        private void ShowPasswordInput()
        {
            if (_window != null)
            {
                _window.ForceClose();
            }

            var input = new NativeInputDialog();
            input.OnCompletion += (w) =>
            {
                var value = input.Result?.Trim();
                if (value != null && value.Length <= 16)
                {
                    _password = string.IsNullOrWhiteSpace(value) ? null : value;
                }
                else if (!string.IsNullOrEmpty(value))
                {
                    MDEN.Managers.ClientLogManager.Warning("Room password is too long. Max length is 16.");
                }

                RebuildWindow();
            };
            input.Show();
        }

        private void ShowMaxPlayersInput()
        {
            ShowNumberInput("人数", 2, 10, value => _maxPlayers = value);
        }

        private void ShowPlaylistSizeInput()
        {
            ShowNumberInput("歌曲列表长度", 2, 32, value => _playlistSize = value);
        }

        private void ShowNumberInput(string fieldName, int min, int max, Action<ushort> applyValue)
        {
            if (_window != null)
            {
                _window.ForceClose();
            }

            var input = new NativeInputDialog();
            input.OnCompletion += (w) =>
            {
                var value = input.Result?.Trim();
                if (TryParseNumberInRange(value, min, max, out var parsed))
                {
                    applyValue((ushort)parsed);
                }
                else
                {
                    ShowText.ShowInfo($"{fieldName}必须在 {min}-{max} 之间");
                }

                RebuildWindow();
            };
            input.Show();
        }

        private async System.Threading.Tasks.Task CreateLobbyAsync()
        {
            if (_createInProgress)
            {
                ShowText.ShowInfo("正在创建房间，请稍候");
                return;
            }

            if (!ConnectionManager.CanSendRequests)
            {
                var message = ConnectionManager.IsReconnecting
                    ? "正在重连服务器，请稍候"
                    : "未连接服务器，请重新选择节点";
                MDEN.Managers.ClientLogManager.Warning($"Create lobby skipped: {message}");
                ShowText.ShowInfo(message);
                if (!ConnectionManager.IsReconnecting)
                {
                    Close();
                    WindowStackController.OpenWindow(new ServerSelectionWindow());
                }
                return;
            }

            _createInProgress = true;
            ShowText.ShowInfo("正在创建房间...");
            RebuildWindow();

            var keepPending = false;
            IDisposable uiLock = WindowStackController.LockUI("Creating lobby...");

            try
            {
                var lobbyId = await LobbyManager.CreateLobbyAsync(
                    _roomName,
                    _maxPlayers,
                    (byte)_playMode,
                    _goal,
                    _playlistSize,
                    _settlementEnabled,
                    _password);
                if (IsDisposed) return;

                keepPending = true;

                MDEN.Managers.ClientLogManager.Msg($"Created lobby: {lobbyId}");
                await MainThreadDispatcher.InvokeAsync(() =>
                {
                    if (IsDisposed) return;
                    Close();
                    NavigationButton.RefreshRoomButton();
                    WindowStackController.OpenWindow(new MyRoomWindow());
                });
            }
            catch (Exception ex)
            {
                MDEN.Managers.ClientLogManager.Warning($"Create lobby failed: {ex.Message}");
                MainThreadDispatcher.Enqueue(() => ShowText.ShowInfo($"创建失败：{ex.Message}"));
            }
            finally
            {
                if (!keepPending && !IsDisposed)
                {
                    _createInProgress = false;
                    MainThreadDispatcher.Enqueue(RebuildWindow);
                }

                await MainThreadDispatcher.InvokeAsync(() => uiLock?.Dispose());
            }
        }

        private static bool TryParseNumberInRange(string value, int min, int max, out int parsed)
        {
            parsed = 0;
            return !string.IsNullOrWhiteSpace(value) &&
                   int.TryParse(value, out parsed) &&
                   parsed >= min &&
                   parsed <= max;
        }

        private string BuildSummary()
        {
            return $"名称: {HighlightValue(EscapeRichText(_roomName))}\n人数: {HighlightValue(_maxPlayers)}\n游玩模式: {FormatPlayMode(_playMode)}\n歌曲列表长度: {HighlightValue(_playlistSize)}\n获胜方式: {HighlightValue(GetGoalName())}\n结算功能: {HighlightValue(GetSettlementNamePlain())}\n密码: {HighlightValue(string.IsNullOrWhiteSpace(_password) ? "无" : "已设置")}";
        }

        private string GetGoalName()
        {
            return _goal == LobbyGoal.Score ? "分数" : "准确率";
        }

        private string GetSettlementName()
        {
            return _settlementEnabled
                ? $"<color={Constants.ColorYellow}>开启</color>"
                : "关闭";
        }

        private string GetSettlementNamePlain()
        {
            return _settlementEnabled ? "开启" : "关闭";
        }

        private static string HighlightValue(object value)
        {
            return $"<color={Constants.ColorYellow}>{value}</color>";
        }

        private static string FormatPlayMode(LobbyPlayMode mode)
        {
            return LobbyRuleTextFormatter.FormatPlayMode((byte)mode, false);
        }

        private static string EscapeRichText(string value)
        {
            return string.IsNullOrEmpty(value)
                ? string.Empty
                : value.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
        }

        private void RebuildWindow()
        {
            if (_window == null) return;

            _window.OnSelectionChanged -= OnSelectionChanged;
            _window.ForceClose();
            _window = new NativeListWindow();
            _window.AutoReset = true;
            _window.Title = "创建房间";
            BuildList();
            _window.OnSelectionChanged += OnSelectionChanged;
            _window.Show();
            _lastSelectedIndex = -1;
        }

        public override void Close()
        {
            _lastSelectedIndex = -1;
            _createInProgress = false;
            if (_window != null)
            {
                _window.ForceClose();
                _window = null;
            }
        }
    }
}
