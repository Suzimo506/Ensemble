using System;
using System.Threading.Tasks;
using MDEN.Network;
using MDEN.Protocol;
using MDEN.Protocol.Messages.Chat;
using MDEN.Protocol.Messages.Mdt;
using MDEN.UI.Core;

namespace MDEN.Managers
{
    public static class ChatManager
    {
        public static event Action<ChatPushMsg> MessageReceived;

        public static void Init()
        {
            PushDispatcher.Instance.Register<ChatPushMsg>(OpCodes.ChatPush, OnChatPush);
        }

        public static async Task SendAsync(string message, byte target = ChatTargets.Default)
        {
            if (string.IsNullOrWhiteSpace(message)) return;
            ConnectionManager.EnsureCanSendRequest();
            var text = message.Trim();

            if (!LobbyManager.IsInLobby && (IsRoomWorldCommand(text) || target == ChatTargets.World || target == ChatTargets.Invite))
            {
                throw new InvalidOperationException(I18nManager.T("player.not_in_room"));
            }

            await NetworkClient.Instance.SendNotifyAsync(
                OpCodes.ChatNotify,
                new ChatNotifyMsg { Message = text, Target = target });
        }

        public static async Task SendMdtHostReplyAsync(string reply)
        {
            if (string.IsNullOrWhiteSpace(reply)) return;
            ConnectionManager.EnsureCanSendRequest();

            if (!LobbyManager.IsInLobby)
            {
                throw new InvalidOperationException(I18nManager.T("player.not_in_room"));
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
            MainThreadDispatcher.Enqueue(() => MessageReceived?.Invoke(message));
        }

        private static bool IsRoomWorldCommand(string message)
        {
            return StartsWithCommand(message, "/invite") || StartsWithCommand(message, "/world");
        }

        private static bool StartsWithCommand(string message, string command)
        {
            if (string.IsNullOrWhiteSpace(message)) return false;
            return message.Equals(command, StringComparison.OrdinalIgnoreCase) ||
                   message.StartsWith(command + " ", StringComparison.OrdinalIgnoreCase);
        }
    }
}
