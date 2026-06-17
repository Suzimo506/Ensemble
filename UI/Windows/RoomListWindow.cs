using System;
using System.Threading;
using System.Threading.Tasks;
using LocalizeLib;
using MDEN.Managers;
using MDEN.Protocol.Enums;
using MDEN.Protocol.Models;
using MDEN.UI.Core;
using MelonLoader;
using PopupLib.UI.Components;
using PopupLib.UI.Windows;
using UnityEngine;

namespace MDEN.UI.Windows
{
    public class RoomListWindow : MDENWindowBase
    {
        private const int AutoRefreshIntervalMs = 3000;
        public const string WaitingStatusColor = "66ff66ff";
        private const string PlayingStatusColor = "ff5555ff";
        private const string LockedStatusColor = Constants.ColorYellow;

        private ForumWindow _window;
        private ForumObject _btnBack;
        private ForumObject _btnRefresh;
        private ForumObject _btnCreateRoom;
        private LobbyListEntry[] _lobbies = new LobbyListEntry[0];
        private CancellationTokenSource _autoRefreshCts;
        private bool _refreshInProgress;
        private int _lastSelectedIndex = -1;
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
            _window = new ForumWindow();
            _window.AutoReset = true;
            BuildList();
            _window.OnSelectionChanged += OnSelectionChanged;
            _window.OnInternalShow += OnInternalShowInjectTitle;
            _window.Show();
            _lastSelectedIndex = -1;

            RegisterEventCleanup(() =>
            {
                if (_window != null)
                {
                    _window.OnSelectionChanged -= OnSelectionChanged;
                    _window.OnInternalShow -= OnInternalShowInjectTitle;
                }

                RemoveInjectedTitle();
                StopAutoRefresh();
            });

            await RefreshLobbiesAsync();
            if (!IsDisposed)
            {
                StartAutoRefresh();
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
                txt.text = "选择房间";
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

        private void BuildList()
        {
            _window.ForumObjects.Clear();
            _lastSelectedIndex = -1;

            if (!_readOnly)
            {
                _btnBack = new ForumObject(new LocalString("- 返回 -"), new LocalString("回到节点列表"));
                _btnBack.Texture = ResourceManager.GetRandomBannerTexture() ?? ResourceManager.GetSprite("OptionsPanel.png")?.texture;
                _window.ForumObjects.Add(_btnBack);
            }
            else
            {
                _btnBack = null;
            }

            _btnRefresh = new ForumObject(new LocalString("- 刷新 -"), new LocalString("重新获取当前服务器的房间列表"));
            _btnRefresh.Texture = ResourceManager.GetRandomBannerTexture() ?? ResourceManager.GetSprite("RoomList.png")?.texture;
            _window.ForumObjects.Add(_btnRefresh);

            if (!_readOnly)
            {
                _btnCreateRoom = new ForumObject(new LocalString("- 创建房间 -"), new LocalString("创建新的联机房间"));
                _btnCreateRoom.Texture = ResourceManager.GetRandomBannerTexture() ?? ResourceManager.GetSprite("HomePanel.png")?.texture;
                _window.ForumObjects.Add(_btnCreateRoom);
            }
            else
            {
                _btnCreateRoom = null;
            }

            if (_lobbies.Length == 0)
            {
                var empty = new ForumObject(new LocalString("暂无房间"), new LocalString("当前服务器没有公开房间，可以刷新或创建房间"));
                empty.Texture = ResourceManager.GetRandomBannerTexture() ?? ResourceManager.GetSprite("PlayerCard.png")?.texture;
                _window.ForumObjects.Add(empty);
                return;
            }

            foreach (var lobby in _lobbies)
            {
                var privateSuffix = lobby.IsPrivate ? "（私密）" : string.Empty;
                var name = $"<color={Constants.ColorYellow}>{EscapeRichText(lobby.Name)}{privateSuffix}</color>";
                var desc = BuildLobbyDescription(lobby);
                var item = new ForumObject(new LocalString(name), new LocalString(desc));
                item.Texture = ResourceManager.GetRandomBannerTexture() ?? ResourceManager.GetSprite("RoomList.png")?.texture;
                _window.ForumObjects.Add(item);
            }
        }

        private static string BuildLobbyDescription(LobbyListEntry lobby)
        {
            var host = EscapeRichText(lobby.HostName ?? lobby.HostUid ?? "Unknown");
            return $"房主: <color={Constants.ColorPink}>{host}</color>\n" +
                   $"人数: <color={Constants.ColorCyan}>{lobby.PlayerCount}/{lobby.MaxPlayers}</color>\n" +
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

        private async void OnSelectionChanged(PopupLib.UI.Windows.Interfaces.IListWindow window, int objectIndex)
        {
            if (_window == null || objectIndex < 0 || objectIndex >= _window.ForumObjects.Count) return;

            if (_lastSelectedIndex != objectIndex)
            {
                _lastSelectedIndex = objectIndex;
                return;
            }

            var button = _window.ForumObjects[objectIndex];
            if (button == _btnBack)
            {
                Close();
                UIManager.OpenWindow(LobbyManager.IsInLobby
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
                UIManager.OpenWindow(new CreateRoomWindow());
            }
            else if (_lobbies.Length == 0)
            {
                MelonLogger.Msg("No lobby selected because lobby list is empty.");
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
                        UIManager.OpenWindow(new MyRoomWindow());
                        return;
                    }

                    if (selectedLobby.JoinLocked)
                    {
                        Il2CppAssets.Scripts.UI.Controls.ShowText.ShowInfo("房间已上锁");
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
            if (_window != null)
            {
                _window.ForceClose();
            }

            var input = new InputWindow();
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

        private async Task RefreshLobbiesAsync()
        {
            await RefreshLobbiesAsync(true, true);
        }

        private async Task RefreshLobbiesAsync(bool showLock, bool forceRebuild)
        {
            if (_refreshInProgress) return;
            _refreshInProgress = true;
            IDisposable uiLock = null;

            try
            {
                if (showLock)
                {
                    uiLock = UIManager.LockUI("Fetching lobby list...");
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
                MelonLogger.Warning($"Fetch lobby list failed: {ex.Message}");
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

        private async Task AutoRefreshLoopAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested && !IsDisposed)
            {
                try
                {
                    await Task.Delay(AutoRefreshIntervalMs, cancellationToken);
                    if (cancellationToken.IsCancellationRequested || IsDisposed) return;

                    await RefreshLobbiesAsync(false, false);
                }
                catch (TaskCanceledException)
                {
                    return;
                }
                catch (Exception ex)
                {
                    MelonLogger.Warning($"Auto refresh lobby list failed: {ex.Message}");
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

        private async Task JoinLobbyAsync(LobbyListEntry lobby, string password = null)
        {
            using var _ = UIManager.LockUI("Joining lobby...");

            try
            {
                await LobbyManager.JoinLobbyAsync(lobby.Id, password);
                MelonLogger.Msg($"Joined lobby: {lobby.Id}");
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Join lobby failed: {ex.Message}");
                if (IsDisposed) return;
                MainThreadDispatcher.Enqueue(RebuildWindow);
            }
        }

        private void RebuildWindow()
        {
            if (_window == null) return;

            _window.OnSelectionChanged -= OnSelectionChanged;
            _window.OnInternalShow -= OnInternalShowInjectTitle;
            _window.ForceClose();
            _window = new ForumWindow();
            _window.AutoReset = true;
            BuildList();
            _window.OnSelectionChanged += OnSelectionChanged;
            _window.OnInternalShow += OnInternalShowInjectTitle;
            _window.Show();
            _lastSelectedIndex = -1;
        }

        public override void Close()
        {
            _lastSelectedIndex = -1;
            if (_window != null)
            {
                _window.ForceClose();
                _window = null;
            }
            StopAutoRefresh();
        }
    }
}
