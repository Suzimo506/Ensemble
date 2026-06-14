using System.Text.Json.Serialization;
using MDEN.Protocol.Models;

namespace MDEN.Protocol.Messages.Lobby
{
    public class CreateLobbyRequest
    {
        public string Name { get; set; }
        public ushort MaxPlayers { get; set; }
        public byte PlayType { get; set; }
        public byte ChartSelection { get; set; }
        public byte Goal { get; set; }
        public ushort PlaylistSize { get; set; }

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
        public string Name { get; set; }
        public string HostUid { get; set; }
        public string HostName { get; set; }
        public byte PlayType { get; set; }
        public byte ChartSelection { get; set; }
        public byte Goal { get; set; }
        public ushort MaxPlayers { get; set; }
        public ushort PlaylistSize { get; set; }
        public bool IsPrivate { get; set; }
        public bool Locked { get; set; }
        public bool IsPlaying { get; set; }
        public ushort CurrentPlaylistEntry { get; set; }
        public string[] Players { get; set; }
        public string[] ReadyPlayers { get; set; }
        public string[] Playlist { get; set; }
        public PlayerSyncEntry[] PlayerDetails { get; set; }
        public LobbyPlayerCharacterEntry[] PlayerCharacters { get; set; }
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
