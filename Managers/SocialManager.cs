using System.Threading.Tasks;
using MDEN.Network;
using MDEN.Protocol;
using MDEN.Protocol.Messages.Social;

namespace MDEN.Managers
{
    public static class SocialManager
    {
        public static Task<FriendRequestResp> SendFriendRequestAsync(string friendUid)
        {
            return NetworkClient.Instance.SendRequestAsync<FriendRequestReq, FriendRequestResp>(
                OpCodes.FriendRequestReq,
                new FriendRequestReq { FriendUid = friendUid });
        }
    }
}
