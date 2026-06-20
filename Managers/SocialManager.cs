using System.Threading.Tasks;
using MDEN.Network;
using MDEN.Protocol;
using MDEN.Protocol.Messages.Social;
using MDEN.UI.Core;

namespace MDEN.Managers
{
    public static class SocialManager
    {
        public static void Init()
        {
            PushDispatcher.Instance.Register<FriendNotifyPush>(OpCodes.FriendNotifyPush, OnFriendNotify);
        }

        public static Task<FriendRequestResp> SendFriendRequestAsync(string friendUid)
        {
            ConnectionManager.EnsureCanSendRequest();
            return NetworkClient.Instance.SendRequestAsync<FriendRequestReq, FriendRequestResp>(
                OpCodes.FriendRequestReq,
                new FriendRequestReq { FriendUid = friendUid });
        }

        public static Task<FriendRequestResp> RespondFriendRequestAsync(string friendUid, bool accept)
        {
            ConnectionManager.EnsureCanSendRequest();
            return NetworkClient.Instance.SendRequestAsync<FriendRequestReq, FriendRequestResp>(
                OpCodes.FriendRequestReq,
                new FriendRequestReq { FriendUid = friendUid, Accept = accept });
        }

        private static void OnFriendNotify(FriendNotifyPush push)
        {
            if (push == null) return;

            MainThreadDispatcher.Enqueue(() => HandleFriendNotify(push));
        }

        private static void HandleFriendNotify(FriendNotifyPush push)
        {
            if (push.Action == "friend_request")
            {
                ShowFriendRequestDialog(push);
                return;
            }

            UiNotificationManager.RequestToast(GetNotifyMessage(push));
        }

        private static void ShowFriendRequestDialog(FriendNotifyPush push)
        {
            var name = GetPlayerName(push);
            UiNotificationManager.RequestConfirm(
                "好友请求",
                $"{name} 想添加你为好友",
                confirmed => _ = RespondFriendRequestSafelyAsync(push.PlayerUid, confirmed));
        }

        private static async Task RespondFriendRequestSafelyAsync(string friendUid, bool accept)
        {
            try
            {
                var response = await RespondFriendRequestAsync(friendUid, accept);
                var message = GetResponseMessage(response?.Action ?? 0);
                UiNotificationManager.RequestToast(message);
            }
            catch (System.Exception ex)
            {
                MDEN.Managers.ClientLogManager.Warning($"Respond friend request failed: {ex.Message}");
                UiNotificationManager.RequestToast(ex.Message);
            }
        }

        private static string GetNotifyMessage(FriendNotifyPush push)
        {
            var name = GetPlayerName(push);
            return push.Action switch
            {
                "friend_request" => $"{name} 向你发送了好友请求",
                "friend_accepted" => $"{name} 已成为你的好友",
                "friend_removed" => $"{name} 已删除好友关系",
                "friend_request_cancelled" => $"{name} 已取消好友请求",
                "friend_declined" => $"{name} 拒绝了你的好友请求",
                _ => "好友状态已更新"
            };
        }

        private static string GetResponseMessage(int action)
        {
            return action switch
            {
                2 => "已添加好友",
                5 => "已拒绝好友请求",
                _ => "好友状态已更新"
            };
        }

        private static string GetPlayerName(FriendNotifyPush push)
        {
            return string.IsNullOrWhiteSpace(push.PlayerName) ? push.PlayerUid : push.PlayerName;
        }
    }
}
