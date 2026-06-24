using System.Text.Json.Serialization;
using MDEN.Protocol.Messages.Lobby;
using MDEN.Protocol.Models;

namespace MDEN.Protocol.Messages.Mdt
{
    public class MdtObserveRequest
    {
        public string Uid { get; set; }
        public string ClientName { get; set; }
        public long TimestampUnixMs { get; set; }
        public string Nonce { get; set; }
        public string Signature { get; set; }
    }

    public class MdtObserveResponse
    {
        public string NodeId { get; set; }
        public string Version { get; set; }
    }

    public class MdtGetLobbySnapshotRequest
    {
    }

    public class MdtGetLobbySnapshotResponse
    {
        public MdtLobbySnapshot Snapshot { get; set; }
    }

    public class MdtLobbySnapshotPush
    {
        public MdtLobbySnapshot Snapshot { get; set; }
    }

    public class MdtLobbySnapshot
    {
        public string NodeId { get; set; }
        public string Version { get; set; }
        public long TimestampUnixMs { get; set; }
        public MdtLobbyEntry[] Lobbies { get; set; }
    }

    public class MdtLobbyEntry
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
        public int PlaylistCount { get; set; }
        public bool SettlementEnabled { get; set; }
        public bool IsPrivate { get; set; }
        public bool JoinLocked { get; set; }
        public bool Locked { get; set; }
        public bool IsPlaying { get; set; }
        public int WatcherCount { get; set; }
        public ushort CurrentPlaylistEntry { get; set; }
        public string CurrentBattleId { get; set; }
        public string CurrentBattleEntry { get; set; }
        public string[] Playlist { get; set; }
        public LobbyPlaylistOwnerEntry[] PlaylistOwners { get; set; }
        public string TenziSelectedEntry { get; set; }
        public bool TenziRoundClosed { get; set; }
        public long TenziDrawSeed { get; set; }
        public MdtLobbyPlayerEntry[] Players { get; set; }
        public MdtChatMessageEntry[] Chats { get; set; }
        public MdtViewerChatMessageEntry[] ViewerChats { get; set; }
    }

    public class MdtLobbyPlayerEntry
    {
        public string Uid { get; set; }
        public string Name { get; set; }
        public string ChatColor { get; set; }
        public ushort PingMS { get; set; }
        public byte Status { get; set; }
        public bool Ready { get; set; }
        public int Difficulty { get; set; }
        public bool Muted { get; set; }
        public bool ChartSelectBanned { get; set; }
        public int GirlIndex { get; set; }
        public int ElfinIndex { get; set; }
        public int FavGirlIndex { get; set; }
        public int FavElfinIndex { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public BattlePlayerEntry Battle { get; set; }
    }

    public class MdtChatRequest
    {
        public int LobbyId { get; set; }
        public string SenderName { get; set; }
        public int PhraseIndex { get; set; }
    }

    public class MdtChatResponse
    {
        public long NextAllowedUnixMs { get; set; }
    }

    public class MdtWatchLobbyRequest
    {
        public int? LobbyId { get; set; }
    }

    public class MdtWatchLobbyResponse
    {
    }

    public class MdtViewerChatRequest
    {
        public int LobbyId { get; set; }
        public string SenderName { get; set; }
        public string Message { get; set; }
    }

    public class MdtViewerChatResponse
    {
        public long NextAllowedUnixMs { get; set; }
    }

    public class MdtHostReplyNotify
    {
        public int LobbyId { get; set; }
        public string Reply { get; set; }
    }

    public class MdtChatPush
    {
        public int LobbyId { get; set; }
        public MdtChatMessageEntry Message { get; set; }
    }

    public class MdtViewerChatPush
    {
        public int LobbyId { get; set; }
        public MdtViewerChatMessageEntry Message { get; set; }
    }

    public class MdtChatMessageEntry
    {
        public long TimestampUnixMs { get; set; }
        public string Source { get; set; }
        public string SenderName { get; set; }
        public string Message { get; set; }
        public string Color { get; set; }
        public bool IsHostReply { get; set; }
    }

    public class MdtViewerChatMessageEntry
    {
        public long TimestampUnixMs { get; set; }
        public string SenderName { get; set; }
        public string Message { get; set; }
        public string Color { get; set; }
    }
}
