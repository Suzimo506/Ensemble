using MDEN.Managers;
using MDEN.Protocol.Messages.Chat;
using MDEN.UI.Displays;

namespace MDEN.UI.Core
{
    public static class RoomHudController
    {
        private const int MaxRefreshRetries = 120;
        private static readonly RoomPlayerListDisplay PlayerList = new RoomPlayerListDisplay();
        private static readonly RoomChatDisplay Chat = new RoomChatDisplay();
        private static readonly RoomReadyDisplay ReadyDisplay = new RoomReadyDisplay();
        private static bool _initialized;
        private static int _pendingRetryGeneration;
        private static int _retryDelayFrames;
        private static bool _characterNotReadyLogged;

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

        public static void RequestRefresh()
        {
            var generation = ++_pendingRetryGeneration;
            MainThreadDispatcher.Enqueue(() =>
            {
                if (generation != _pendingRetryGeneration) return;
                Refresh();
            });
        }

        public static void Update()
        {
            Chat.Update();
            ReadyDisplay.Update();
            RoomSceneOverlay.UpdateVisibility();
        }

        public static bool IsChatConsumingInput => Chat.IsConsumingInput;

        private static void Refresh(int retryCount)
        {
            if (!LobbyManager.IsInLobby)
            {
                Destroy();
                return;
            }

            if (RoomSceneOverlay.IsNavigationReady)
            {
                PlayerList.Update(LobbyManager.CurrentLobby);
                Chat.CreateEmpty();
                ReadyDisplay.Refresh(LobbyManager.CurrentLobby);
            }

            if (!RoomSceneOverlay.IsHomeReady)
            {
                RoomSceneOverlay.Hide();
                ScheduleRefreshRetry(retryCount);
                return;
            }

            var characterReady = RoomCharacterDisplay.Refresh(LobbyManager.CurrentLobby);
            if (characterReady)
            {
                _characterNotReadyLogged = false;
            }
            else
            {
                if (!_characterNotReadyLogged)
                {
                    MelonLoader.MelonLogger.Warning("Room character display is not ready.");
                    _characterNotReadyLogged = true;
                }
            }

            RoomSceneOverlay.Refresh(LobbyManager.CurrentLobby);

            if (NeedsRefreshRetry() && retryCount < MaxRefreshRetries)
            {
                ScheduleRefreshRetry(retryCount);
            }
        }

        private static void ScheduleRefreshRetry(int retryCount)
        {
            if (retryCount >= MaxRefreshRetries) return;

            var generation = ++_pendingRetryGeneration;
            _retryDelayFrames = 3;
            MainThreadDispatcher.Enqueue(() => RunDelayedRefresh(generation, retryCount));
        }

        private static void RunDelayedRefresh(int generation, int retryCount)
        {
            if (generation != _pendingRetryGeneration) return;

            if (_retryDelayFrames > 0)
            {
                _retryDelayFrames--;
                MainThreadDispatcher.Enqueue(() => RunDelayedRefresh(generation, retryCount));
                return;
            }

            Refresh(retryCount + 1);
        }

        private static bool NeedsRefreshRetry()
        {
            return !Chat.IsCreated || !RoomSceneOverlay.IsCreated || !RoomCharacterDisplay.IsCreated;
        }

        public static void Destroy()
        {
            _pendingRetryGeneration++;
            _retryDelayFrames = 0;
            _characterNotReadyLogged = false;
            PlayerList.Destroy();
            Chat.Destroy();
            ReadyDisplay.Destroy();
            RoomSceneOverlay.Destroy();
            RoomCharacterDisplay.Destroy();
            MultiplayerBattleController.Reset();
        }

        public static void ResetSceneObjects()
        {
            _pendingRetryGeneration++;
            _retryDelayFrames = 0;
            _characterNotReadyLogged = false;
            PlayerList.Destroy();
            Chat.ResetSceneObjects();
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
