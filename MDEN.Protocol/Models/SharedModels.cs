namespace MDEN.Protocol.Models
{
    /// <summary>
    /// 房间同步推送中的简要玩家信息
    /// </summary>
    public class PlayerSyncEntry
    {
        public string Uid { get; set; }
        public string Name { get; set; }
        public string Bio { get; set; }
        public string Title { get; set; }
        public string ChatColor { get; set; }
        public string AvatarName { get; set; }
        public string AvatarData { get; set; }
        public ushort PingMS { get; set; }
        public byte Status { get; set; }
        public int TotalMultiplayerGames { get; set; }
    }

    /// <summary>
    /// 对战广播中的单个玩家数据
    /// </summary>
    public class BattlePlayerEntry
    {
        public string Uid { get; set; }
        public int Difficulty { get; set; }
        public uint Score { get; set; }
        public float Accuracy { get; set; }
        public ushort Perfects { get; set; }
        public ushort Greats { get; set; }
        public ushort Earlies { get; set; }
        public ushort Lates { get; set; }
        public ushort Misses { get; set; }
        public bool FC { get; set; }
        public bool Alive { get; set; }
        public ushort PingMS { get; set; }
    }

    /// <summary>
    /// 房间列表中的简要房间信息
    /// </summary>
    public class LobbyListEntry
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string HostUid { get; set; }
        public string HostName { get; set; }
        public byte PlayMode { get; set; }
        public byte PlayType { get; set; }
        public byte ChartSelection { get; set; }
        public byte Goal { get; set; }
        public ushort MaxPlayers { get; set; }
        public ushort PlaylistSize { get; set; }
        public byte TenziSongsPerPlayer { get; set; }
        public int PlaylistCount { get; set; }
        public bool SettlementEnabled { get; set; }
        public int PlayerCount { get; set; }
        public bool IsPrivate { get; set; }
        public bool JoinLocked { get; set; }
        public bool IsPlaying { get; set; }
        public bool Locked { get; set; }
    }

    /// <summary>
    /// 系统消息 (Disconnect 推送用)
    /// </summary>
    public class DisconnectMessage
    {
        public string Reason { get; set; }
    }

    public class LobbyPlayerDifficultyEntry
    {
        public string Uid { get; set; }
        public int Difficulty { get; set; }
    }
}
