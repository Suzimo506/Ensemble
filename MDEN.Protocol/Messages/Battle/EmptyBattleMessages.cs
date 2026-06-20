using MDEN.Protocol.Models;

namespace MDEN.Protocol.Messages.Battle
{
    public class BattleReturnedReq
    {
        public string BattleId { get; set; }
        public float PlayedSeconds { get; set; }
        public BattlePlayerEntry FinalPlayer { get; set; }
    }

    public class BattleReturnedResp {}
}
