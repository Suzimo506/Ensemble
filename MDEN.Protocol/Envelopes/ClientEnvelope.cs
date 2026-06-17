using System.Text.Json.Serialization;

namespace MDEN.Protocol.Envelopes
{
    /// <summary>
    /// 客户端发往服务端的消息信封
    /// </summary>
    public class ClientEnvelope
    {
        /// <summary>操作码，标识消息类型</summary>
        public ushort Op { get; set; }

        /// <summary>请求ID，仅 Request 类型有值，Notify 时为 null</summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public uint? ReqId { get; set; }

        /// <summary>具体消息体，结构由 OpCode 决定</summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public object Payload { get; set; }
    }
}
