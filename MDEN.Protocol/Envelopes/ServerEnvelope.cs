using System.Text.Json.Serialization;

namespace MDEN.Protocol.Envelopes
{
    /// <summary>
    /// 服务端发往客户端的消息信封
    /// </summary>
    public class ServerEnvelope
    {
        /// <summary>操作码</summary>
        public ushort Op { get; set; }

        /// <summary>对应请求的ID，仅 Response 有值，Push 时为 null</summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public uint? ReqId { get; set; }

        /// <summary>仅 Response 有意义，标识请求是否成功</summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public bool Success { get; set; }

        /// <summary>具体消息体 (反序列化时为 JsonElement)</summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public object Payload { get; set; }

        /// <summary>
        /// 创建一个 Response 信封
        /// </summary>
        public static ServerEnvelope Response(ushort opCode, uint reqId, bool success, object payload = null)
        {
            return new ServerEnvelope
            {
                Op = opCode,
                ReqId = reqId,
                Success = success,
                Payload = payload
            };
        }

        /// <summary>
        /// 创建一个 Push 信封
        /// </summary>
        public static ServerEnvelope Push(ushort opCode, object payload = null)
        {
            return new ServerEnvelope
            {
                Op = opCode,
                Success = true,
                Payload = payload
            };
        }
    }
}
