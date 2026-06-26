using System.Collections.Generic;
using System.Linq;
using MDEN.Managers;
using MDEN.Protocol.Messages.Lobby;
using MDEN.Protocol.Models;
using MDEN.UI.Core;
using MDEN.UI.Windows;
using UnityEngine;

namespace MDEN.UI.Displays
{
    public sealed class RoomPlayerListDisplay : LobbyDisplayBase
    {
        protected override string FrameParentPath => "UI/Standerd/PnlNavigation";
        protected override Vector2 AnchorPosition => new Vector2(-65f, -90f);
        protected override Vector2 Pivot => new Vector2(1f, 1f);
        protected override TextAnchor TextAnchor => TextAnchor.UpperLeft;
        protected override int FontSize => 26;
        protected override float EntryWidth => 460f;
        private const string NotReadyColor = "9a8b96ff";

        public void Update(LobbySyncPush lobby)
        {
            if (lobby == null)
            {
                Destroy();
                return;
            }

            Create();

            var activeKeys = new List<string> { "title" };
            var watcherInfo = lobby.WatcherCount > 0
                ? $"{I18nManager.T("room.watchers")}<color=#{Constants.ColorCyan}>{lobby.WatcherCount}</color> "
                : string.Empty;
            SetEntry(
                "title",
                $"{watcherInfo}{lobby.Name} <color=#{Constants.ColorYellow}>({GetPlayerCount(lobby)}/{lobby.MaxPlayers})</color>");

            foreach (var player in GetPlayers(lobby))
            {
                var key = $"player:{player.Uid}";
                activeKeys.Add(key);

                var hostPrefix = player.Uid == lobby.HostUid ? $"<color=#{Constants.ColorYellow}>{I18nManager.T("room.host.badge")}</color> " : string.Empty;
                var localColorStart = player.Uid == PlayerManager.CurrentUid ? $"<color=#{Constants.ColorCyan}>" : string.Empty;
                var localColorEnd = player.Uid == PlayerManager.CurrentUid ? "</color>" : string.Empty;
                var readyState = GetReadyStateText(lobby, player.Uid);
                var capturedPlayer = player;
                SetEntry(
                    key,
                    $"{hostPrefix}{localColorStart}{player.Name}{localColorEnd}{readyState}",
                    () => WindowStackController.OpenWindow(new RoomPlayerWindow(capturedPlayer)));
            }

            RemoveMissingEntries(activeKeys);
        }

        private static int GetPlayerCount(LobbySyncPush lobby)
        {
            if (lobby.PlayerDetails != null && lobby.PlayerDetails.Length > 0)
            {
                return lobby.PlayerDetails
                    .Where(player => !string.IsNullOrEmpty(player?.Uid))
                    .Select(player => player.Uid)
                    .Distinct()
                    .Count();
            }

            if (lobby.Players == null) return 0;
            return lobby.Players.Where(uid => !string.IsNullOrEmpty(uid)).Distinct().Count();
        }

        private static string GetReadyStateText(LobbySyncPush lobby, string uid)
        {
            if (lobby == null || !lobby.Locked || lobby.IsPlaying || string.IsNullOrEmpty(uid))
            {
                return string.Empty;
            }

            var isReady = lobby.ReadyPlayers != null && lobby.ReadyPlayers.Contains(uid);
            var color = isReady ? Constants.ColorGreen : NotReadyColor;
            var text = isReady ? I18nManager.T("room.player.ready") : I18nManager.T("room.player.not_ready");
            return $" <size=22><color=#{color}>{text}</color></size>";
        }

        private static IEnumerable<PlayerSyncEntry> GetPlayers(LobbySyncPush lobby)
        {
            var seenUids = new HashSet<string>();
            if (lobby.PlayerDetails != null && lobby.PlayerDetails.Length > 0)
            {
                foreach (var player in lobby.PlayerDetails)
                {
                    if (string.IsNullOrEmpty(player?.Uid)) continue;
                    if (!seenUids.Add(player.Uid)) continue;
                    yield return new PlayerSyncEntry
                    {
                        Uid = player.Uid,
                        Name = string.IsNullOrEmpty(player.Name) ? player.Uid : player.Name,
                        Bio = player.Bio,
                        Title = GetDisplayTitle(player),
                        ChatColor = GetDisplayColor(player),
                        AvatarName = player.AvatarName,
                        AvatarData = player.AvatarData,
                        PingMS = player.PingMS,
                        Status = player.Status
                    };
                }

                yield break;
            }

            if (lobby.Players == null) yield break;
            foreach (var uid in lobby.Players)
            {
                if (string.IsNullOrEmpty(uid)) continue;
                if (!seenUids.Add(uid)) continue;
                yield return new PlayerSyncEntry
                {
                    Uid = uid,
                    Name = uid == PlayerManager.CurrentUid && !string.IsNullOrEmpty(PlayerManager.CurrentProfile?.Name)
                        ? PlayerManager.CurrentProfile.Name
                        : uid,
                    Bio = uid == PlayerManager.CurrentUid ? PlayerManager.CurrentProfile?.Bio : null,
                    Title = uid == PlayerManager.CurrentUid ? PlayerManager.CurrentProfile?.Title : null,
                    ChatColor = uid == PlayerManager.CurrentUid ? PlayerManager.CurrentProfile?.ChatColor : null,
                    AvatarName = uid == PlayerManager.CurrentUid ? PlayerManager.CurrentProfile?.AvatarName : null,
                    AvatarData = uid == PlayerManager.CurrentUid ? PlayerManager.CurrentProfile?.AvatarData : null
                };
            }
        }

        private static string GetDisplayTitle(PlayerSyncEntry player)
        {
            if (player?.Uid == PlayerManager.CurrentUid && !string.IsNullOrEmpty(PlayerManager.CurrentProfile?.Title))
            {
                return PlayerManager.CurrentProfile.Title;
            }

            return player?.Title;
        }

        private static string GetDisplayColor(PlayerSyncEntry player)
        {
            if (player?.Uid == PlayerManager.CurrentUid && !string.IsNullOrEmpty(PlayerManager.CurrentProfile?.ChatColor))
            {
                return PlayerManager.CurrentProfile.ChatColor;
            }

            return player?.ChatColor;
        }
    }
}
