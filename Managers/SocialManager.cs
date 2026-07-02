using System.Threading.Tasks;
using MDEN.Network;
using MDEN.Protocol;
using MDEN.Protocol.Enums;
using MDEN.Protocol.Messages.Social;
using MDEN.UI.Core;

namespace MDEN.Managers
{
    public static class SocialManager
    {
        public static void Init()
        {
            PushDispatcher.Instance.Register<FriendNotifyPush>(OpCodes.FriendNotifyPush, OnFriendNotify);
            PushDispatcher.Instance.Register<LobbyInvitePush>(OpCodes.LobbyInvitePush, OnLobbyInvite);
        }

        public static Task<NodePlayerEntry[]> GetNodePlayersAsync()
        {
            ConnectionManager.EnsureCanSendRequest();
            return GetNodePlayersCoreAsync();
        }

        private static async Task<NodePlayerEntry[]> GetNodePlayersCoreAsync()
        {
            var response = await NetworkClient.Instance.SendRequestAsync<GetNodePlayersRequest, GetNodePlayersResponse>(
                OpCodes.GetNodePlayersReq,
                new GetNodePlayersRequest());
            return response?.Players ?? new NodePlayerEntry[0];
        }

        public static Task<SendLobbyInviteResponse> SendLobbyInviteAsync(string targetUid)
        {
            ConnectionManager.EnsureCanSendRequest();
            return NetworkClient.Instance.SendRequestAsync<SendLobbyInviteRequest, SendLobbyInviteResponse>(
                OpCodes.SendLobbyInviteReq,
                new SendLobbyInviteRequest { TargetUid = targetUid });
        }

        public static void SyncPresence(PlayerStatus status)
        {
            if (!ConnectionManager.CanSendRequests || string.IsNullOrWhiteSpace(PlayerManager.CurrentUid)) return;

            _ = NetworkClient.Instance.TrySendNotifyAsync(
                OpCodes.PlayerPresenceNotify,
                new PlayerPresenceNotify { Status = (byte)status });
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

        private static void OnLobbyInvite(LobbyInvitePush push)
        {
            if (push == null) return;

            MainThreadDispatcher.Enqueue(() => HandleLobbyInvite(push));
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

        private static void HandleLobbyInvite(LobbyInvitePush push)
        {
            if (!CanShowLobbyInviteDialog()) return;

            var inviter = ColorText(EscapeRichText(GetInviterName(push)), NormalizeColor(push.InviterColor));
            var lobbyName = ColorText(EscapeRichText(push.LobbyName), Constants.ColorYellow);
            NativeConfirmDialog.Show(
                I18nManager.T("node.invite.title"),
                I18nManager.Tf("node.invite.confirm", inviter, lobbyName),
                confirmed => _ = RespondLobbyInviteSafelyAsync(push, confirmed));
        }

        private static bool CanShowLobbyInviteDialog()
        {
            return ConnectionManager.CanSendRequests && !LobbyManager.IsInLobby && !PlayerManager.IsSinglePlaying;
        }

        private static async Task RespondLobbyInviteSafelyAsync(LobbyInvitePush push, bool accepted)
        {
            try
            {
                var response = await NetworkClient.Instance.SendRequestAsync<RespondLobbyInviteRequest, RespondLobbyInviteResponse>(
                    OpCodes.RespondLobbyInviteReq,
                    new RespondLobbyInviteRequest { InviteId = push.InviteId, Accepted = accepted });

                if (!accepted) return;

                await LobbyManager.RefreshCurrentLobbyAsync();
                MainThreadDispatcher.Enqueue(() =>
                {
                    if (LobbyManager.IsInLobby)
                    {
                        WindowStackController.OpenWindow(new MDEN.UI.Windows.MyRoomWindow());
                    }
                });
            }
            catch (System.Exception ex)
            {
                MDEN.Managers.ClientLogManager.Warning($"Respond lobby invite failed: {ex.Message}");
                UiNotificationManager.RequestToast(ex.Message);
            }
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

        private static string GetInviterName(LobbyInvitePush push)
        {
            return string.IsNullOrWhiteSpace(push.InviterName) ? push.InviterUid : push.InviterName;
        }

        private static string NormalizeColor(string color)
        {
            if (string.IsNullOrWhiteSpace(color)) return "ffffffff";
            var value = color.Trim().TrimStart('#');
            return System.Text.RegularExpressions.Regex.IsMatch(value, "^[0-9a-fA-F]{6}([0-9a-fA-F]{2})?$")
                ? value
                : "ffffffff";
        }

        private static string ColorText(string value, string color)
        {
            return $"<color=#{color}>{value}</color>";
        }

        private static string EscapeRichText(string value)
        {
            return string.IsNullOrEmpty(value)
                ? string.Empty
                : value.Replace("<", "＜").Replace(">", "＞");
        }
    }
}
