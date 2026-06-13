using System.Text.Json.Serialization;

namespace MDEN.Protocol.Messages.Chat
{
    /// <summary>
    /// 客户端发送聊天消�?(Notify, 即发即忘)
    /// 服务端通过会话自动补充发送者信�?    /// </summary>
    public class ChatNotifyMsg
    {
        public string Message { get; set; }
    }

    /// <summary>
    /// 服务端广播聊天消�?(Push)
    /// </summary>
    public class ChatPushMsg
    {
        public string AuthorUid { get; set; }
        public string AuthorName { get; set; }
        public string Message { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string ExtraData { get; set; }

        public bool IsSystem { get; set; }
    }
}
