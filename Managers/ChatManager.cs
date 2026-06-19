using System;
using System.Threading.Tasks;
using MDEN.Network;
using MDEN.Protocol;
using MDEN.Protocol.Messages.Chat;
using MDEN.Protocol.Messages.Mdt;

namespace MDEN.Managers
{
    public static class ChatManager
    {
        public static event Action<ChatPushMsg> MessageReceived;

        public static void Init()
        {
            PushDispatcher.Instance.Register<ChatPushMsg>(OpCodes.ChatPush, OnChatPush);
        }

        public static async Task SendAsync(string message)
        {
            if (string.IsNullOrWhiteSpace(message)) return;
            ConnectionManager.EnsureCanSendRequest();

            if (!LobbyManager.IsInLobby)
            {
                throw new InvalidOperationException("Not in lobby.");
            }

            await NetworkClient.Instance.SendNotifyAsync(
                OpCodes.ChatNotify,
                new ChatNotifyMsg { Message = message.Trim() });
        }

        public static async Task SendMdtHostReplyAsync(string reply)
        {
            if (string.IsNullOrWhiteSpace(reply)) return;
            ConnectionManager.EnsureCanSendRequest();

            if (!LobbyManager.IsInLobby)
            {
                throw new InvalidOperationException("Not in lobby.");
            }

            await NetworkClient.Instance.SendNotifyAsync(
                OpCodes.MdtHostReplyNotify,
                new MdtHostReplyNotify
                {
                    LobbyId = LobbyManager.CurrentLobby.Id,
                    Reply = reply.Trim()
                });
        }

        private static void OnChatPush(ChatPushMsg message)
        {
            MessageReceived?.Invoke(message);
        }
    }
}
