using MDEN.Managers;
using MDEN.Protocol.Messages.Chat;
using MDEN.UI.Displays;

namespace MDEN.UI.Core
{
    public static class RoomHudController
    {
        private const int MaxRefreshRetries = 12;
        private static readonly RoomPlayerListDisplay PlayerList = new RoomPlayerListDisplay();
        private static readonly RoomChatDisplay Chat = new RoomChatDisplay();
        private static readonly RoomReadyDisplay ReadyDisplay = new RoomReadyDisplay();
        private static bool _initialized;
        private static int _pendingRetryGeneration;

        public static void Initialize()
        {
            if (_initialized) return;
            LobbyManager.CurrentLobbyChanged += HandleLobbyChanged;
            ChatManager.MessageReceived += HandleChatMessageReceived;
            _initialized = true;
        }

        public static void Deinitialize()
        {
            if (!_initialized) return;
            LobbyManager.CurrentLobbyChanged -= HandleLobbyChanged;
            ChatManager.MessageReceived -= HandleChatMessageReceived;
            Destroy();
            _initialized = false;
        }

        private static void HandleLobbyChanged(MDEN.Protocol.Messages.Lobby.LobbySyncPush _)
        {
            MainThreadDispatcher.Enqueue(OnLobbyChanged);
        }

        private static void HandleChatMessageReceived(ChatPushMsg message)
        {
            MainThreadDispatcher.Enqueue(() => AddChatMessage(message));
        }

        private static void OnLobbyChanged()
        {
            Refresh();
            NavigationButton.RefreshRoomButton();
            PreparationStartController.BindOrRefresh();
            MultiplayerBattleController.OnLobbyChanged();
        }

        public static void Refresh()
        {
            Refresh(0);
        }

        private static void Refresh(int retryCount)
        {
            if (!LobbyManager.IsInLobby)
            {
                Destroy();
                return;
            }

            PlayerList.Update(LobbyManager.CurrentLobby);
            Chat.CreateEmpty();
            ReadyDisplay.Refresh(LobbyManager.CurrentLobby);
            RoomSceneOverlay.Refresh(LobbyManager.CurrentLobby);
            RoomCharacterDisplay.Refresh(LobbyManager.CurrentLobby);

            if (!Chat.IsCreated && retryCount < MaxRefreshRetries)
            {
                var generation = ++_pendingRetryGeneration;
                MainThreadDispatcher.Enqueue(() =>
                {
                    if (generation != _pendingRetryGeneration) return;
                    Refresh(retryCount + 1);
                });
            }
        }

        public static void Destroy()
        {
            _pendingRetryGeneration++;
            PlayerList.Destroy();
            Chat.Destroy();
            ReadyDisplay.Destroy();
            RoomSceneOverlay.Destroy();
            RoomCharacterDisplay.Destroy();
            MultiplayerBattleController.Reset();
        }

        public static void AddChatMessage(ChatPushMsg message)
        {
            if (!LobbyManager.IsInLobby) return;
            Chat.AddMessage(message);
        }
    }
}
