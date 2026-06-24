using System.Text.Json.Serialization;
using MDEN.Protocol.Models;

namespace MDEN.Protocol.Messages.Lobby
{
    public class CreateLobbyRequest
    {
        public string Name { get; set; }
        public ushort MaxPlayers { get; set; }
        public byte PlayMode { get; set; }
        public byte PlayType { get; set; }
        public byte ChartSelection { get; set; }
        public byte Goal { get; set; }
        public ushort PlaylistSize { get; set; }
        public bool SettlementEnabled { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string Password { get; set; }
    }

    public class CreateLobbyResponse
    {
        public int LobbyId { get; set; }
    }

    public class JoinLobbyRequest
    {
        public int LobbyId { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string Password { get; set; }
    }

    public class JoinLobbyResponse
    {
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string Reason { get; set; }
    }

    public class LobbyReadyRequest
    {
        public bool Ready { get; set; }
        public int Difficulty { get; set; }
    }

    public class LobbyReadyResponse
    {
    }

    public class LobbyKickRequest
    {
        public string TargetUid { get; set; }
    }

    public class LobbyKickResponse
    {
    }

    public class LobbyTransferHostRequest
    {
        public string TargetUid { get; set; }
    }

    public class LobbyTransferHostResponse
    {
    }

    public class LobbyMuteRequest
    {
        public string TargetUid { get; set; }
        public bool Muted { get; set; }
    }

    public class LobbyMuteResponse
    {
    }

    public class LobbyBanChartSelectRequest
    {
        public string TargetUid { get; set; }
        public bool Banned { get; set; }
    }

    public class LobbyBanChartSelectResponse
    {
    }

    public class LobbySettingsRequest
    {
        public bool JoinLocked { get; set; }
        public bool UpdatePassword { get; set; }
        public bool UpdateGoal { get; set; }
        public byte Goal { get; set; }
        public bool UpdateSettlementEnabled { get; set; }
        public bool SettlementEnabled { get; set; }
        public bool UpdatePlayMode { get; set; }
        public byte PlayMode { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string Password { get; set; }
    }

    public class LobbySettingsResponse
    {
    }

    public class GetCurrentLobbyRequest
    {
    }

    public class GetCurrentLobbyResponse
    {
        public LobbySyncPush Lobby { get; set; }
    }

    public class LobbyLockRequest
    {
        public bool Locked { get; set; }
    }

    public class LobbyLockResponse
    {
        public int Result { get; set; }
    }

    public class GetLobbiesResponse
    {
        public LobbyListEntry[] Lobbies { get; set; }
    }

    public class LobbySyncPush
    {
        public int Id { get; set; }
        public long Revision { get; set; }
        public string Name { get; set; }
        public string HostUid { get; set; }
        public string HostName { get; set; }
        public byte PlayMode { get; set; }
        public byte PlayType { get; set; }
        public byte ChartSelection { get; set; }
        public byte Goal { get; set; }
        public ushort MaxPlayers { get; set; }
        public ushort PlaylistSize { get; set; }
        public bool SettlementEnabled { get; set; }
        public bool IsPrivate { get; set; }
        public bool JoinLocked { get; set; }
        public bool Locked { get; set; }
        public bool IsPlaying { get; set; }
        public int WatcherCount { get; set; }
        public ushort CurrentPlaylistEntry { get; set; }
        public string CurrentBattleId { get; set; }
        public string CurrentBattleEntry { get; set; }
        public string[] Players { get; set; }
        public string[] ReadyPlayers { get; set; }
        public string[] MutedPlayers { get; set; }
        public string[] ChartSelectBannedPlayers { get; set; }
        public string[] Playlist { get; set; }
        public LobbyPlaylistOwnerEntry[] PlaylistOwners { get; set; }
        public string TenziSelectedEntry { get; set; }
        public bool TenziRoundClosed { get; set; }
        public long TenziDrawSeed { get; set; }
        public LobbyPlayerDifficultyEntry[] ReadyPlayerDifficulties { get; set; }
        public LobbyPlayerDifficultyEntry[] CurrentBattleDifficulties { get; set; }
        public PlayerSyncEntry[] PlayerDetails { get; set; }
        public LobbyPlayerCharacterEntry[] PlayerCharacters { get; set; }
    }

    public class LobbyPlaylistOwnerEntry
    {
        public string Entry { get; set; }
        public string Uid { get; set; }
    }

    public class LobbyPlayerCharacterEntry
    {
        public string Uid { get; set; }
        public int GirlIndex { get; set; }
        public int ElfinIndex { get; set; }
        public int FavGirlIndex { get; set; }
        public int FavElfinIndex { get; set; }
    }

    public class LobbyKickedPush
    {
        public string Reason { get; set; }
    }
}
