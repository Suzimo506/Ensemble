using System;
using System.Threading;
using System.Threading.Tasks;
using Il2CppAssets.Scripts.UI.Controls;
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
        public const string WaitingStatusColor = Constants.ColorSoftGreen;
        private const string PlayingStatusColor = Constants.ColorRed;
        private const string LockedStatusColor = Constants.ColorYellow;

        private ForumWindow _window;
        private ForumObject _btnBack;
        private ForumObject _btnRefresh;
        private ForumObject _btnCreateRoom;
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

            _window = new ForumWindow();
            _window.AutoReset = true;
            BuildList();
            _window.OnSelectionChanged += OnSelectionChanged;
            _window.OnInternalShow += OnInternalShowInjectTitle;
            _window.OnCompletion += OnWindowCompletion;
            _window.Show();
            _lastSelectedIndex = -1;

            RegisterEventCleanup(() =>
            {
                if (_window != null)
                {
                    _window.OnSelectionChanged -= OnSelectionChanged;
                    _window.OnInternalShow -= OnInternalShowInjectTitle;
                    _window.OnCompletion -= OnWindowCompletion;
                }

                RemoveInjectedTitle();
                StopAutoRefresh();
            });

            if (!IsDisposed)
            {
                StartAutoRefresh();
            }
        }

        private void OnWindowCompletion(PopupLib.UI.Windows.Abstract.BaseWindow w)
        {
            if (_suppressNextCompletion)
            {
                _suppressNextCompletion = false;
                return;
            }

            WindowStackController.NotifyWindowCompleted(this);
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
                txt.text = I18nManager.T("lobby.title");
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
                _btnBack = new ForumObject(new LocalString(I18nManager.T("common.back.button")), new LocalString(I18nManager.T("common.back.server_list")));
                _btnBack.Texture = ResourceManager.GetRandomBannerTexture() ?? ResourceManager.GetSprite("OptionsPanel.png")?.texture;
                _window.ForumObjects.Add(_btnBack);
            }
            else
            {
                _btnBack = null;
            }

            _btnRefresh = new ForumObject(new LocalString(I18nManager.T("server.refresh.button")), new LocalString(I18nManager.T("lobby.refresh.desc")));
            _btnRefresh.Texture = ResourceManager.GetRandomBannerTexture() ?? ResourceManager.GetSprite("RoomList.png")?.texture;
            _window.ForumObjects.Add(_btnRefresh);

            if (!_readOnly)
            {
                _btnCreateRoom = new ForumObject(new LocalString(I18nManager.T("lobby.create.button")), new LocalString(I18nManager.T("lobby.create.desc")));
                _btnCreateRoom.Texture = ResourceManager.GetRandomBannerTexture() ?? ResourceManager.GetSprite("HomePanel.png")?.texture;
                _window.ForumObjects.Add(_btnCreateRoom);
            }
            else
            {
                _btnCreateRoom = null;
            }

            if (_lobbies.Length == 0)
            {
                var empty = new ForumObject(new LocalString(I18nManager.T("lobby.empty.title")), new LocalString(I18nManager.T("lobby.empty.desc")));
                empty.Texture = ResourceManager.GetRandomBannerTexture() ?? ResourceManager.GetSprite("PlayerCard.png")?.texture;
                _window.ForumObjects.Add(empty);
                return;
            }

            foreach (var lobby in _lobbies)
            {
                var privateSuffix = lobby.IsPrivate ? I18nManager.T("lobby.private_suffix") : string.Empty;
                var joining = _joiningLobbyId == lobby.Id;
                var name = joining
                    ? $"<color={Constants.ColorYellow}>{EscapeRichText(lobby.Name)}{privateSuffix}{I18nManager.T("lobby.joining_suffix")}</color>"
                    : $"<color={Constants.ColorYellow}>{EscapeRichText(lobby.Name)}{privateSuffix}</color>";
                var desc = joining
                    ? I18nManager.Tf("lobby.joining.desc", BuildLobbyDescription(lobby))
                    : BuildLobbyDescription(lobby);
                var item = new ForumObject(new LocalString(name), new LocalString(desc));
                item.Texture = ResourceManager.GetRandomBannerTexture() ?? ResourceManager.GetSprite("RoomList.png")?.texture;
                _window.ForumObjects.Add(item);
            }
        }

        private static string BuildLobbyDescription(LobbyListEntry lobby)
        {
            var host = EscapeRichText(lobby.HostName ?? lobby.HostUid ?? I18nManager.T("common.unknown"));
            return I18nManager.Tf(
                "lobby.desc",
                Constants.ColorPink,
                host,
                Constants.ColorCyan,
                lobby.PlayerCount,
                lobby.MaxPlayers,
                LobbyRuleTextFormatter.FormatPlayMode(lobby.PlayMode, false),
                Constants.ColorYellow,
                lobby.PlaylistCount,
                lobby.PlaylistSize,
                Constants.ColorBlue,
                GetPlayTypeName(lobby.PlayType),
                Constants.ColorBlue,
                GetChartSelectionName(lobby.ChartSelection),
                Constants.ColorYellow,
                GetGoalName(lobby.Goal),
                Constants.ColorYellow,
                lobby.SettlementEnabled ? I18nManager.T("common.enabled") : I18nManager.T("common.disabled"),
                ColorText(lobby.JoinLocked ? I18nManager.T("common.locked") : I18nManager.T("common.open"), lobby.JoinLocked ? LockedStatusColor : WaitingStatusColor),
                ColorText(lobby.IsPrivate ? I18nManager.T("common.required") : I18nManager.T("common.no"), lobby.IsPrivate ? LockedStatusColor : WaitingStatusColor),
                GetColoredLobbyStatus(lobby.IsPlaying, lobby.Locked, lobby.JoinLocked));
        }

        private static string GetGoalName(byte goal)
        {
            return (LobbyGoal)goal switch
            {
                LobbyGoal.Score => I18nManager.T("lobby.goal.score"),
                LobbyGoal.Custom => I18nManager.T("lobby.goal.custom"),
                _ => I18nManager.T("lobby.goal.accuracy")
            };
        }

        private static string GetPlayTypeName(byte playType)
        {
            return (LobbyPlayType)playType switch
            {
                LobbyPlayType.VanillaOnly => I18nManager.T("lobby.play_type.vanilla"),
                LobbyPlayType.CustomOnly => I18nManager.T("lobby.play_type.custom"),
                _ => I18nManager.T("lobby.play_type.all")
            };
        }

        private static string GetChartSelectionName(byte chartSelection)
        {
            return (LobbyChartSelection)chartSelection switch
            {
                LobbyChartSelection.Playlist => I18nManager.T("lobby.chart_selection.playlist"),
                LobbyChartSelection.Random => I18nManager.T("lobby.chart_selection.random"),
                _ => I18nManager.T("lobby.chart_selection.host")
            };
        }

        public static string GetColoredLobbyStatus(bool isPlaying, bool locked, bool joinLocked = false)
        {
            if (isPlaying) return ColorText(I18nManager.T("lobby.status.playing"), PlayingStatusColor);
            if (joinLocked) return ColorText(I18nManager.T("common.locked"), LockedStatusColor);
            if (locked) return ColorText(I18nManager.T("lobby.status.locked"), LockedStatusColor);
            return ColorText(I18nManager.T("lobby.status.waiting"), WaitingStatusColor);
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

            if (_joinInProgress)
            {
                ShowText.ShowInfo(I18nManager.T("lobby.join_in_progress"));
                return;
            }

            if (_lastSelectedIndex != objectIndex)
            {
                _lastSelectedIndex = objectIndex;
                return;
            }

            var button = _window.ForumObjects[objectIndex];
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
                        ShowText.ShowInfo(I18nManager.T("lobby.room_locked"));
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
                            I18nManager.T("lobby.switch.title"),
                            I18nManager.Tf("lobby.switch.confirm", selectedLobby.Name),
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
                ShowText.ShowInfo(I18nManager.T("lobby.join_in_progress"));
                return;
            }

            if (_window != null)
            {
                _suppressNextCompletion = true;
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
                        I18nManager.T("lobby.switch.title"),
                        I18nManager.Tf("lobby.switch.confirm", lobby.Name),
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
                uiLock = WindowStackController.LockUI(I18nManager.T("lobby.fetching"));
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
                    uiLock = WindowStackController.LockUI(I18nManager.T("lobby.fetching"));
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
                ShowText.ShowInfo(I18nManager.T("lobby.join_in_progress"));
                return;
            }

            _joinInProgress = true;
            _joiningLobbyId = lobby.Id;
            ShowText.ShowInfo(I18nManager.T("lobby.joining"));
            StopAutoRefresh();
            MainThreadDispatcher.Enqueue(RebuildWindow);

            var joined = false;
            IDisposable uiLock = WindowStackController.LockUI(I18nManager.T("lobby.joining"));

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
                    ShowText.ShowInfo(I18nManager.Tf("lobby.join_failed", ex.Message));
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
            _window.OnInternalShow -= OnInternalShowInjectTitle;
            _window.OnCompletion -= OnWindowCompletion;
            ForceCloseWindowSafe();
            _suppressNextCompletion = false;
            _window = new ForumWindow();
            _window.AutoReset = true;
            BuildList();
            _window.OnSelectionChanged += OnSelectionChanged;
            _window.OnInternalShow += OnInternalShowInjectTitle;
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
                _window.OnInternalShow -= OnInternalShowInjectTitle;
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
