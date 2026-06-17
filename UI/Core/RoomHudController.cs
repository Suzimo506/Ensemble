using System.Collections.Generic;
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
        private const int CharacterRepairCooldownFrames = 30;
        private static readonly RoomPlayerListDisplay PlayerList = new RoomPlayerListDisplay();
        private static readonly RoomChatDisplay Chat = new RoomChatDisplay();
        private static readonly RoomReadyDisplay ReadyDisplay = new RoomReadyDisplay();
        private static bool _initialized;
        private static int _pendingRetryGeneration;
        private static int _retryDelayFrames;
        private static int _entranceFallbackLobbyId = -1;
        private static int _entranceDisplayedLobbyId = -1;
        private static int _entranceTrackedLobbyId = -1;
        private static bool _entranceFallbackCompleted;
        private static readonly HashSet<int> EntranceAnnouncedLobbyIds = new HashSet<int>();
        private static bool _characterNotReadyLogged;
        private static int _characterRepairCooldownFrames;
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

        private static void HandleLobbyChanged(MDEN.Protocol.Messages.Lobby.LobbySyncPush lobby)
        {
            MainThreadDispatcher.Enqueue(() => OnLobbyChanged(lobby));
        }

        private static void HandleChatMessageReceived(ChatPushMsg message)
        {
            MainThreadDispatcher.Enqueue(() => AddChatMessage(message));
        }

        private static void HandleProfileChanged()
        {
            MainThreadDispatcher.Enqueue(OnProfileChanged);
        }

        private static void OnLobbyChanged(MDEN.Protocol.Messages.Lobby.LobbySyncPush lobby)
        {
            if (lobby == null && LobbyManager.CurrentLobby != null) return;
            if (lobby != null && LobbyManager.CurrentLobby?.Id != lobby.Id) return;

            var isNewLobbyEntry = lobby != null && _entranceTrackedLobbyId != lobby.Id;
            if (lobby == null)
            {
                _entranceTrackedLobbyId = -1;
            }
            else if (isNewLobbyEntry)
            {
                _entranceTrackedLobbyId = lobby.Id;
            }

            Refresh(lobby);
            ScheduleEntranceFallback(lobby, isNewLobbyEntry);
            NavigationButton.RefreshRoomButton();
            PreparationStartController.BindOrRefresh();
            ChartPreviewController.OnLobbyChanged(lobby);
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
            Refresh(LobbyManager.CurrentLobby, 0);
        }

        public static void RequestRefresh()
        {
            var generation = ++_pendingRetryGeneration;
            MainThreadDispatcher.Enqueue(() =>
            {
                if (generation != _pendingRetryGeneration) return;
                Refresh(LobbyManager.CurrentLobby);
            });
        }

        public static void RebuildRoomCharacters()
        {
            RoomCharacterDisplay.DestroyGeneratedObjects();
            _characterRepairCooldownFrames = 0;
            RequestRefresh();
        }

        public static void Update()
        {
            Chat.Update();
            UpdateNativeInputBlock();
            ReadyDisplay.Update();
            StageDesignerTextController.Update();
            RoomSceneOverlay.UpdateVisibility();
            RoomCharacterDisplay.UpdateLabelPositions(LobbyManager.CurrentLobby);
            RepairRoomCharactersIfNeeded();
        }

        public static bool IsChatConsumingInput => Chat.IsConsumingInput;

        private static void Refresh(MDEN.Protocol.Messages.Lobby.LobbySyncPush lobby, int retryCount = 0)
        {
            if (lobby == null || !LobbyManager.IsInLobby)
            {
                Destroy();
                return;
            }

            if (LobbyManager.CurrentLobby?.Id != lobby.Id) return;

            if (RoomSceneOverlay.IsNavigationReady)
            {
                PlayerList.Update(lobby);
                Chat.CreateEmpty();
                ReadyDisplay.Refresh(lobby);
            }

            if (!RoomSceneOverlay.IsHomeReady)
            {
                RoomSceneOverlay.Hide();
                RoomCharacterDisplay.DestroyGeneratedObjects();
                ScheduleRefreshRetry(lobby, retryCount);
                return;
            }

            if (!RoomSceneOverlay.IsHomeVisible)
            {
                RoomSceneOverlay.Hide();
                RoomCharacterDisplay.DestroyGeneratedObjects();
                ScheduleRefreshRetry(lobby, retryCount);
                return;
            }

            var characterReady = RoomCharacterDisplay.Refresh(lobby);
            if (characterReady)
            {
                _characterNotReadyLogged = false;
                _characterRepairCooldownFrames = 0;
            }
            else
            {
                if (!_characterNotReadyLogged)
                {
                    MelonLoader.MelonLogger.Warning("Room character display is not ready.");
                    _characterNotReadyLogged = true;
                }
            }

            RoomSceneOverlay.Refresh(lobby);

            if ((NeedsRefreshRetry() || !characterReady) && retryCount < MaxRefreshRetries)
            {
                ScheduleRefreshRetry(lobby, retryCount);
            }
        }

        private static void ScheduleRefreshRetry(MDEN.Protocol.Messages.Lobby.LobbySyncPush lobby, int retryCount)
        {
            if (retryCount >= MaxRefreshRetries) return;

            var generation = ++_pendingRetryGeneration;
            _retryDelayFrames = 3;
            MainThreadDispatcher.Enqueue(() => RunDelayedRefresh(lobby, generation, retryCount));
        }

        private static void RunDelayedRefresh(MDEN.Protocol.Messages.Lobby.LobbySyncPush lobby, int generation, int retryCount)
        {
            if (generation != _pendingRetryGeneration) return;
            if (lobby == null || !LobbyManager.IsInLobby || LobbyManager.CurrentLobby?.Id != lobby.Id) return;

            if (_retryDelayFrames > 0)
            {
                _retryDelayFrames--;
                MainThreadDispatcher.Enqueue(() => RunDelayedRefresh(lobby, generation, retryCount));
                return;
            }

            Refresh(lobby, retryCount + 1);
        }

        private static bool NeedsRefreshRetry()
        {
            return !Chat.IsCreated || !RoomSceneOverlay.IsCreated || !RoomCharacterDisplay.IsCreated;
        }

        private static void RepairRoomCharactersIfNeeded()
        {
            var lobby = LobbyManager.CurrentLobby;
            if (_characterRepairCooldownFrames > 0)
            {
                _characterRepairCooldownFrames--;
                return;
            }

            if (lobby == null || !LobbyManager.IsInLobby || !RoomSceneOverlay.IsHomeVisible) return;
            if (RoomCharacterDisplay.HasExpectedCharacterContent(lobby)) return;

            _characterRepairCooldownFrames = CharacterRepairCooldownFrames;
            RequestRefresh();
        }

        public static void Destroy()
        {
            _pendingRetryGeneration++;
            _retryDelayFrames = 0;
            ResetEntranceFallback();
            _characterNotReadyLogged = false;
            _characterRepairCooldownFrames = 0;
            PlayerList.Destroy();
            Chat.Destroy();
            ReadyDisplay.Destroy();
            StageDesignerTextController.Restore();
            RoomSceneOverlay.Destroy();
            RoomCharacterDisplay.Destroy();
            ChartPreviewController.ResetAll();
            MultiplayerBattleController.Reset();
            RestoreNativeInput();
        }

        public static void ResetSceneObjects()
        {
            _pendingRetryGeneration++;
            _retryDelayFrames = 0;
            ResetEntranceFallback(false);
            _characterNotReadyLogged = false;
            _characterRepairCooldownFrames = 0;
            PlayerList.Destroy();
            Chat.ResetSceneObjects();
            ReadyDisplay.Destroy();
            StageDesignerTextController.Restore();
            RoomSceneOverlay.Destroy();
            RoomCharacterDisplay.Destroy();
            ChartPreviewController.ResetAll();
            MultiplayerBattleController.Reset();
            RestoreNativeInput();
        }

        public static void AddChatMessage(ChatPushMsg message)
        {
            if (!LobbyManager.IsInLobby) return;
            if (IsOwnEntranceMessage(message))
            {
                var lobbyId = LobbyManager.CurrentLobby.Id;
                if (EntranceAnnouncedLobbyIds.Contains(lobbyId)) return;

                _entranceDisplayedLobbyId = lobbyId;
                _entranceFallbackCompleted = true;
                _entranceTrackedLobbyId = lobbyId;
                EntranceAnnouncedLobbyIds.Add(lobbyId);
            }

            Chat.AddMessage(message);
        }

        private static void ScheduleEntranceFallback(MDEN.Protocol.Messages.Lobby.LobbySyncPush lobby, bool isNewLobbyEntry)
        {
            if (lobby == null)
            {
                ResetEntranceFallback();
                return;
            }

            if (!isNewLobbyEntry) return;
            if (_entranceFallbackLobbyId == lobby.Id) return;
            if (EntranceAnnouncedLobbyIds.Contains(lobby.Id))
            {
                _entranceFallbackLobbyId = lobby.Id;
                _entranceDisplayedLobbyId = lobby.Id;
                _entranceFallbackCompleted = true;
                return;
            }

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

        private static void ResetEntranceFallback(bool clearAnnounced = true)
        {
            _entranceFallbackLobbyId = -1;
            _entranceDisplayedLobbyId = -1;
            _entranceFallbackCompleted = false;
            if (clearAnnounced)
            {
                _entranceTrackedLobbyId = -1;
                EntranceAnnouncedLobbyIds.Clear();
            }
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
