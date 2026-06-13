using MDEN.Protocol.Messages.Player;

namespace MDEN.Protocol.Messages.Social
{
    public class FriendRequestReq
    {
        public string FriendUid { get; set; }
    }

    public class FriendRequestResp
    {
        /// <summary>
        /// 0=无操�? 1=已发送请�? 2=已接�? 3=已删除好�? 4=已取消请�?        /// </summary>
        public int Action { get; set; }
    }

    public class GetFriendsResponse
    {
        public GetPlayerResponse[] Friends { get; set; }
    }

    public class FriendNotifyPush
    {
        /// <summary>
        /// friend_request / friend_accepted / friend_declined / friend_removed
        /// </summary>
        public string Action { get; set; }
        public string PlayerUid { get; set; }
        public string PlayerName { get; set; }
    }
}
