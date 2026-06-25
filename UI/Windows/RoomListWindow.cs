using System;
using System.Threading;
using System.Threading.Tasks;
using Il2CppAssets.Scripts.UI.Controls;
using MDEN.Managers;
using MDEN.Protocol.Enums;
using MDEN.Protocol.Models;
using MDEN.UI.Core;
using MelonLoader;
using UnityEngine;

namespace MDEN.UI.Windows
{
    public class RoomListWindow : MDENWindowBase
    {
        private const int AutoRefreshIntervalMs = 3000;
        public const string WaitingStatusColor = Constants.ColorSoftGreen;
        private const string PlayingStatusColor = Constants.ColorRed;
        private const string LockedStatusColor = Constants.ColorYellow;

        private NativeListWindow _window;
        private NativeListItem _btnBack;
        private NativeListItem _btnRefresh;
        private NativeListItem _btnCreateRoom;
        private LobbyListEntry[] _lobbies = new LobbyListEntry[0];
        private CancellationTokenSource _autoRefreshCts;
        private bool _refreshInProgress;
        private bool _joinInProgress;
        private int? _joiningLobbyId;
        private int _lastSelectedIndex = -1;
        private bool _suppressNextCompletion;
        private readonly bool _readOnly;

        public RoomListWindow()
        {
        }

        public RoomListWindow(bool readOnly)
        {
            _readOnly = readOnly;
        }

        public override async void Show()
        {
            await LoadInitialLobbiesAsync();
            if (IsDisposed) return;

            MainThreadDispatcher.Enqueue(ShowLoadedWindow);
        }

        private void ShowLoadedWindow()
        {
            if (IsDisposed) return;

            _window = new NativeListWindow();
            _window.AutoReset = true;
            BuildList();
            _window.OnSelectionChanged += OnSelectionChanged;
            _window.OnCompletion += OnWindowCompletion;
            _window.Title = "选择房间";
            _window.Show();
            _lastSelectedIndex = -1;

            RegisterEventCleanup(() =>
            {
                if (_window != null)
                {
                    _window.OnSelectionChanged -= OnSelectionChanged;
                    _window.OnCompletion -= OnWindowCompletion;
                }

                StopAutoRefresh();
            });

            if (!IsDisposed)
            {
                StartAutoRefresh();
            }
        }

        private void OnWindowCompletion(INativeBaseWindow w)
        {
            if (_suppressNextCompletion)
            {
                _suppressNextCompletion = false;
                return;
            }

            WindowStackController.NotifyWindowCompleted(this);
        }

        private void BuildList()
        {
            _window.Items.Clear();
            _lastSelectedIndex = -1;

            if (!_readOnly)
            {
                _btnBack = new NativeListItem("- 返回 -", "回到节点列表");
                _btnBack.Texture = ResourceManager.GetRandomBannerTexture() ?? ResourceManager.GetSprite("OptionsPanel.png")?.texture;
                _window.Items.Add(_btnBack);
            }
            else
            {
                _btnBack = null;
            }

            _btnRefresh = new NativeListItem("- 刷新 -", "重新获取当前服务器的房间列表");
            _btnRefresh.Texture = ResourceManager.GetRandomBannerTexture() ?? ResourceManager.GetSprite("RoomList.png")?.texture;
            _window.Items.Add(_btnRefresh);

            if (!_readOnly)
            {
                _btnCreateRoom = new NativeListItem("- 创建房间 -", "创建新的联机房间");
                _btnCreateRoom.Texture = ResourceManager.GetRandomBannerTexture() ?? ResourceManager.GetSprite("HomePanel.png")?.texture;
                _window.Items.Add(_btnCreateRoom);
            }
            else
            {
                _btnCreateRoom = null;
            }

            if (_lobbies.Length == 0)
            {
                var empty = new NativeListItem("暂无房间", "当前服务器没有公开房间，可以刷新或创建房间");
                empty.Texture = ResourceManager.GetRandomBannerTexture() ?? ResourceManager.GetSprite("PlayerCard.png")?.texture;
                _window.Items.Add(empty);
                return;
            }

            foreach (var lobby in _lobbies)
            {
                var privateSuffix = lobby.IsPrivate ? "（私密）" : string.Empty;
                var joining = _joiningLobbyId == lobby.Id;
                var name = joining
                    ? $"<color={Constants.ColorYellow}>{EscapeRichText(lobby.Name)}{privateSuffix} - 加入中...</color>"
                    : $"<color={Constants.ColorYellow}>{EscapeRichText(lobby.Name)}{privateSuffix}</color>";
                var desc = joining
                    ? "请求已提交，正在等待服务器回应\n" + BuildLobbyDescription(lobby)
                    : BuildLobbyDescription(lobby);
                var item = new NativeListItem(name, desc);
                item.Texture = ResourceManager.GetRandomBannerTexture() ?? ResourceManager.GetSprite("RoomList.png")?.texture;
                _window.Items.Add(item);
            }
        }

        private static string BuildLobbyDescription(LobbyListEntry lobby)
        {
            var host = EscapeRichText(lobby.HostName ?? lobby.HostUid ?? "Unknown");
            return $"房主: <color={Constants.ColorPink}>{host}</color>\n" +
                   $"人数: <color={Constants.ColorCyan}>{lobby.PlayerCount}/{lobby.MaxPlayers}</color>\n" +
                   $"游玩模式: {LobbyRuleTextFormatter.FormatPlayMode(lobby.PlayMode, false)}\n" +
                   $"歌曲列表: <color={Constants.ColorYellow}>{lobby.PlaylistCount}/{lobby.PlaylistSize}</color>\n" +
                   $"玩法: <color={Constants.ColorBlue}>{GetPlayTypeName(lobby.PlayType)}</color>\n" +
                   $"选谱: <color={Constants.ColorBlue}>{GetChartSelectionName(lobby.ChartSelection)}</color>\n" +
                   $"获胜方式: <color={Constants.ColorYellow}>{GetGoalName(lobby.Goal)}</color>\n" +
                   $"结算功能: <color={Constants.ColorYellow}>{(lobby.SettlementEnabled ? "开启" : "关闭")}</color>\n" +
                   $"加入限制: {ColorText(lobby.JoinLocked ? "已上锁" : "开放", lobby.JoinLocked ? LockedStatusColor : WaitingStatusColor)}\n" +
                   $"密码: {ColorText(lobby.IsPrivate ? "需要" : "无", lobby.IsPrivate ? LockedStatusColor : WaitingStatusColor)}\n" +
                   $"状态: {GetColoredLobbyStatus(lobby.IsPlaying, lobby.Locked, lobby.JoinLocked)}";
        }

        private static string GetGoalName(byte goal)
        {
            return (LobbyGoal)goal switch
            {
                LobbyGoal.Score => "分数",
                LobbyGoal.Custom => "自定义",
                _ => "准确率"
            };
        }

        private static string GetPlayTypeName(byte playType)
        {
            return (LobbyPlayType)playType switch
            {
                LobbyPlayType.VanillaOnly => "仅官方谱",
                LobbyPlayType.CustomOnly => "仅自定义谱",
                _ => "全部"
            };
        }

        private static string GetChartSelectionName(byte chartSelection)
        {
            return (LobbyChartSelection)chartSelection switch
            {
                LobbyChartSelection.Playlist => "列表轮换",
                LobbyChartSelection.Random => "随机",
                _ => "房主歌单"
            };
        }

        public static string GetColoredLobbyStatus(bool isPlaying, bool locked, bool joinLocked = false)
        {
            if (isPlaying) return ColorText("游戏中", PlayingStatusColor);
            if (joinLocked) return ColorText("已上锁", LockedStatusColor);
            if (locked) return ColorText("已锁定", LockedStatusColor);
            return ColorText("等待中", WaitingStatusColor);
        }

        private static string ColorText(string value, string color)
        {
            return $"<color={color}>{value}</color>";
        }

        private static string EscapeRichText(string value)
        {
            return string.IsNullOrEmpty(value)
                ? string.Empty
                : value.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
        }

        private async void OnSelectionChanged(INativeListWindow window, int objectIndex)
        {
            if (_window == null || objectIndex < 0 || objectIndex >= _window.Items.Count) return;

            if (_joinInProgress)
            {
                ShowText.ShowInfo("正在加入房间，请稍候");
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
                WindowStackController.OpenWindow(LobbyManager.IsInLobby
                    ? new MyRoomWindow()
                    : new ServerSelectionWindow());
            }
            else if (button == _btnRefresh)
            {
                await RefreshLobbiesAsync();
            }
            else if (!_readOnly && button == _btnCreateRoom)
            {
                Close();
                WindowStackController.OpenWindow(new CreateRoomWindow());
            }
            else if (_lobbies.Length == 0)
            {
                MDEN.Managers.ClientLogManager.Msg("No lobby selected because lobby list is empty.");
            }
            else
            {
                if (_readOnly)
                {
                    return;
                }

                var lobbyIndex = objectIndex - GetLobbyStartIndex();
                if (lobbyIndex >= 0 && lobbyIndex < _lobbies.Length)
                {
                    var selectedLobby = _lobbies[lobbyIndex];
                    if (IsCurrentLobby(selectedLobby))
                    {
                        Close();
                        WindowStackController.OpenWindow(new MyRoomWindow());
                        return;
                    }

                    if (selectedLobby.JoinLocked)
                    {
                        ShowText.ShowInfo("房间已上锁");
                        return;
                    }

                    if (selectedLobby.IsPrivate)
                    {
                        ShowPasswordInput(selectedLobby);
                        return;
                    }

                    if (NeedsSwitchConfirm(selectedLobby))
                    {
                        NativeConfirmDialog.Show(
                            "切换房间",
                            $"确认离开当前房间并加入「{selectedLobby.Name}」吗？",
                            confirmed =>
                            {
                                if (confirmed)
                                {
                                    _ = JoinLobbyAsync(selectedLobby);
                                }
                            });
                        return;
                    }

                    await JoinLobbyAsync(selectedLobby);
                }
            }
        }

        private void ShowPasswordInput(LobbyListEntry lobby)
        {
            if (_joinInProgress)
            {
                ShowText.ShowInfo("正在加入房间，请稍候");
                return;
            }

            if (_window != null)
            {
                _suppressNextCompletion = true;
                _window.ForceClose();
            }

            var input = new NativeInputDialog();
            input.OnCompletion += (w) =>
            {
                var password = input.Result?.Trim();
                if (string.IsNullOrEmpty(password))
                {
                    MainThreadDispatcher.Enqueue(RebuildWindow);
                    return;
                }

                if (NeedsSwitchConfirm(lobby))
                {
                    NativeConfirmDialog.Show(
                        "切换房间",
                        $"确认离开当前房间并加入「{lobby.Name}」吗？",
                        confirmed =>
                        {
                            if (confirmed)
                            {
                                _ = JoinLobbyAsync(lobby, password);
                            }
                            else
                            {
                                MainThreadDispatcher.Enqueue(RebuildWindow);
                            }
                        });
                    return;
                }

                _ = JoinLobbyAsync(lobby, password);
            };
            input.Show();
        }

        private static bool NeedsSwitchConfirm(LobbyListEntry lobby)
        {
            return LobbyManager.IsInLobby && LobbyManager.CurrentLobby?.Id != lobby.Id;
        }

        private static bool IsCurrentLobby(LobbyListEntry lobby)
        {
            return LobbyManager.IsInLobby && LobbyManager.CurrentLobby?.Id == lobby.Id;
        }

        private int GetLobbyStartIndex()
        {
            return _readOnly ? 1 : 3;
        }

        private async System.Threading.Tasks.Task RefreshLobbiesAsync()
        {
            await RefreshLobbiesAsync(true, true);
        }

        private async System.Threading.Tasks.Task LoadInitialLobbiesAsync()
        {
            IDisposable uiLock = null;
            try
            {
                uiLock = WindowStackController.LockUI("Fetching lobby list...");
                _lobbies = await LobbyManager.RefreshLobbiesAsync();
            }
            catch (Exception ex)
            {
                MDEN.Managers.ClientLogManager.Warning($"Fetch lobby list failed: {ex.Message}");
                _lobbies = new LobbyListEntry[0];
            }
            finally
            {
                uiLock?.Dispose();
            }
        }

        private async System.Threading.Tasks.Task RefreshLobbiesAsync(bool showLock, bool forceRebuild)
        {
            if (_refreshInProgress) return;
            _refreshInProgress = true;
            IDisposable uiLock = null;

            try
            {
                if (showLock)
                {
                    uiLock = WindowStackController.LockUI("Fetching lobby list...");
                }

                var lobbies = await LobbyManager.RefreshLobbiesAsync();
                if (IsDisposed) return;
                var changed = forceRebuild || !AreLobbyListsEqual(_lobbies, lobbies);
                _lobbies = lobbies;
                if (!changed) return;

                MainThreadDispatcher.Enqueue(() =>
                {
                    if (IsDisposed) return;
                    RebuildWindow();
                });
            }
            catch (Exception ex)
            {
                MDEN.Managers.ClientLogManager.Warning($"Fetch lobby list failed: {ex.Message}");
            }
            finally
            {
                uiLock?.Dispose();
                _refreshInProgress = false;
            }
        }

        private void StartAutoRefresh()
        {
            StopAutoRefresh();
            _autoRefreshCts = new CancellationTokenSource();
            _ = AutoRefreshLoopAsync(_autoRefreshCts.Token);
        }

        private void StopAutoRefresh()
        {
            try { _autoRefreshCts?.Cancel(); } catch { }
            try { _autoRefreshCts?.Dispose(); } catch { }
            _autoRefreshCts = null;
        }

        private async System.Threading.Tasks.Task AutoRefreshLoopAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested && !IsDisposed)
            {
                try
                {
                    await System.Threading.Tasks.Task.Delay(AutoRefreshIntervalMs, cancellationToken);
                    if (cancellationToken.IsCancellationRequested || IsDisposed) return;

                    await RefreshLobbiesAsync(false, false);
                }
                catch (TaskCanceledException)
                {
                    return;
                }
                catch (Exception ex)
                {
                    MDEN.Managers.ClientLogManager.Warning($"Auto refresh lobby list failed: {ex.Message}");
                }
            }
        }

        private static bool AreLobbyListsEqual(LobbyListEntry[] left, LobbyListEntry[] right)
        {
            left ??= new LobbyListEntry[0];
            right ??= new LobbyListEntry[0];
            if (left.Length != right.Length) return false;

            for (var i = 0; i < left.Length; i++)
            {
                if (!AreLobbyEntriesEqual(left[i], right[i])) return false;
            }

            return true;
        }

        private static bool AreLobbyEntriesEqual(LobbyListEntry left, LobbyListEntry right)
        {
            if (left == null || right == null) return left == right;

            return left.Id == right.Id &&
                left.Name == right.Name &&
                left.HostUid == right.HostUid &&
                left.HostName == right.HostName &&
                left.PlayMode == right.PlayMode &&
                left.PlayType == right.PlayType &&
                left.ChartSelection == right.ChartSelection &&
                left.Goal == right.Goal &&
                left.MaxPlayers == right.MaxPlayers &&
                left.PlaylistSize == right.PlaylistSize &&
                left.PlaylistCount == right.PlaylistCount &&
                left.SettlementEnabled == right.SettlementEnabled &&
                left.PlayerCount == right.PlayerCount &&
                left.IsPrivate == right.IsPrivate &&
                left.JoinLocked == right.JoinLocked &&
                left.IsPlaying == right.IsPlaying &&
                left.Locked == right.Locked;
        }

        private async System.Threading.Tasks.Task JoinLobbyAsync(LobbyListEntry lobby, string password = null)
        {
            if (_joinInProgress)
            {
                ShowText.ShowInfo("正在加入房间，请稍候");
                return;
            }

            _joinInProgress = true;
            _joiningLobbyId = lobby.Id;
            ShowText.ShowInfo("正在加入房间...");
            StopAutoRefresh();
            MainThreadDispatcher.Enqueue(RebuildWindow);

            var joined = false;
            IDisposable uiLock = WindowStackController.LockUI("Joining lobby...");

            try
            {
                await LobbyManager.JoinLobbyAsync(lobby.Id, password);
                await MainThreadDispatcher.InvokeAsync(() => LobbyManager.MarkLobbyEntered(lobby));
                joined = true;
                if (IsDisposed) return;

                MDEN.Managers.ClientLogManager.Msg($"Joined lobby: {lobby.Id}");
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
                MDEN.Managers.ClientLogManager.Warning($"Join lobby failed: {ex.Message}");
                if (IsDisposed) return;
                MainThreadDispatcher.Enqueue(() =>
                {
                    ShowText.ShowInfo($"加入失败：{ex.Message}");
                    RebuildWindow();
                });
            }
            finally
            {
                if (!joined)
                {
                    LobbyManager.CancelPendingJoin(lobby.Id);
                    if (!IsDisposed)
                    {
                        _joinInProgress = false;
                        _joiningLobbyId = null;
                        if (_autoRefreshCts == null)
                        {
                            StartAutoRefresh();
                        }
                    }
                }

                await MainThreadDispatcher.InvokeAsync(() => uiLock?.Dispose());
            }
        }

        private void RebuildWindow()
        {
            if (_window == null) return;

            _window.OnSelectionChanged -= OnSelectionChanged;
            _window.OnCompletion -= OnWindowCompletion;
            ForceCloseWindowSafe();
            _suppressNextCompletion = false;
            _window = new NativeListWindow();
            _window.AutoReset = true;
            _window.Title = "选择房间";
            BuildList();
            _window.OnSelectionChanged += OnSelectionChanged;
            _window.OnCompletion += OnWindowCompletion;
            _window.Show();
            _lastSelectedIndex = -1;
        }

        public override void Close()
        {
            _lastSelectedIndex = -1;
            _joinInProgress = false;
            _joiningLobbyId = null;
            LobbyManager.CancelPendingJoin();
            if (_window != null)
            {
                _window.OnSelectionChanged -= OnSelectionChanged;
                _window.OnCompletion -= OnWindowCompletion;
                ForceCloseWindowSafe();
                _window = null;
            }
            StopAutoRefresh();
        }

        private void ForceCloseWindowSafe()
        {
            try
            {
                _window?.ForceClose();
            }
            catch (Exception ex)
            {
                MDEN.Managers.ClientLogManager.Warning($"Room list window close failed: {ex.Message}");
            }
        }
    }
}
