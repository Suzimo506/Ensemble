using MDEN.Managers;
using MDEN.Protocol.Messages.Chat;
using MDEN.UI.Displays;
using PeroInputManager = Il2CppAssets.Scripts.PeroTools.Managers.InputManager;

namespace MDEN.UI.Core
{
    public static class RoomHudController
    {
        private const int MaxRefreshRetries = 120;
        private const int EntranceFallbackDelayFrames = 20;
        private static readonly RoomPlayerListDisplay PlayerList = new RoomPlayerListDisplay();
        private static readonly RoomChatDisplay Chat = new RoomChatDisplay();
        private static readonly RoomReadyDisplay ReadyDisplay = new RoomReadyDisplay();
        private static bool _initialized;
        private static int _pendingRetryGeneration;
        private static int _retryDelayFrames;
        private static int _entranceFallbackLobbyId = -1;
        private static int _entranceDisplayedLobbyId = -1;
        private static bool _entranceFallbackCompleted;
        private static bool _characterNotReadyLogged;
        private static bool _nativeInputBlockedByChat;

        public static void Initialize()
        {
            if (_initialized) return;
            LobbyManager.CurrentLobbyChanged += HandleLobbyChanged;
            ChatManager.MessageReceived += HandleChatMessageReceived;
            PlayerManager.ProfileChanged += HandleProfileChanged;
            _initialized = true;
        }

        public static void Deinitialize()
        {
            if (!_initialized) return;
            LobbyManager.CurrentLobbyChanged -= HandleLobbyChanged;
            ChatManager.MessageReceived -= HandleChatMessageReceived;
            PlayerManager.ProfileChanged -= HandleProfileChanged;
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

        private static void HandleProfileChanged()
        {
            MainThreadDispatcher.Enqueue(OnProfileChanged);
        }

        private static void OnLobbyChanged()
        {
            Refresh();
            ScheduleEntranceFallback(LobbyManager.CurrentLobby);
            NavigationButton.RefreshRoomButton();
            PreparationStartController.BindOrRefresh();
            ChartPreviewController.OnLobbyChanged(LobbyManager.CurrentLobby);
            MultiplayerBattleController.OnLobbyChanged();
        }

        private static void OnProfileChanged()
        {
            Chat.InvalidatePlayerColors();
            RoomSceneOverlay.InvalidatePlayerColors();
            BattleLobbyDisplay.InvalidatePlayerColors();
            RequestRefresh();
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
            UpdateNativeInputBlock();
            ReadyDisplay.Update();
            StageDesignerTextController.Update();
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
                RoomCharacterDisplay.DestroyGeneratedObjects();
                ScheduleRefreshRetry(retryCount);
                return;
            }

            if (!RoomSceneOverlay.IsHomeVisible)
            {
                RoomSceneOverlay.Hide();
                RoomCharacterDisplay.DestroyGeneratedObjects();
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
            ResetEntranceFallback();
            _characterNotReadyLogged = false;
            PlayerList.Destroy();
            Chat.Destroy();
            ReadyDisplay.Destroy();
            StageDesignerTextController.Restore();
            RoomSceneOverlay.Destroy();
            RoomCharacterDisplay.Destroy();
            ChartPreviewController.Reset();
            MultiplayerBattleController.Reset();
            RestoreNativeInput();
        }

        public static void ResetSceneObjects()
        {
            _pendingRetryGeneration++;
            _retryDelayFrames = 0;
            ResetEntranceFallback();
            _characterNotReadyLogged = false;
            PlayerList.Destroy();
            Chat.ResetSceneObjects();
            ReadyDisplay.Destroy();
            StageDesignerTextController.Restore();
            RoomSceneOverlay.Destroy();
            RoomCharacterDisplay.Destroy();
            ChartPreviewController.Reset();
            MultiplayerBattleController.Reset();
            RestoreNativeInput();
        }

        public static void AddChatMessage(ChatPushMsg message)
        {
            if (!LobbyManager.IsInLobby) return;
            if (IsOwnEntranceMessage(message))
            {
                var lobbyId = LobbyManager.CurrentLobby.Id;
                if (_entranceDisplayedLobbyId == lobbyId) return;

                _entranceDisplayedLobbyId = lobbyId;
                _entranceFallbackCompleted = true;
            }

            Chat.AddMessage(message);
        }

        private static void ScheduleEntranceFallback(MDEN.Protocol.Messages.Lobby.LobbySyncPush lobby)
        {
            if (lobby == null)
            {
                ResetEntranceFallback();
                return;
            }

            if (_entranceFallbackLobbyId == lobby.Id) return;

            _entranceFallbackLobbyId = lobby.Id;
            _entranceDisplayedLobbyId = -1;
            _entranceFallbackCompleted = false;
            MainThreadDispatcher.Enqueue(() => RunEntranceFallback(lobby.Id, EntranceFallbackDelayFrames));
        }

        private static void RunEntranceFallback(int lobbyId, int framesRemaining)
        {
            if (!LobbyManager.IsInLobby || LobbyManager.CurrentLobby?.Id != lobbyId) return;
            if (_entranceFallbackCompleted || _entranceDisplayedLobbyId == lobbyId) return;

            if (framesRemaining > 0)
            {
                MainThreadDispatcher.Enqueue(() => RunEntranceFallback(lobbyId, framesRemaining - 1));
                return;
            }

            var entranceMessage = PlayerManager.CurrentProfile?.EntranceMessage?.Trim();
            if (string.IsNullOrWhiteSpace(entranceMessage))
            {
                _entranceFallbackCompleted = true;
                return;
            }

            AddChatMessage(new ChatPushMsg
            {
                AuthorUid = "system",
                AuthorName = "System",
                Message = "PlayerEntranceMessage",
                ExtraData = $"{PlayerManager.CurrentUid}#{entranceMessage}",
                IsSystem = true
            });
        }

        private static bool IsOwnEntranceMessage(ChatPushMsg message)
        {
            if (message == null || message.Message != "PlayerEntranceMessage") return false;
            if (string.IsNullOrWhiteSpace(message.ExtraData)) return false;

            var parts = message.ExtraData.Split(new[] { '#' }, 2);
            return parts.Length == 2 && parts[0] == PlayerManager.CurrentUid;
        }

        private static void ResetEntranceFallback()
        {
            _entranceFallbackLobbyId = -1;
            _entranceDisplayedLobbyId = -1;
            _entranceFallbackCompleted = false;
        }

        private static void UpdateNativeInputBlock()
        {
            var shouldBlock = LobbyManager.IsInLobby && Chat.IsConsumingInput;
            if (_nativeInputBlockedByChat == shouldBlock) return;

            _nativeInputBlockedByChat = shouldBlock;
            SetNativeInputBlocked(shouldBlock);
        }

        private static void RestoreNativeInput()
        {
            if (!_nativeInputBlockedByChat) return;

            _nativeInputBlockedByChat = false;
            SetNativeInputBlocked(false);
        }

        private static void SetNativeInputBlocked(bool blocked)
        {
            try
            {
                if (PeroInputManager.instance != null)
                {
                    PeroInputManager.instance.isStopKeyAction = blocked;
                }
            }
            catch
            {
                // Native input manager may be unavailable during scene transitions.
            }
        }
    }
}
