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
                I18nManager.T("social.request.title"),
                I18nManager.Tf("social.request.confirm", name),
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
                "friend_request" => I18nManager.Tf("social.notify.request", name),
                "friend_accepted" => I18nManager.Tf("social.notify.accepted", name),
                "friend_removed" => I18nManager.Tf("social.notify.removed", name),
                "friend_request_cancelled" => I18nManager.Tf("social.notify.cancelled", name),
                "friend_declined" => I18nManager.Tf("social.notify.declined", name),
                _ => I18nManager.T("friend.updated")
            };
        }

        private static string GetResponseMessage(int action)
        {
            return action switch
            {
                2 => I18nManager.T("friend.added"),
                5 => I18nManager.T("friend.request_declined"),
                _ => I18nManager.T("friend.updated")
            };
        }

        private static string GetPlayerName(FriendNotifyPush push)
        {
            return string.IsNullOrWhiteSpace(push.PlayerName) ? push.PlayerUid : push.PlayerName;
        }
    }
}
