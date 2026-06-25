using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MDEN.Managers;
using MDEN.Protocol.Enums;
using MDEN.Protocol.Messages.Lobby;
using MDEN.Protocol.Models;
using MDEN.Protocol.Rules;
using MDEN.UI.Core;
using UnityEngine;

namespace MDEN.UI.Windows
{
    public class MyRoomWindow : MDENWindowBase
    {
        private NativeListWindow _window;
        private NativeListItem _btnLeave;
        private NativeListItem _btnPlayMode;
        private NativeListItem _btnGoal;
        private NativeListItem _btnSettlement;
        private NativeListItem _btnJoinLock;
        private NativeListItem _btnPassword;
        private readonly Dictionary<NativeListItem, PlayerSyncEntry> _playerItems = new Dictionary<NativeListItem, PlayerSyncEntry>();
        private readonly Dictionary<string, double?> _ratingLevels = new Dictionary<string, double?>();
        private readonly HashSet<string> _loadingRatingLevels = new HashSet<string>();
        private int _lastSelectedIndex = -1;
        private string _lastLobbyRefreshKey;

        public override async void Show()
        {
            await LoadRatingLevelsForInitialShowAsync();
            if (IsDisposed) return;

            MainThreadDispatcher.Enqueue(ShowLoadedWindow);
        }

        private void ShowLoadedWindow()
        {
            if (IsDisposed) return;

            _window = new NativeListWindow();
            _window.AutoReset = true;
            BuildList();
            _lastLobbyRefreshKey = BuildLobbyRefreshKey(LobbyManager.CurrentLobby);
            LobbyManager.CurrentLobbyChanged += HandleCurrentLobbyChanged;
            _window.OnSelectionChanged += OnSelectionChanged;
            _window.Title = "我的房间";
            _window.Show();
            _lastSelectedIndex = -1;

            RegisterEventCleanup(() =>
            {
                LobbyManager.CurrentLobbyChanged -= HandleCurrentLobbyChanged;

                if (_window != null)
                {
                    _window.OnSelectionChanged -= OnSelectionChanged;
                }
            });
        }

        private void HandleCurrentLobbyChanged(LobbySyncPush lobby)
        {
            MainThreadDispatcher.Enqueue(() =>
            {
                if (IsDisposed) return;

                if (lobby == null)
                {
                    Close();
                    NavigationButton.RefreshRoomButton();
                    WindowStackController.OpenWindow(new RoomListWindow());
                    return;
                }

                if (ShouldSkipBackgroundRefresh(lobby))
                {
                    return;
                }

                var refreshKey = BuildLobbyRefreshKey(lobby);
                if (refreshKey == _lastLobbyRefreshKey)
                {
                    return;
                }

                RefreshWindowContent();
            });
        }

        private static bool ShouldSkipBackgroundRefresh(LobbySyncPush lobby)
        {
            return lobby?.IsPlaying == true ||
                   BattleResultFlowManager.IsBattleResultFlowPending ||
                   SettlementOverlayController.IsAnyMdenOverlayActive;
        }

        private static string BuildLobbyRefreshKey(LobbySyncPush lobby)
        {
            if (lobby == null) return string.Empty;

            var players = lobby.PlayerDetails != null && lobby.PlayerDetails.Length > 0
                ? string.Join("|", lobby.PlayerDetails
                    .Where(player => !string.IsNullOrWhiteSpace(player?.Uid))
                    .OrderBy(player => player.Uid)
                    .Select(player => string.Join(":",
                        player.Uid,
                        player.Name ?? string.Empty,
                        player.Title ?? string.Empty,
                        player.Bio ?? string.Empty,
                        player.ChatColor ?? string.Empty,
                        player.Status)))
                : JoinOrdered(lobby.Players);

            return string.Join("#",
                lobby.Id,
                lobby.Name ?? string.Empty,
                lobby.HostUid ?? string.Empty,
                lobby.HostName ?? string.Empty,
                lobby.PlayMode,
                lobby.PlayType,
                lobby.ChartSelection,
                lobby.Goal,
                lobby.MaxPlayers,
                lobby.PlaylistSize,
                lobby.SettlementEnabled,
                lobby.IsPrivate,
                lobby.JoinLocked,
                lobby.Locked,
                lobby.IsPlaying,
                lobby.WatcherCount,
                lobby.CurrentPlaylistEntry,
                lobby.CurrentBattleId ?? string.Empty,
                lobby.CurrentBattleEntry ?? string.Empty,
                JoinOrdered(lobby.ReadyPlayers),
                JoinOrdered(lobby.MutedPlayers),
                JoinOrdered(lobby.ChartSelectBannedPlayers),
                JoinDifficulties(lobby.ReadyPlayerDifficulties),
                JoinDifficulties(lobby.CurrentBattleDifficulties),
                JoinPlaylist(lobby.Playlist),
                players);
        }

        private static string JoinDifficulties(LobbyPlayerDifficultyEntry[] values)
        {
            if (values == null || values.Length == 0) return string.Empty;

            return string.Join(",", values
                .Where(value => !string.IsNullOrWhiteSpace(value?.Uid))
                .OrderBy(value => value.Uid)
                .Select(value => $"{value.Uid}:{value.Difficulty}"));
        }

        private static string JoinOrdered(string[] values)
        {
            if (values == null || values.Length == 0) return string.Empty;
            return string.Join(",", values
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .OrderBy(value => value));
        }

        private static string JoinPlaylist(string[] playlist)
        {
            if (playlist == null || playlist.Length == 0) return string.Empty;
            return string.Join("|", playlist.Where(entry => !string.IsNullOrWhiteSpace(entry)));
        }

        private async Task LoadRatingLevelsForInitialShowAsync()
        {
            var uids = GetCurrentLobbyPlayerUids();
            if (uids.Length == 0) return;

            var results = await Task.WhenAll(uids.Select(LoadRatingLevelValueAsync));
            foreach (var result in results)
            {
                _ratingLevels[result.Uid] = result.Level;
                _loadingRatingLevels.Remove(result.Uid);
            }
        }

        private static string[] GetCurrentLobbyPlayerUids()
        {
            var lobby = LobbyManager.CurrentLobby;
            if (lobby?.PlayerDetails != null && lobby.PlayerDetails.Length > 0)
            {
                return lobby.PlayerDetails
                    .Where(player => !string.IsNullOrWhiteSpace(player?.Uid))
                    .Select(player => player.Uid)
                    .Distinct()
                    .ToArray();
            }

            return lobby?.Players?
                .Where(uid => !string.IsNullOrWhiteSpace(uid))
                .Distinct()
                .ToArray() ?? new string[0];
        }

        private void BuildList()
        {
            _window.Items.Clear();
            _playerItems.Clear();
            _lastSelectedIndex = -1;

            var lobby = LobbyManager.CurrentLobby;
            var summary = lobby == null
                ? "Not in lobby."
                : BuildRoomSummary(lobby);

            _btnLeave = new NativeListItem("- 退出房间 -", "离开当前联机房间");
            _btnLeave.Texture = ResourceManager.GetRandomBannerTexture() ?? ResourceManager.GetSprite("HomePanel.png")?.texture;
            _window.Items.Add(_btnLeave);

            if (lobby != null)
            {
                _btnPlayMode = new NativeListItem(
                    "游玩模式",
                    $"当前: {Highlight(LobbyRuleTextFormatter.GetPlayModeName(lobby.PlayMode), LobbyRuleTextFormatter.GetPlayModeColor(lobby.PlayMode))}\n点击切换为{Highlight(LobbyRuleTextFormatter.GetPlayModeName((byte)LobbyRuleTextFormatter.GetNextPlayMode(lobby.PlayMode)), LobbyRuleTextFormatter.GetPlayModeColor((byte)LobbyRuleTextFormatter.GetNextPlayMode(lobby.PlayMode)))}");
                _btnPlayMode.Texture = ResourceManager.GetRandomBannerTexture() ?? ResourceManager.GetSprite("OptionsPanel.png")?.texture;
                _window.Items.Add(_btnPlayMode);

                _btnGoal = new NativeListItem(
                    "获胜方式",
                    $"当前: {Highlight(GetGoalName(lobby.Goal), Constants.ColorYellow)}\n点击切换为{Highlight(GetGoalName(GetNextGoal(lobby.Goal)), Constants.ColorCyan)}");
                _btnGoal.Texture = ResourceManager.GetRandomBannerTexture() ?? ResourceManager.GetSprite("SocialNetwork.png")?.texture;
                _window.Items.Add(_btnGoal);

                _btnSettlement = new NativeListItem(
                    "结算功能",
                    $"当前: {Highlight(lobby.SettlementEnabled ? "开启" : "关闭", Constants.ColorYellow)}\n点击{Highlight(lobby.SettlementEnabled ? "关闭" : "开启", Constants.ColorCyan)}每五首结算");
                _btnSettlement.Texture = ResourceManager.GetRandomBannerTexture() ?? ResourceManager.GetSprite("OptionsPanel.png")?.texture;
                _window.Items.Add(_btnSettlement);
            }
            else
            {
                _btnPlayMode = null;
                _btnGoal = null;
                _btnSettlement = null;
            }

            if (lobby?.HostUid == PlayerManager.CurrentUid)
            {
                _btnJoinLock = new NativeListItem(
                    lobby.JoinLocked ? "- 手动解锁 -" : "- 手动上锁 -",
                    lobby.JoinLocked ? "解锁后其他玩家可以加入房间" : "上锁后其他玩家不能加入房间");
                _btnJoinLock.Texture = ResourceManager.GetRandomBannerTexture() ?? ResourceManager.GetSprite("OptionsPanel.png")?.texture;
                _window.Items.Add(_btnJoinLock);

                _btnPassword = new NativeListItem(
                    lobby.IsPrivate ? "- 修改/清除密码 -" : "- 设置密码 -",
                    lobby.IsPrivate ? "当前房间需要密码加入，输入空内容可清除密码" : "设置后房间列表会显示（私密），加入时需要输入密码");
                _btnPassword.Texture = ResourceManager.GetRandomBannerTexture() ?? ResourceManager.GetSprite("SocialNetwork.png")?.texture;
                _window.Items.Add(_btnPassword);
            }
            else
            {
                _btnJoinLock = null;
                _btnPassword = null;
            }

            var roomName = Highlight(EscapeRichText(lobby?.Name ?? "我的房间"), Constants.ColorYellow);
            var info = new NativeListItem(roomName, summary);
            info.Texture = ResourceManager.GetRandomBannerTexture() ?? ResourceManager.GetSprite("RoomList.png")?.texture;
            _window.Items.Add(info);

            if (lobby?.PlayerDetails != null && lobby.PlayerDetails.Length > 0)
            {
                foreach (var player in lobby.PlayerDetails)
                {
                    var displayName = string.IsNullOrEmpty(player.Name) ? player.Uid : player.Name;
                    var name = player.Uid == lobby.HostUid
                        ? $"<color={Constants.ColorPink}>{displayName}</color>"
                        : displayName;
                    var entry = new PlayerSyncEntry
                    {
                        Uid = player.Uid,
                        Name = displayName,
                        Bio = player.Bio,
                        Title = player.Title,
                        ChatColor = player.ChatColor,
                        AvatarName = player.AvatarName,
                        AvatarData = player.AvatarData,
                        PingMS = player.PingMS,
                        Status = player.Status
                    };
                    var item = new NativeListItem(name, BuildPlayerDescription(entry));
                    item.Texture = AvatarManager.GetAvatarTexture(entry.Uid, entry.AvatarName, entry.AvatarData);
                    _window.Items.Add(item);
                    _playerItems[item] = entry;
                    RequestRatingLevel(entry.Uid);
                }
            }
            else if (lobby?.Players != null)
            {
                foreach (var uid in lobby.Players.Where(uid => !string.IsNullOrEmpty(uid)))
                {
                    var displayName = uid == PlayerManager.CurrentUid && !string.IsNullOrEmpty(PlayerManager.CurrentProfile?.Name)
                        ? PlayerManager.CurrentProfile.Name
                        : uid;
                    var name = uid == lobby.HostUid ? $"<color={Constants.ColorPink}>{displayName}</color>" : displayName;
                    var entry = new PlayerSyncEntry
                    {
                        Uid = uid,
                        Name = displayName,
                        Bio = uid == PlayerManager.CurrentUid ? PlayerManager.CurrentProfile?.Bio : null,
                        Title = uid == PlayerManager.CurrentUid ? PlayerManager.CurrentProfile?.Title : null,
                        ChatColor = uid == PlayerManager.CurrentUid ? PlayerManager.CurrentProfile?.ChatColor : null,
                        AvatarName = uid == PlayerManager.CurrentUid ? PlayerManager.CurrentProfile?.AvatarName : null,
                        AvatarData = uid == PlayerManager.CurrentUid ? PlayerManager.CurrentProfile?.AvatarData : null
                    };
                    var item = new NativeListItem(name, BuildPlayerDescription(entry));
                    item.Texture = AvatarManager.GetAvatarTexture(entry.Uid, entry.AvatarName, entry.AvatarData);
                    _window.Items.Add(item);
                    _playerItems[item] = entry;
                    RequestRatingLevel(entry.Uid);
                }
            }
        }

        private string BuildPlayerDescription(PlayerSyncEntry player)
        {
            var uid = player?.Uid;
            var loaded = !string.IsNullOrWhiteSpace(uid) && _ratingLevels.ContainsKey(uid);
            var rl = loaded ? _ratingLevels[uid] : null;
            return PlayerInfoDescriptionFormatter.Build(player, rl, loaded);
        }

        private void RequestRatingLevel(string uid)
        {
            if (string.IsNullOrWhiteSpace(uid) ||
                _ratingLevels.ContainsKey(uid) ||
                !_loadingRatingLevels.Add(uid))
            {
                return;
            }

            _ = LoadRatingLevelAsync(uid);
        }

        private async Task LoadRatingLevelAsync(string uid)
        {
            var result = await LoadRatingLevelValueAsync(uid);

            MainThreadDispatcher.Enqueue(() =>
            {
                if (IsDisposed) return;
                _ratingLevels[result.Uid] = result.Level;
                _loadingRatingLevels.Remove(result.Uid);
            });
        }

        private static async Task<RatingLevelLoadResult> LoadRatingLevelValueAsync(string uid)
        {
            return new RatingLevelLoadResult(uid, await MuseDashMoeProfileManager.GetRatingLevelAsync(uid));
        }

        private readonly struct RatingLevelLoadResult
        {
            public RatingLevelLoadResult(string uid, double? level)
            {
                Uid = uid;
                Level = level;
            }

            public string Uid { get; }
            public double? Level { get; }
        }

        private static string BuildRoomSummary(MDEN.Protocol.Messages.Lobby.LobbySyncPush lobby)
        {
            var hostName = EscapeRichText(lobby.HostName ?? lobby.HostUid ?? "Unknown");
            return $"房主: {Highlight(hostName, Constants.ColorPink)}\n" +
                   $"人数: {Highlight($"{GetPlayerCount(lobby)}/{lobby.MaxPlayers}", Constants.ColorCyan)}\n" +
                   $"游玩模式: {Highlight(LobbyRuleTextFormatter.GetPlayModeName(lobby.PlayMode), LobbyRuleTextFormatter.GetPlayModeColor(lobby.PlayMode))}\n" +
                   $"歌曲列表: {Highlight($"{GetPlaylistCount(lobby)}/{lobby.PlaylistSize}", Constants.ColorYellow)}\n" +
                   $"获胜方式: {Highlight(GetGoalName(lobby.Goal), Constants.ColorYellow)}\n" +
                   $"结算功能: {Highlight(lobby.SettlementEnabled ? "开启" : "关闭", Constants.ColorYellow)}\n" +
                   $"加入限制: {Highlight(lobby.JoinLocked ? "已上锁" : "开放", lobby.JoinLocked ? Constants.ColorYellow : RoomListWindow.WaitingStatusColor)}\n" +
                   $"密码: {Highlight(lobby.IsPrivate ? "已设置" : "无", lobby.IsPrivate ? Constants.ColorYellow : RoomListWindow.WaitingStatusColor)}\n" +
                   $"状态: {RoomListWindow.GetColoredLobbyStatus(lobby.IsPlaying, lobby.Locked, lobby.JoinLocked)}";
        }

        private static int GetPlayerCount(MDEN.Protocol.Messages.Lobby.LobbySyncPush lobby)
        {
            if (lobby.PlayerDetails != null && lobby.PlayerDetails.Length > 0)
            {
                return lobby.PlayerDetails
                    .Where(player => !string.IsNullOrEmpty(player?.Uid))
                    .Select(player => player.Uid)
                    .Distinct()
                    .Count();
            }

            return lobby.Players?
                .Where(uid => !string.IsNullOrEmpty(uid))
                .Distinct()
                .Count() ?? 0;
        }

        private static int GetPlaylistCount(MDEN.Protocol.Messages.Lobby.LobbySyncPush lobby)
        {
            return lobby.Playlist?
                .Where(entry => !string.IsNullOrEmpty(entry))
                .Distinct()
                .Count() ?? 0;
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

        private static string Highlight(string value, string color)
        {
            return $"<color={color}>{value}</color>";
        }

        private static string EscapeRichText(string value)
        {
            return string.IsNullOrEmpty(value)
                ? string.Empty
                : value.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
        }

        private void OnSelectionChanged(INativeListWindow window, int objectIndex)
        {
            if (_window == null || objectIndex < 0 || objectIndex >= _window.Items.Count) return;

            if (_lastSelectedIndex != objectIndex)
            {
                _lastSelectedIndex = objectIndex;
                return;
            }

            var button = _window.Items[objectIndex];
            if (button == _btnLeave)
            {
                if (!CanLeaveLobby(LobbyManager.CurrentLobby)) return;

                NativeConfirmDialog.Show(
                    string.Empty,
                    "是否退出房间？",
                    confirmed =>
                    {
                        if (confirmed)
                        {
                            _ = LeaveLobbyAsync();
                        }
                    });
                return;
            }

            if (button == _btnPlayMode)
            {
                var lobby = LobbyManager.CurrentLobby;
                if (!CanChangeRoomRules(lobby)) return;

                _ = UpdateLobbySettingsAsync(
                    () => LobbyManager.SetPlayModeAsync((byte)LobbyRuleTextFormatter.GetNextPlayMode(lobby.PlayMode)),
                    LocalLobbySettingsChange.ForPlayMode(
                        lobby.JoinLocked,
                        (byte)LobbyRuleTextFormatter.GetNextPlayMode(lobby.PlayMode)));
                return;
            }

            if (button == _btnGoal)
            {
                var lobby = LobbyManager.CurrentLobby;
                if (!CanChangeRoomRules(lobby)) return;

                var nextGoal = GetNextGoal(lobby.Goal);
                _ = UpdateLobbySettingsAsync(
                    () => LobbyManager.SetGoalAsync(nextGoal),
                    LocalLobbySettingsChange.ForGoal(lobby.JoinLocked, nextGoal));
                return;
            }

            if (button == _btnSettlement)
            {
                var lobby = LobbyManager.CurrentLobby;
                if (!CanChangeRoomRules(lobby)) return;

                var settlementEnabled = !lobby.SettlementEnabled;
                _ = UpdateLobbySettingsAsync(
                    () => LobbyManager.SetSettlementEnabledAsync(settlementEnabled),
                    LocalLobbySettingsChange.Settlement(lobby.JoinLocked, settlementEnabled));
                return;
            }

            if (button == _btnJoinLock)
            {
                var lobby = LobbyManager.CurrentLobby;
                if (!CanChangeRoomSettings(lobby)) return;

                var joinLocked = !lobby.JoinLocked;
                _ = UpdateLobbySettingsAsync(
                    () => LobbyManager.SetJoinLockedAsync(joinLocked),
                    LocalLobbySettingsChange.JoinLock(joinLocked));
                return;
            }

            if (button == _btnPassword)
            {
                if (!CanChangeRoomSettings(LobbyManager.CurrentLobby)) return;

                ShowPasswordInput();
                return;
            }

            if (_playerItems.TryGetValue(button, out var player))
            {
                Close();
                WindowStackController.OpenWindow(new RoomPlayerWindow(player));
            }
        }

        private static bool CanChangeRoomSettings(MDEN.Protocol.Messages.Lobby.LobbySyncPush lobby)
        {
            if (lobby == null) return false;
            if (lobby.HostUid == PlayerManager.CurrentUid) return true;

            Il2CppAssets.Scripts.UI.Controls.ShowText.ShowInfo("只有房主能进行该操作");
            return false;
        }

        private static bool CanChangeRoomRules(MDEN.Protocol.Messages.Lobby.LobbySyncPush lobby)
        {
            if (!CanChangeRoomSettings(lobby)) return false;
            if (!lobby.Locked && !lobby.IsPlaying) return true;

            Il2CppAssets.Scripts.UI.Controls.ShowText.ShowInfo("游戏准备或进行中，不能修改房间规则");
            return false;
        }

        private static bool CanLeaveLobby(MDEN.Protocol.Messages.Lobby.LobbySyncPush lobby)
        {
            if (lobby == null) return false;
            if (!lobby.Locked && !lobby.IsPlaying) return true;
            if (IsHostKnownOffline(lobby)) return true;

            Il2CppAssets.Scripts.UI.Controls.ShowText.ShowInfo("请先停止游戏");
            return false;
        }

        private static bool IsHostKnownOffline(MDEN.Protocol.Messages.Lobby.LobbySyncPush lobby)
        {
            if (lobby?.PlayerDetails == null || string.IsNullOrWhiteSpace(lobby.HostUid)) return false;

            foreach (var player in lobby.PlayerDetails)
            {
                if (player?.Uid != lobby.HostUid) continue;
                return (PlayerStatus)player.Status == PlayerStatus.Offline;
            }

            return false;
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
                if (value != null && value.Length > 16)
                {
                    MDEN.Managers.ClientLogManager.Warning("Lobby password is too long. Max length is 16.");
                    MainThreadDispatcher.Enqueue(RebuildWindow);
                    return;
                }

                _ = UpdateLobbySettingsAsync(
                    () => LobbyManager.SetPasswordAsync(value),
                    LocalLobbySettingsChange.ForPassword(LobbyManager.CurrentLobby?.JoinLocked == true, value));
            };
            input.Show();
        }

        private async Task UpdateLobbySettingsAsync(Func<Task> updateAsync, LocalLobbySettingsChange localChange)
        {
            IDisposable uiLock = WindowStackController.LockUI("处理中...");

            try
            {
                await updateAsync();
                await MainThreadDispatcher.InvokeAsync(() => ApplyLocalLobbySettings(localChange));
                if (IsDisposed) return;

                await MainThreadDispatcher.InvokeAsync(() =>
                {
                    if (IsDisposed) return;
                    RebuildWindow();
                });
            }
            catch (System.Exception ex)
            {
                MDEN.Managers.ClientLogManager.Warning($"Update lobby settings failed: {ex.Message}");
                MainThreadDispatcher.Enqueue(RebuildWindow);
            }
            finally
            {
                await MainThreadDispatcher.InvokeAsync(() => uiLock?.Dispose());
            }
        }

        private static void ApplyLocalLobbySettings(LocalLobbySettingsChange change)
        {
            var lobby = LobbyManager.CurrentLobby;
            if (lobby == null || change == null) return;

            lobby.JoinLocked = change.JoinLocked;
            if (change.UpdatePassword)
            {
                lobby.IsPrivate = !string.IsNullOrWhiteSpace(change.Password);
            }

            if (change.UpdateGoal)
            {
                lobby.Goal = change.Goal;
            }

            if (change.UpdateSettlementEnabled)
            {
                lobby.SettlementEnabled = change.SettlementEnabled;
            }

            if (change.UpdatePlayMode)
            {
                lobby.PlayMode = change.PlayMode;
                lobby.ReadyPlayerDifficulties = new LobbyPlayerDifficultyEntry[0];
                lobby.CurrentBattleDifficulties = new LobbyPlayerDifficultyEntry[0];
                lobby.TenziSelectedEntry = null;
                lobby.TenziRoundClosed = false;
                lobby.TenziDrawSeed = 0;
                lobby.PlaylistOwners = new LobbyPlaylistOwnerEntry[0];
                if (LobbyPlayModeRules.IsTenzi(change.PlayMode))
                {
                    lobby.Playlist = new string[0];
                    lobby.CurrentPlaylistEntry = 0;
                }
            }
        }

        private sealed class LocalLobbySettingsChange
        {
            public bool JoinLocked { get; private set; }
            public bool UpdatePassword { get; private set; }
            public string Password { get; private set; }
            public bool UpdateGoal { get; private set; }
            public byte Goal { get; private set; }
            public bool UpdateSettlementEnabled { get; private set; }
            public bool SettlementEnabled { get; private set; }
            public bool UpdatePlayMode { get; private set; }
            public byte PlayMode { get; private set; }

            public static LocalLobbySettingsChange JoinLock(bool joinLocked)
            {
                return new LocalLobbySettingsChange { JoinLocked = joinLocked };
            }

            public static LocalLobbySettingsChange ForPassword(bool joinLocked, string password)
            {
                return new LocalLobbySettingsChange
                {
                    JoinLocked = joinLocked,
                    UpdatePassword = true,
                    Password = password
                };
            }

            public static LocalLobbySettingsChange ForGoal(bool joinLocked, byte goal)
            {
                return new LocalLobbySettingsChange
                {
                    JoinLocked = joinLocked,
                    UpdateGoal = true,
                    Goal = goal
                };
            }

            public static LocalLobbySettingsChange Settlement(bool joinLocked, bool settlementEnabled)
            {
                return new LocalLobbySettingsChange
                {
                    JoinLocked = joinLocked,
                    UpdateSettlementEnabled = true,
                    SettlementEnabled = settlementEnabled
                };
            }

            public static LocalLobbySettingsChange ForPlayMode(bool joinLocked, byte playMode)
            {
                return new LocalLobbySettingsChange
                {
                    JoinLocked = joinLocked,
                    UpdatePlayMode = true,
                    PlayMode = playMode
                };
            }
        }

        private static byte GetNextGoal(byte goal)
        {
            return (byte)((LobbyGoal)goal == LobbyGoal.Accuracy ? LobbyGoal.Score : LobbyGoal.Accuracy);
        }

        private async System.Threading.Tasks.Task LeaveLobbyAsync()
        {
            IDisposable uiLock = WindowStackController.LockUI("Leaving lobby...");

            try
            {
                await LobbyManager.LeaveLobbyAsync();
                if (IsDisposed) return;

                await MainThreadDispatcher.InvokeAsync(() =>
                {
                    if (IsDisposed) return;
                    Close();
                    NavigationButton.RefreshRoomButton();
                    WindowStackController.OpenWindow(new RoomListWindow());
                });
            }
            catch (System.Exception ex)
            {
                MDEN.Managers.ClientLogManager.Warning($"Leave lobby failed: {ex.Message}");
                MainThreadDispatcher.Enqueue(() => Il2CppAssets.Scripts.UI.Controls.ShowText.ShowInfo(ex.Message));
            }
            finally
            {
                await MainThreadDispatcher.InvokeAsync(() => uiLock?.Dispose());
            }
        }

        public override void Close()
        {
            _lastSelectedIndex = -1;
            if (_window != null)
            {
                _window.ForceClose();
                _window = null;
            }
        }

        private void RebuildWindow()
        {
            if (_window == null) return;

            _window.OnSelectionChanged -= OnSelectionChanged;
            _window.ForceClose();
            _window = new NativeListWindow();
            _window.AutoReset = true;
            _window.Title = "我的房间";
            BuildList();
            _lastLobbyRefreshKey = BuildLobbyRefreshKey(LobbyManager.CurrentLobby);
            _window.OnSelectionChanged += OnSelectionChanged;
            _window.Show();
            _lastSelectedIndex = -1;
        }

        private void RefreshWindowContent()
        {
            if (_window == null) return;

            using (PerfTrace.Measure("MDEN.MyRoomWindow.RefreshWindowContent"))
            {
                BuildList();
                _lastLobbyRefreshKey = BuildLobbyRefreshKey(LobbyManager.CurrentLobby);
                _lastSelectedIndex = -1;
            }
        }
    }
}
