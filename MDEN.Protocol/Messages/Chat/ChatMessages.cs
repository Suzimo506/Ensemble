using System.Text.Json.Serialization;

namespace MDEN.Protocol.Messages.Chat
{
    /// <summary>
    /// 客户端发送聊天消息 (Notify, 即发即忘)
    /// 服务端通过会话自动补充发送者信息
    /// </summary>
    public class ChatNotifyMsg
    {
        public string Message { get; set; }

        public byte Target { get; set; }
    }

    /// <summary>
    /// 服务端广播聊天消息 (Push)
    /// </summary>
    public class ChatPushMsg
    {
        public string AuthorUid { get; set; }
        public string AuthorName { get; set; }
        public string Message { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string ExtraData { get; set; }

        public bool IsSystem { get; set; }

        public byte Channel { get; set; }
    }

    public static class ChatTargets
    {
        public const byte Default = 0;
        public const byte Lobby = 1;
        public const byte World = 2;
        public const byte Invite = 3;
        public const byte ApBroadcast = 4;
    }
}
