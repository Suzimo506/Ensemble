using System.Collections.Generic;
using MDEN.Managers;
using MDEN.Protocol.Messages.Chat;
using MDEN.UI.Displays;
using MDEN.UI.Windows;

namespace MDEN.UI.Core
{
    public static class RoomHudController
    {
        internal const string ChatGuideMessageKey = "ChatGuide";
        private const int MaxRefreshRetries = 120;
        private const int EntranceFallbackDelayFrames = 20;
        private const int CharacterRepairCooldownFrames = 30;
        private const int CharacterRebuildDelayFrames = 8;
        private static readonly int[] PostBattleCharacterRebuildDelays = { 8, 30, 90 };
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
        private static bool _battleSceneActive;
        private static bool _roomHudDeferred;
        private static int _popupSuppressionCount;
        private static int _chatScopeLobbyId = int.MinValue;

        public static void Initialize()
        {
            if (_initialized) return;
            LobbyManager.CurrentLobbyChanged += HandleLobbyChanged;
            ChatManager.MessageReceived += HandleChatMessageReceived;
            ConnectionManager.StateChanged += HandleConnectionStateChanged;
            PlayerManager.ProfileChanged += HandleProfileChanged;
            _initialized = true;
        }

        public static void Deinitialize()
        {
            if (!_initialized) return;
            LobbyManager.CurrentLobbyChanged -= HandleLobbyChanged;
            ChatManager.MessageReceived -= HandleChatMessageReceived;
            ConnectionManager.StateChanged -= HandleConnectionStateChanged;
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

        private static void HandleConnectionStateChanged(ConnectionLifecycleState state)
        {
            MainThreadDispatcher.Enqueue(() =>
            {
                if (state == ConnectionLifecycleState.Connected)
                {
                    RequestRefresh();
                    return;
                }

                if (!LobbyManager.IsInLobby)
                {
                    Destroy();
                }
            });
        }

        private static void HandleProfileChanged()
        {
            MainThreadDispatcher.Enqueue(OnProfileChanged);
        }

        private static void OnLobbyChanged(MDEN.Protocol.Messages.Lobby.LobbySyncPush lobby)
        {
            if (lobby == null && LobbyManager.CurrentLobby != null) return;
            if (lobby != null && LobbyManager.CurrentLobby?.Id != lobby.Id) return;

            if (IsPopupSuppressed)
            {
                _roomHudDeferred = true;
                HidePopupSensitiveObjects();
                MultiplayerBattleController.OnLobbyChanged();
                return;
            }

            if (ShouldSkipRoomHudForBattleScene())
            {
                DeferRoomHudRefresh();
                MultiplayerBattleController.OnLobbyChanged();
                return;
            }

            var isNewLobbyEntry = lobby != null && _entranceTrackedLobbyId != lobby.Id;
            if (lobby == null)
            {
                _entranceTrackedLobbyId = -1;
            }
            else if (isNewLobbyEntry)
            {
                _entranceTrackedLobbyId = lobby.Id;
            }

            if (ShouldUpdateRoomHud(lobby))
            {
                _roomHudDeferred = false;
                Refresh(lobby);
                ScheduleEntranceFallback(lobby, isNewLobbyEntry);
                NavigationButton.RefreshRoomButton();
                PreparationStartController.BindOrRefresh();
            }
            else
            {
                DeferRoomHudRefresh();
            }

            ChartPreviewController.OnLobbyChanged(lobby);
            TenziDrawController.OnLobbyChanged(lobby);
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
            for (var i = 0; i < PostBattleCharacterRebuildDelays.Length; i++)
            {
                RebuildRoomCharactersDelayed(PostBattleCharacterRebuildDelays[i], i + 1);
            }
        }

        public static void SetBattleSceneActive(bool active)
        {
            _battleSceneActive = active;
            if (!active)
            {
                _roomHudDeferred = false;
            }
        }

        private static void RebuildRoomCharactersDelayed(int framesRemaining)
        {
            RebuildRoomCharactersDelayed(framesRemaining, 0);
        }

        private static void RebuildRoomCharactersDelayed(int framesRemaining, int generationOffset)
        {
            if (framesRemaining > 0)
            {
                MainThreadDispatcher.Enqueue(() => RebuildRoomCharactersDelayed(framesRemaining - 1, generationOffset));
                return;
            }

            RoomCharacterDisplay.DestroyGeneratedObjects();
            _characterRepairCooldownFrames = 0;
            RequestRefresh();
        }

        public static void Update()
        {
            if (IsPopupSuppressed)
            {
                RestoreNativeInput();
                return;
            }

            if (ShouldSkipRoomHudForBattleScene())
            {
                RestoreNativeInput();
                return;
            }

            var nativeSettingsOpen = RoomSceneOverlay.UpdateVisibility(Chat.IsVisible);
            ClosePlayerInfoWindowsForNativeSettings(nativeSettingsOpen);

            if (ShouldPauseRoomHudUpdate())
            {
                RestoreNativeInput();
                return;
            }

            Chat.Update();
            UpdateNativeInputBlock();
            ReadyDisplay.Update();
            StageDesignerTextController.Update();
            RoomCharacterDisplay.UpdateLabelPositions(LobbyManager.CurrentLobby);
            RepairRoomCharactersIfNeeded();
        }

        private static void ClosePlayerInfoWindowsForNativeSettings(bool nativeSettingsOpen)
        {
            if (!nativeSettingsOpen) return;

            WindowStackController.CloseCurrentWindowIf<RoomPlayerProfileWindow>();
            WindowStackController.CloseCurrentWindowIf<RoomPlayerWindow>();
        }

        public static bool IsChatConsumingInput => Chat.IsConsumingInput;

        private static void Refresh(MDEN.Protocol.Messages.Lobby.LobbySyncPush lobby, int retryCount = 0)
        {
            if (IsPopupSuppressed)
            {
                HidePopupSensitiveObjects();
                return;
            }

            if (lobby == null || !LobbyManager.IsInLobby)
            {
                if (CanShowServerChat())
                {
                    EnsureChatScope(-1);
                    if (RoomSceneOverlay.IsNavigationReady)
                    {
                        Chat.CreateEmpty();
                    }

                    PlayerList.Destroy();
                    ReadyDisplay.Destroy();
                    RoomSceneOverlay.Hide();
                    RoomCharacterDisplay.HideGeneratedObjects();
                }
                else
                {
                    Destroy();
                }

                return;
            }

            if (LobbyManager.CurrentLobby?.Id != lobby.Id) return;
            EnsureChatScope(lobby.Id);

            if (RoomSceneOverlay.IsNavigationReady)
            {
                Chat.CreateEmpty();
                if (Chat.IsVisible)
                {
                    PlayerList.Update(lobby);
                }
                else
                {
                    PlayerList.Destroy();
                }

                ReadyDisplay.Refresh(lobby);
            }

            if (!RoomSceneOverlay.IsRoomInfoVisible)
            {
                RoomSceneOverlay.Hide();
                RoomCharacterDisplay.HideGeneratedObjects();
                if (!ShouldRetryRoomHudRefresh(lobby)) return;
                ScheduleRefreshRetry(lobby, retryCount);
                return;
            }

            if (!RoomSceneOverlay.IsHomeVisible)
            {
                RoomCharacterDisplay.HideGeneratedObjects();
                RoomSceneOverlay.Refresh(lobby, Chat.IsVisible);
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
                    MDEN.Managers.ClientLogManager.Warning("Room character display is not ready.");
                    _characterNotReadyLogged = true;
                }
            }

            RoomSceneOverlay.Refresh(lobby, Chat.IsVisible);

            if ((NeedsRefreshRetry() || !characterReady) && retryCount < MaxRefreshRetries)
            {
                ScheduleRefreshRetry(lobby, retryCount);
            }
        }

        private static bool ShouldUpdateRoomHud(MDEN.Protocol.Messages.Lobby.LobbySyncPush lobby)
        {
            if (lobby != null && lobby.IsPlaying && _battleSceneActive) return false;
            return lobby == null || !lobby.IsPlaying || RoomSceneOverlay.IsHomeVisible;
        }

        private static bool ShouldRetryRoomHudRefresh(MDEN.Protocol.Messages.Lobby.LobbySyncPush lobby)
        {
            if (lobby != null && lobby.IsPlaying && _battleSceneActive) return false;
            return lobby == null || !lobby.IsPlaying;
        }

        private static bool ShouldPauseRoomHudUpdate()
        {
            var lobby = LobbyManager.CurrentLobby;
            if (lobby == null || !lobby.IsPlaying) return false;
            if (_battleSceneActive) return true;

            return !RoomSceneOverlay.IsHomeVisible;
        }

        private static bool ShouldSkipRoomHudForBattleScene()
        {
            return _battleSceneActive && LobbyManager.CurrentLobby?.IsPlaying == true;
        }

        private static void DeferRoomHudRefresh()
        {
            if (_roomHudDeferred) return;

            _roomHudDeferred = true;
            _pendingRetryGeneration++;
            _retryDelayFrames = 0;
            RestoreNativeInput();
            PlayerList.Destroy();
            Chat.ResetSceneObjects();
            ReadyDisplay.Destroy();
            TenziDrawController.Close();
            RoomSceneOverlay.Hide();
            RoomCharacterDisplay.DestroyGeneratedObjects();
        }

        public static System.IDisposable SuppressForPopupWindow(string reason)
        {
            _popupSuppressionCount++;
            if (_popupSuppressionCount == 1)
            {
                MDEN.Managers.ClientLogManager.Msg($"Room HUD suppressed for popup: {reason}");
                HidePopupSensitiveObjects();
            }

            return new PopupSuppressionToken(ReleasePopupSuppression);
        }

        private static bool IsPopupSuppressed => _popupSuppressionCount > 0;

        private static void ReleasePopupSuppression()
        {
            if (_popupSuppressionCount <= 0) return;

            _popupSuppressionCount--;
            if (_popupSuppressionCount > 0) return;

            _popupSuppressionCount = 0;
            _roomHudDeferred = false;
            MDEN.Managers.ClientLogManager.Msg("Room HUD popup suppression released.");
            RequestRefresh();
        }

        private static void HidePopupSensitiveObjects()
        {
            RestoreNativeInput();
            PlayerList.Destroy();
            Chat.ResetSceneObjects();
            ReadyDisplay.Destroy();
            TenziDrawController.Close();
            RoomSceneOverlay.Destroy();
            RoomCharacterDisplay.DestroyGeneratedObjects();
        }

        private sealed class PopupSuppressionToken : System.IDisposable
        {
            private System.Action _release;

            public PopupSuppressionToken(System.Action release)
            {
                _release = release;
            }

            public void Dispose()
            {
                var release = _release;
                if (release == null) return;

                _release = null;
                release();
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
            if (lobby.IsPlaying) return;
            if (RoomCharacterDisplay.HasExpectedCharacterContent(lobby)) return;

            _characterRepairCooldownFrames = CharacterRepairCooldownFrames;
            RequestRefresh();
        }

        public static void Destroy()
        {
            _pendingRetryGeneration++;
            _retryDelayFrames = 0;
            _roomHudDeferred = false;
            _popupSuppressionCount = 0;
            ResetEntranceFallback();
            _characterNotReadyLogged = false;
            _characterRepairCooldownFrames = 0;
            PlayerList.Destroy();
            Chat.Destroy();
            ReadyDisplay.Destroy();
            TenziDrawController.Reset();
            StageDesignerTextController.Restore();
            RoomSceneOverlay.Destroy();
            RoomCharacterDisplay.Destroy();
            ChartPreviewController.ResetAll();
            MultiplayerBattleController.Reset();
            RestoreNativeInput();
            _chatScopeLobbyId = int.MinValue;
        }

        public static void ResetSceneObjects()
        {
            _pendingRetryGeneration++;
            _retryDelayFrames = 0;
            _roomHudDeferred = false;
            _popupSuppressionCount = 0;
            ResetEntranceFallback(false);
            _characterNotReadyLogged = false;
            _characterRepairCooldownFrames = 0;
            PlayerList.Destroy();
            Chat.ResetSceneObjects();
            ReadyDisplay.Destroy();
            TenziDrawController.Reset();
            StageDesignerTextController.Restore();
            RoomSceneOverlay.Destroy();
            RoomCharacterDisplay.Destroy();
            ChartPreviewController.ResetAll();
            MultiplayerBattleController.Reset();
            RestoreNativeInput();
        }

        private static void EnsureChatScope(int lobbyId)
        {
            if (_chatScopeLobbyId == lobbyId) return;

            _chatScopeLobbyId = lobbyId;
            Chat.ResetSendMode();
            Chat.ClearHistory();
            AddRoomCommandTip(lobbyId);
        }

        private static void AddRoomCommandTip(int lobbyId)
        {
            Chat.AddMessage(new ChatPushMsg
            {
                AuthorUid = "system",
                AuthorName = "System",
                Message = ChatGuideMessageKey,
                IsSystem = true
            });
        }

        public static void AddChatMessage(ChatPushMsg message)
        {
            if (!LobbyManager.IsInLobby)
            {
                if (!CanShowServerChat()) return;

                Chat.AddMessage(message);
                return;
            }

            if (IsOwnEntranceMessage(message))
            {
                var lobbyId = LobbyManager.CurrentLobby.Id;
                if (EntranceAnnouncedLobbyIds.Contains(lobbyId)) return;

                _entranceDisplayedLobbyId = lobbyId;
                _entranceFallbackCompleted = true;
                _entranceTrackedLobbyId = lobbyId;
                EntranceAnnouncedLobbyIds.Add(lobbyId);
            }

            var hudPaused = ShouldPauseRoomHudUpdate();
            Chat.AddMessage(message, !hudPaused);
            if (hudPaused) return;

            if (!message.IsSystem && message.Channel == ChatTargets.Lobby)
            {
                RoomCharacterDisplay.ShowChatBubble(message.AuthorUid, message.Message);
            }
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
            var shouldBlock = (LobbyManager.IsInLobby || CanShowServerChat()) && Chat.IsConsumingInput;
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
            NativeInputBlocker.SetBlocked("RoomChat", blocked);
        }

        private static bool CanShowServerChat()
        {
            return ConnectionManager.CanSendRequests;
        }
    }
}
