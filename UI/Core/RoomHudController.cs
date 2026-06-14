using MDEN.Managers;
using MDEN.Protocol.Messages.Chat;
using MDEN.UI.Displays;

namespace MDEN.UI.Core
{
    public static class RoomHudController
    {
        private static readonly RoomPlayerListDisplay PlayerList = new RoomPlayerListDisplay();
        private static readonly RoomChatDisplay Chat = new RoomChatDisplay();
        private static bool _initialized;

        public static void Initialize()
        {
            if (_initialized) return;
            LobbyManager.CurrentLobbyChanged += _ => MainThreadDispatcher.Enqueue(OnLobbyChanged);
            ChatManager.MessageReceived += AddChatMessage;
            _initialized = true;
        }

        private static void OnLobbyChanged()
        {
            Refresh();
            NavigationButton.RefreshRoomButton();
        }

        public static void Refresh()
        {
            if (!LobbyManager.IsInLobby)
            {
                Destroy();
                return;
            }

            PlayerList.Update(LobbyManager.CurrentLobby);
            Chat.CreateEmpty();
            RoomSceneOverlay.Refresh(LobbyManager.CurrentLobby);
            RoomCharacterDisplay.Refresh(LobbyManager.CurrentLobby);
        }

        public static void Destroy()
        {
            PlayerList.Destroy();
            Chat.Destroy();
            RoomSceneOverlay.Destroy();
            RoomCharacterDisplay.Destroy();
        }

        public static void AddChatMessage(ChatPushMsg message)
        {
            if (!LobbyManager.IsInLobby) return;
            Chat.AddMessage(message);
        }
    }
}
