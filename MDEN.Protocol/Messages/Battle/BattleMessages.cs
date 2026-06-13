using MDEN.Protocol.Models;

namespace MDEN.Protocol.Messages.Battle
{
    /// <summary>
    /// 客户端上报自身对战数�?(Notify, 即发即忘)
    /// </summary>
    public class BattleDataNotifyMsg
    {
        public uint Score { get; set; }
        public float Accuracy { get; set; }
        public ushort Perfects { get; set; }
        public ushort Greats { get; set; }
        public ushort Earlies { get; set; }
        public ushort Lates { get; set; }
        public ushort Misses { get; set; }
        public bool FC { get; set; }
        public bool Alive { get; set; }
    }

    /// <summary>
    /// 服务端广播给房间内所有人的对战数�?(Push)
    /// </summary>
    public class BattleDataPushMsg
    {
        public BattlePlayerEntry[] Players { get; set; }
    }
}
