using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using LocalizeLib;
using MDEN.Managers;
using MDEN.Protocol.Enums;
using MDEN.Protocol.Messages.Lobby;
using MDEN.Protocol.Models;
using MDEN.Protocol.Rules;
using MDEN.UI.Core;
using PopupLib.UI.Components;
using PopupLib.UI.Windows;
using UnityEngine;

namespace MDEN.UI.Windows
{
    public class MyRoomWindow : MDENWindowBase
    {
        private ForumWindow _window;
        private ForumObject _btnLeave;
        private ForumObject _btnPlayMode;
        private ForumObject _btnTenziSongsPerPlayer;
        private ForumObject _btnGoal;
        private ForumObject _btnSettlement;
        private ForumObject _btnMaxPlayers;
        private ForumObject _btnPlaylistSize;
        private ForumObject _btnJoinLock;
        private ForumObject _btnPassword;
        private readonly Dictionary<ForumObject, PlayerSyncEntry> _playerItems = new Dictionary<ForumObject, PlayerSyncEntry>();
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

            _window = new ForumWindow();
            _window.AutoReset = true;
            BuildList();
            _lastLobbyRefreshKey = BuildLobbyRefreshKey(LobbyManager.CurrentLobby);
            LobbyManager.CurrentLobbyChanged += HandleCurrentLobbyChanged;
            _window.OnSelectionChanged += OnSelectionChanged;
            _window.OnInternalShow += OnInternalShowInjectTitle;
            _window.Show();
            _lastSelectedIndex = -1;

            RegisterEventCleanup(() =>
            {
                LobbyManager.CurrentLobbyChanged -= HandleCurrentLobbyChanged;

                if (_window != null)
                {
                    _window.OnSelectionChanged -= OnSelectionChanged;
                    _window.OnInternalShow -= OnInternalShowInjectTitle;
                }

                RemoveInjectedTitle();
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
                lobby.TenziSongsPerPlayer,
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
            _window.ForumObjects.Clear();
            _playerItems.Clear();
            _lastSelectedIndex = -1;

            var lobby = LobbyManager.CurrentLobby;
            var summary = lobby == null
                ? I18nManager.T("room.not_in_lobby")
                : BuildRoomSummary(lobby);

            _btnLeave = new ForumObject(new LocalString(I18nManager.T("room.leave.button")), new LocalString(I18nManager.T("room.leave.desc")));
            _btnLeave.Texture = ResourceManager.GetRandomBannerTexture() ?? ResourceManager.GetSprite("HomePanel.png")?.texture;
            _window.ForumObjects.Add(_btnLeave);

            if (lobby != null)
            {
                _btnPlayMode = new ForumObject(
                    new LocalString(I18nManager.T("create.play_mode.title")),
                    new LocalString(BuildPlayModeSettingDescription(lobby.PlayMode)));
                _btnPlayMode.Texture = ResourceManager.GetRandomBannerTexture() ?? ResourceManager.GetSprite("OptionsPanel.png")?.texture;
                _window.ForumObjects.Add(_btnPlayMode);

                if (LobbyPlayModeRules.IsTenzi(lobby.PlayMode))
                {
                    _btnTenziSongsPerPlayer = new ForumObject(
                        new LocalString(I18nManager.T("tenzi.songs_per_player.title")),
                        new LocalString(I18nManager.Tf("tenzi.songs_per_player.room_desc", Highlight(GetTenziSongsPerPlayer(lobby).ToString(), Constants.ColorYellow), LobbyPlayModeRules.GetTenziSongsPerPlayerMax(lobby.PlaylistSize))));
                    _btnTenziSongsPerPlayer.Texture = ResourceManager.GetRandomBannerTexture() ?? ResourceManager.GetSprite("RoomList.png")?.texture;
                    _window.ForumObjects.Add(_btnTenziSongsPerPlayer);
                }
                else
                {
                    _btnTenziSongsPerPlayer = null;
                }

                _btnGoal = new ForumObject(
                    new LocalString(I18nManager.T("create.goal.title")),
                    new LocalString(I18nManager.Tf("room.setting.desc", Highlight(GetGoalName(lobby.Goal), Constants.ColorYellow), Highlight(GetGoalName(GetNextGoal(lobby.Goal)), Constants.ColorCyan))));
                _btnGoal.Texture = ResourceManager.GetRandomBannerTexture() ?? ResourceManager.GetSprite("SocialNetwork.png")?.texture;
                _window.ForumObjects.Add(_btnGoal);

                _btnSettlement = new ForumObject(
                    new LocalString(I18nManager.T("create.settlement.title")),
                    new LocalString(I18nManager.Tf("room.settlement.desc", Highlight(lobby.SettlementEnabled ? I18nManager.T("common.enabled") : I18nManager.T("common.disabled"), Constants.ColorYellow), Highlight(lobby.SettlementEnabled ? I18nManager.T("common.disabled") : I18nManager.T("common.enabled"), Constants.ColorCyan))));
                _btnSettlement.Texture = ResourceManager.GetRandomBannerTexture() ?? ResourceManager.GetSprite("OptionsPanel.png")?.texture;
                _window.ForumObjects.Add(_btnSettlement);
            }
            else
            {
                _btnPlayMode = null;
                _btnTenziSongsPerPlayer = null;
                _btnGoal = null;
                _btnSettlement = null;
            }

            if (lobby?.HostUid == PlayerManager.CurrentUid)
            {
                _btnMaxPlayers = new ForumObject(
                    new LocalString(I18nManager.T("create.players.title")),
                    new LocalString(I18nManager.Tf("room.players.desc", Highlight(lobby.MaxPlayers.ToString(), Constants.ColorYellow))));
                _btnMaxPlayers.Texture = ResourceManager.GetRandomBannerTexture() ?? ResourceManager.GetSprite("PlayerCard.png")?.texture;
                _window.ForumObjects.Add(_btnMaxPlayers);

                _btnPlaylistSize = new ForumObject(
                    new LocalString(I18nManager.T("create.playlist_size.title")),
                    new LocalString(I18nManager.Tf("room.playlist_size.desc", Highlight(lobby.PlaylistSize.ToString(), Constants.ColorYellow))));
                _btnPlaylistSize.Texture = ResourceManager.GetRandomBannerTexture() ?? ResourceManager.GetSprite("RoomList.png")?.texture;
                _window.ForumObjects.Add(_btnPlaylistSize);

                _btnJoinLock = new ForumObject(
                    new LocalString(lobby.JoinLocked ? I18nManager.T("room.unlock.button") : I18nManager.T("room.lock.button")),
                    new LocalString(lobby.JoinLocked ? I18nManager.T("room.unlock.desc") : I18nManager.T("room.lock.desc")));
                _btnJoinLock.Texture = ResourceManager.GetRandomBannerTexture() ?? ResourceManager.GetSprite("OptionsPanel.png")?.texture;
                _window.ForumObjects.Add(_btnJoinLock);

                _btnPassword = new ForumObject(
                    new LocalString(lobby.IsPrivate ? I18nManager.T("room.password.change.button") : I18nManager.T("room.password.set.button")),
                    new LocalString(lobby.IsPrivate ? I18nManager.T("room.password.change.desc") : I18nManager.T("room.password.set.desc")));
                _btnPassword.Texture = ResourceManager.GetRandomBannerTexture() ?? ResourceManager.GetSprite("SocialNetwork.png")?.texture;
                _window.ForumObjects.Add(_btnPassword);
            }
            else
            {
                _btnMaxPlayers = null;
                _btnPlaylistSize = null;
                _btnJoinLock = null;
                _btnPassword = null;
            }

            var roomName = Highlight(EscapeRichText(lobby?.Name ?? I18nManager.T("room.title")), Constants.ColorYellow);
            var info = new ForumObject(new LocalString(roomName), new LocalString(summary));
            info.Texture = ResourceManager.GetRandomBannerTexture() ?? ResourceManager.GetSprite("RoomList.png")?.texture;
            _window.ForumObjects.Add(info);

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
                        Status = player.Status,
                        TotalMultiplayerGames = player.TotalMultiplayerGames
                    };
                    var item = new ForumObject(new LocalString(name), new LocalString(BuildPlayerDescription(entry)));
                    item.Texture = AvatarManager.GetAvatarTexture(entry.Uid, entry.AvatarName, entry.AvatarData);
                    _window.ForumObjects.Add(item);
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
                        AvatarData = uid == PlayerManager.CurrentUid ? PlayerManager.CurrentProfile?.AvatarData : null,
                        TotalMultiplayerGames = uid == PlayerManager.CurrentUid
                            ? PlayerManager.CurrentProfile?.TotalMultiplayerGames ?? 0
                            : 0
                    };
                    var item = new ForumObject(new LocalString(name), new LocalString(BuildPlayerDescription(entry)));
                    item.Texture = AvatarManager.GetAvatarTexture(entry.Uid, entry.AvatarName, entry.AvatarData);
                    _window.ForumObjects.Add(item);
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
            var hostName = EscapeRichText(lobby.HostName ?? lobby.HostUid ?? I18nManager.T("common.unknown"));
            var playlistText = LobbyPlayModeRules.IsTenzi(lobby.PlayMode)
                ? I18nManager.Tf("room.playlist.tenzi", $"{GetPlaylistCount(lobby)}/{lobby.PlaylistSize}", GetTenziSongsPerPlayer(lobby))
                : $"{GetPlaylistCount(lobby)}/{lobby.PlaylistSize}";
            return I18nManager.Tf(
                "room.summary",
                Highlight(hostName, Constants.ColorPink),
                Highlight($"{GetPlayerCount(lobby)}/{lobby.MaxPlayers}", Constants.ColorCyan),
                Highlight(LobbyRuleTextFormatter.GetPlayModeName(lobby.PlayMode), LobbyRuleTextFormatter.GetPlayModeColor(lobby.PlayMode)),
                Highlight(playlistText, Constants.ColorYellow),
                Highlight(GetGoalName(lobby.Goal), Constants.ColorYellow),
                Highlight(lobby.SettlementEnabled ? I18nManager.T("common.enabled") : I18nManager.T("common.disabled"), Constants.ColorYellow),
                Highlight(lobby.JoinLocked ? I18nManager.T("common.locked") : I18nManager.T("common.open"), lobby.JoinLocked ? Constants.ColorYellow : RoomListWindow.WaitingStatusColor),
                Highlight(lobby.IsPrivate ? I18nManager.T("common.set") : I18nManager.T("common.no"), lobby.IsPrivate ? Constants.ColorYellow : RoomListWindow.WaitingStatusColor),
                RoomListWindow.GetColoredLobbyStatus(lobby.IsPlaying, lobby.Locked, lobby.JoinLocked));
        }

        private static string BuildPlayModeSettingDescription(byte playMode)
        {
            return I18nManager.Tf(
                "room.setting.desc",
                Highlight(LobbyRuleTextFormatter.GetPlayModeName(playMode), LobbyRuleTextFormatter.GetPlayModeColor(playMode)),
                Highlight(LobbyRuleTextFormatter.GetPlayModeName((byte)LobbyRuleTextFormatter.GetNextPlayMode(playMode)), LobbyRuleTextFormatter.GetPlayModeColor((byte)LobbyRuleTextFormatter.GetNextPlayMode(playMode)))) +
                   "\n" +
                   LobbyRuleTextFormatter.GetPlayModeDescription(playMode);
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
                LobbyGoal.Score => I18nManager.T("lobby.goal.score"),
                LobbyGoal.Custom => I18nManager.T("lobby.goal.custom"),
                _ => I18nManager.T("lobby.goal.accuracy")
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

        private void OnSelectionChanged(PopupLib.UI.Windows.Interfaces.IListWindow window, int objectIndex)
        {
            if (_window == null || objectIndex < 0 || objectIndex >= _window.ForumObjects.Count) return;

            if (_lastSelectedIndex != objectIndex)
            {
                _lastSelectedIndex = objectIndex;
                return;
            }

            var button = _window.ForumObjects[objectIndex];
            if (button == _btnLeave)
            {
                if (!CanLeaveLobby(LobbyManager.CurrentLobby)) return;

                NativeConfirmDialog.Show(
                    string.Empty,
                    I18nManager.T("room.leave.confirm"),
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

            if (button == _btnTenziSongsPerPlayer)
            {
                var lobby = LobbyManager.CurrentLobby;
                if (!CanChangeRoomRules(lobby)) return;

                ShowTenziSongsPerPlayerInput(lobby);
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

            if (button == _btnMaxPlayers)
            {
                var lobby = LobbyManager.CurrentLobby;
                if (!CanChangeRoomRules(lobby)) return;

                ShowMaxPlayersInput(lobby);
                return;
            }

            if (button == _btnPlaylistSize)
            {
                var lobby = LobbyManager.CurrentLobby;
                if (!CanChangeRoomRules(lobby)) return;

                ShowPlaylistSizeInput(lobby);
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

            Il2CppAssets.Scripts.UI.Controls.ShowText.ShowInfo(I18nManager.T("room.host_only"));
            return false;
        }

        private static bool CanChangeRoomRules(MDEN.Protocol.Messages.Lobby.LobbySyncPush lobby)
        {
            if (!CanChangeRoomSettings(lobby)) return false;
            if (!lobby.Locked && !lobby.IsPlaying) return true;

            Il2CppAssets.Scripts.UI.Controls.ShowText.ShowInfo(I18nManager.T("room.rules_locked"));
            return false;
        }

        private static bool CanLeaveLobby(MDEN.Protocol.Messages.Lobby.LobbySyncPush lobby)
        {
            if (lobby == null) return false;
            if (!lobby.Locked && !lobby.IsPlaying) return true;
            if (IsHostKnownOffline(lobby)) return true;

            Il2CppAssets.Scripts.UI.Controls.ShowText.ShowInfo(I18nManager.T("room.stop_first"));
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

            var input = new InputWindow();
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

        private void ShowMaxPlayersInput(LobbySyncPush lobby)
        {
            if (lobby == null) return;

            ShowNumberInput(
                I18nManager.T("create.players.title"),
                2,
                10,
                value =>
                {
                    var playerCount = GetPlayerCount(LobbyManager.CurrentLobby ?? lobby);
                    if (value < playerCount)
                    {
                        Il2CppAssets.Scripts.UI.Controls.ShowText.ShowInfo(I18nManager.T("room.max_players.existing_limit"));
                        MainThreadDispatcher.Enqueue(RebuildWindow);
                        return;
                    }

                    _ = UpdateLobbySettingsAsync(
                        () => LobbyManager.SetMaxPlayersAsync((ushort)value),
                        LocalLobbySettingsChange.ForMaxPlayers(
                            LobbyManager.CurrentLobby?.JoinLocked == true,
                            (ushort)value));
                });
        }

        private void ShowPlaylistSizeInput(LobbySyncPush lobby)
        {
            if (lobby == null) return;

            ShowNumberInput(
                I18nManager.T("create.playlist_size.title"),
                2,
                32,
                value =>
                {
                    var currentLobby = LobbyManager.CurrentLobby ?? lobby;
                    var playlistCount = GetPlaylistCount(currentLobby);
                    if (value < playlistCount)
                    {
                        Il2CppAssets.Scripts.UI.Controls.ShowText.ShowInfo(I18nManager.T("room.playlist_size.existing_limit"));
                        MainThreadDispatcher.Enqueue(RebuildWindow);
                        return;
                    }

                    _ = UpdateLobbySettingsAsync(
                        () => LobbyManager.SetPlaylistSizeAsync((ushort)value),
                        LocalLobbySettingsChange.ForPlaylistSize(
                            currentLobby.JoinLocked,
                            (ushort)value,
                            LobbyPlayModeRules.NormalizeTenziSongsPerPlayer(currentLobby.TenziSongsPerPlayer, (ushort)value)));
                });
        }

        private void ShowTenziSongsPerPlayerInput(LobbySyncPush lobby)
        {
            if (lobby == null) return;

            var min = LobbyPlayModeRules.MinTenziSongsPerPlayer;
            var max = LobbyPlayModeRules.GetTenziSongsPerPlayerMax(lobby.PlaylistSize);
            if (_window != null)
            {
                _window.ForceClose();
            }

            var input = new InputWindow();
            input.OnCompletion += (w) =>
            {
                var value = input.Result?.Trim();
                if (!TryParseNumberInRange(value, min, max, out var parsed))
                {
                    Il2CppAssets.Scripts.UI.Controls.ShowText.ShowInfo(I18nManager.Tf("create.number_range", I18nManager.T("tenzi.songs_per_player.title"), min, max));
                    MainThreadDispatcher.Enqueue(RebuildWindow);
                    return;
                }

                if (!LobbyManager.CanSetTenziSongsPerPlayer((byte)parsed, out var blockedMessage))
                {
                    Il2CppAssets.Scripts.UI.Controls.ShowText.ShowInfo(blockedMessage);
                    MainThreadDispatcher.Enqueue(RebuildWindow);
                    return;
                }

                _ = UpdateLobbySettingsAsync(
                    () => LobbyManager.SetTenziSongsPerPlayerAsync((byte)parsed),
                    LocalLobbySettingsChange.ForTenziSongsPerPlayer(
                        LobbyManager.CurrentLobby?.JoinLocked == true,
                        (byte)parsed));
            };
            input.Show();
        }

        private void ShowNumberInput(string fieldName, int min, int max, Action<int> applyValue)
        {
            if (_window != null)
            {
                _window.ForceClose();
            }

            var input = new InputWindow();
            input.OnCompletion += (w) =>
            {
                var value = input.Result?.Trim();
                if (!TryParseNumberInRange(value, min, max, out var parsed))
                {
                    Il2CppAssets.Scripts.UI.Controls.ShowText.ShowInfo(I18nManager.Tf("create.number_range", fieldName, min, max));
                    MainThreadDispatcher.Enqueue(RebuildWindow);
                    return;
                }

                applyValue(parsed);
            };
            input.Show();
        }

        private async Task UpdateLobbySettingsAsync(Func<Task> updateAsync, LocalLobbySettingsChange localChange)
        {
            IDisposable uiLock = WindowStackController.LockUI(I18nManager.T("common.processing"));

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
                MainThreadDispatcher.Enqueue(() =>
                {
                    Il2CppAssets.Scripts.UI.Controls.ShowText.ShowInfo(I18nManager.Tf("room.setting_failed", LobbyManager.FormatSettingsFailureMessage(ex.Message)));
                    RebuildWindow();
                });
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

            if (change.UpdateMaxPlayers)
            {
                lobby.MaxPlayers = change.MaxPlayers;
            }

            if (change.UpdatePlaylistSize)
            {
                lobby.PlaylistSize = change.PlaylistSize;
                lobby.TenziSongsPerPlayer = LobbyPlayModeRules.NormalizeTenziSongsPerPlayer(
                    change.TenziSongsPerPlayer,
                    change.PlaylistSize);
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

            if (change.UpdateTenziSongsPerPlayer)
            {
                lobby.TenziSongsPerPlayer = change.TenziSongsPerPlayer;
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
            public bool UpdateMaxPlayers { get; private set; }
            public ushort MaxPlayers { get; private set; }
            public bool UpdatePlaylistSize { get; private set; }
            public ushort PlaylistSize { get; private set; }
            public bool UpdatePlayMode { get; private set; }
            public byte PlayMode { get; private set; }
            public bool UpdateTenziSongsPerPlayer { get; private set; }
            public byte TenziSongsPerPlayer { get; private set; }

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

            public static LocalLobbySettingsChange ForMaxPlayers(bool joinLocked, ushort maxPlayers)
            {
                return new LocalLobbySettingsChange
                {
                    JoinLocked = joinLocked,
                    UpdateMaxPlayers = true,
                    MaxPlayers = maxPlayers
                };
            }

            public static LocalLobbySettingsChange ForPlaylistSize(bool joinLocked, ushort playlistSize, byte tenziSongsPerPlayer)
            {
                return new LocalLobbySettingsChange
                {
                    JoinLocked = joinLocked,
                    UpdatePlaylistSize = true,
                    PlaylistSize = playlistSize,
                    TenziSongsPerPlayer = tenziSongsPerPlayer
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

            public static LocalLobbySettingsChange ForTenziSongsPerPlayer(bool joinLocked, byte value)
            {
                return new LocalLobbySettingsChange
                {
                    JoinLocked = joinLocked,
                    UpdateTenziSongsPerPlayer = true,
                    TenziSongsPerPlayer = value
                };
            }
        }

        private static byte GetNextGoal(byte goal)
        {
            return (byte)((LobbyGoal)goal == LobbyGoal.Accuracy ? LobbyGoal.Score : LobbyGoal.Accuracy);
        }

        private static byte GetTenziSongsPerPlayer(LobbySyncPush lobby)
        {
            return LobbyPlayModeRules.NormalizeTenziSongsPerPlayer(lobby?.TenziSongsPerPlayer ?? 0, lobby?.PlaylistSize ?? 2);
        }

        private static bool TryParseNumberInRange(string value, int min, int max, out int parsed)
        {
            parsed = 0;
            return !string.IsNullOrWhiteSpace(value) &&
                   int.TryParse(value, out parsed) &&
                   parsed >= min &&
                   parsed <= max;
        }

        private async System.Threading.Tasks.Task LeaveLobbyAsync()
        {
            IDisposable uiLock = WindowStackController.LockUI(I18nManager.T("room.leaving"));

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

            var newTitle = UnityEngine.Object.Instantiate(txtTittleObj.gameObject, imgBase);
            newTitle.name = "MDENTitle";
            newTitle.SetActive(true);

            var loc = newTitle.GetComponent<Il2CppAssets.Scripts.PeroTools.GeneralLocalization.Localization>();
            if (loc != null) UnityEngine.Object.Destroy(loc);

            var txt = newTitle.GetComponent<UnityEngine.UI.Text>();
            if (txt != null)
            {
                txt.text = I18nManager.T("room.title");
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
            _window.OnInternalShow -= OnInternalShowInjectTitle;
            _window.ForceClose();
            _window = new ForumWindow();
            _window.AutoReset = true;
            BuildList();
            _lastLobbyRefreshKey = BuildLobbyRefreshKey(LobbyManager.CurrentLobby);
            _window.OnSelectionChanged += OnSelectionChanged;
            _window.OnInternalShow += OnInternalShowInjectTitle;
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
