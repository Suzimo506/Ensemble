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

    /// <summary>
    /// 服务端推送的房间完整状�?    /// </summary>
    public class LobbySyncPush
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string HostUid { get; set; }
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
    }

    public class LobbyKickedPush
    {
        public string Reason { get; set; }
    }
}
