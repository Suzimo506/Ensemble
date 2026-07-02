using System.Text.Json.Serialization;
using MDEN.Protocol.Messages.Player;

namespace MDEN.Protocol.Messages.Social
{
    public class FriendRequestReq
    {
        public string FriendUid { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public bool? Accept { get; set; }
    }

    public class FriendRequestResp
    {
        /// <summary>
        /// 0=无操作 1=已发送请求 2=已接受 3=已删除好友 4=已取消请求 5=已拒绝
        /// </summary>
        public int Action { get; set; }
    }

    public class GetFriendsResponse
    {
        public GetPlayerResponse[] Friends { get; set; }
    }

    public class FriendNotifyPush
    {
        /// <summary>
        /// friend_request / friend_accepted / friend_declined / friend_removed / friend_request_cancelled
        /// </summary>
        public string Action { get; set; }
        public string PlayerUid { get; set; }
        public string PlayerName { get; set; }
    }

    public class GetNodePlayersRequest
    {
    }

    public class GetNodePlayersResponse
    {
        public NodePlayerEntry[] Players { get; set; }
    }

    public class NodePlayerEntry
    {
        public string Uid { get; set; }
        public string Name { get; set; }
        public string ChatColor { get; set; }
        public byte Status { get; set; }
        public int LobbyId { get; set; }
        public string LobbyName { get; set; }
    }

    public class SendLobbyInviteRequest
    {
        public string TargetUid { get; set; }
    }

    public class SendLobbyInviteResponse
    {
        public bool Sent { get; set; }
        public string Reason { get; set; }
    }

    public class LobbyInvitePush
    {
        public string InviteId { get; set; }
        public int LobbyId { get; set; }
        public string LobbyName { get; set; }
        public string InviterUid { get; set; }
        public string InviterName { get; set; }
        public string InviterColor { get; set; }
    }

    public class RespondLobbyInviteRequest
    {
        public string InviteId { get; set; }
        public bool Accepted { get; set; }
    }

    public class RespondLobbyInviteResponse
    {
        public int LobbyId { get; set; }
    }

    public class PlayerPresenceNotify
    {
        public byte Status { get; set; }
    }
}
