using MDEN.Managers;
using MDEN.Patches;
using MDEN.Protocol.Messages.Battle;
using MDEN.Protocol.Messages.Chat;
using MelonLoader;

namespace MDEN.UI.Core
{
    public static class SettlementHudController
    {
        private static bool _initialized;
        private static int _pendingGeneration;
        private static SettlementResultPush _pendingResult;
        private static int _finishedBroadcastCount;
        private static bool _loggedWaitingForLobby;

        public static void Initialize()
        {
            if (_initialized) return;
            SettlementManager.SettlementReceived += HandleSettlementReceived;
            LobbyManager.CurrentLobbyChanged += HandleCurrentLobbyChanged;
            ChatManager.MessageReceived += HandleChatMessageReceived;
            _initialized = true;
        }

        public static void Deinitialize()
        {
            if (!_initialized) return;
            SettlementManager.SettlementReceived -= HandleSettlementReceived;
            LobbyManager.CurrentLobbyChanged -= HandleCurrentLobbyChanged;
            ChatManager.MessageReceived -= HandleChatMessageReceived;
            _pendingGeneration++;
            _pendingResult = null;
            _finishedBroadcastCount = 0;
            _loggedWaitingForLobby = false;
            SettlementResultDialog.Destroy();
            _initialized = false;
        }

        private static void HandleSettlementReceived(SettlementResultPush result)
        {
            if (result == null) return;

            _pendingResult = result;
            _loggedWaitingForLobby = false;
            MarkAllPlayersFinishedFromSettlementResult();
            var generation = ++_pendingGeneration;
            MelonLogger.Msg($"Settlement result received. finished={_finishedBroadcastCount}, players={LobbyManager.CurrentLobby?.Players?.Length ?? 0}, playing={LobbyManager.CurrentLobby?.IsPlaying}, resultFlow={BattleFlowPatch.IsBattleResultFlowPending}");
            TryShowSettlement(generation);
        }

        private static void HandleCurrentLobbyChanged(MDEN.Protocol.Messages.Lobby.LobbySyncPush lobby)
        {
            if (!_initialized) return;

            if (lobby?.IsPlaying == true)
            {
                _finishedBroadcastCount = 0;
                return;
            }

            if (_pendingResult == null) return;

            var generation = _pendingGeneration;
            MainThreadDispatcher.Enqueue(() => TryShowSettlement(generation));
        }

        private static void HandleChatMessageReceived(ChatPushMsg message)
        {
            if (!_initialized || message == null || !message.IsSystem) return;

            if (TryParseFinishedPlayerName(message.Message, out var playerName))
            {
                _finishedBroadcastCount++;
                MelonLogger.Msg($"Settlement finish broadcast counted: {_finishedBroadcastCount}/{LobbyManager.CurrentLobby?.Players?.Length ?? 0}");
                if (_pendingResult != null)
                {
                    var generation = _pendingGeneration;
                    MainThreadDispatcher.Enqueue(() => TryShowSettlement(generation));
                }
            }
        }

        private static void TryShowSettlement(int generation)
        {
            if (!_initialized || generation != _pendingGeneration || _pendingResult == null) return;

            if (!LobbyManager.IsInLobby)
            {
                _pendingResult = null;
                return;
            }

            if (!CanShowSettlement())
            {
                if (!_loggedWaitingForLobby)
                {
                    _loggedWaitingForLobby = true;
                    MelonLogger.Msg($"Settlement result pending until lobby is visible. playing={LobbyManager.CurrentLobby?.IsPlaying}, resultFlow={BattleFlowPatch.IsBattleResultFlowPending}, homeVisible={RoomSceneOverlay.IsHomeVisible}, finished={_finishedBroadcastCount}/{LobbyManager.CurrentLobby?.Players?.Length ?? 0}");
                }

                MainThreadDispatcher.Enqueue(() => TryShowSettlement(generation));
                return;
            }

            var result = _pendingResult;
            _pendingResult = null;
            _finishedBroadcastCount = 0;
            _loggedWaitingForLobby = false;
            if (result != null)
            {
                MelonLogger.Msg("Showing settlement result dialog.");
                SettlementResultDialog.Show(result);
            }
        }

        private static bool CanShowSettlement()
        {
            return LobbyManager.IsInLobby &&
                   LobbyManager.CurrentLobby?.IsPlaying != true &&
                   !BattleFlowPatch.IsBattleResultFlowPending &&
                   RoomSceneOverlay.IsHomeVisible &&
                   HasEveryoneFinished();
        }

        private static bool HasEveryoneFinished()
        {
            var playerCount = LobbyManager.CurrentLobby?.Players?.Length ?? 0;
            return playerCount > 0 && _finishedBroadcastCount >= playerCount;
        }

        private static void MarkAllPlayersFinishedFromSettlementResult()
        {
            var playerCount = LobbyManager.CurrentLobby?.Players?.Length ?? 0;
            if (playerCount > 0 && _finishedBroadcastCount < playerCount)
            {
                _finishedBroadcastCount = playerCount;
            }
        }

        private static bool TryParseFinishedPlayerName(string message, out string playerName)
        {
            playerName = null;
            if (string.IsNullOrWhiteSpace(message)) return false;

            const string suffix = " 已完成";
            if (!message.EndsWith(suffix)) return false;

            playerName = StripRichText(message.Substring(0, message.Length - suffix.Length)).Trim();
            return !string.IsNullOrWhiteSpace(playerName) && playerName != "已完成";
        }

        private static string StripRichText(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return string.Empty;

            var result = value;
            while (true)
            {
                var start = result.IndexOf('<');
                var end = start >= 0 ? result.IndexOf('>', start) : -1;
                if (start < 0 || end < 0) break;
                result = result.Remove(start, end - start + 1);
            }

            return result;
        }
    }
}
