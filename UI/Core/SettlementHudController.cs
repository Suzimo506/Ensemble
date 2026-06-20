using MDEN.Managers;
using MDEN.Protocol.Messages.Battle;
using MDEN.Protocol.Messages.Chat;
using System;

namespace MDEN.UI.Core
{
    public static class SettlementHudController
    {
        private const int RetryDelayFrames = 10;
        private static readonly TimeSpan BattleResultFlowWaitTimeout = TimeSpan.FromSeconds(25);
        private static readonly TimeSpan HomeVisibleWaitTimeout = TimeSpan.FromSeconds(90);
        private static bool _initialized;
        private static int _pendingGeneration;
        private static SettlementResultPush _pendingResult;
        private static int _finishedBroadcastCount;
        private static bool _loggedWaitingForLobby;
        private static bool _retryScheduled;
        private static DateTime _pendingSinceUtc;

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
            _retryScheduled = false;
            _pendingSinceUtc = default;
            SettlementResultDialog.Destroy();
            _initialized = false;
        }

        private static void HandleSettlementReceived(SettlementResultPush result)
        {
            if (result == null) return;

            _pendingResult = result;
            _loggedWaitingForLobby = false;
            _retryScheduled = false;
            _pendingSinceUtc = DateTime.UtcNow;
            MarkAllPlayersFinishedFromSettlementResult();
            var generation = ++_pendingGeneration;
            MDEN.Managers.ClientLogManager.Msg($"Settlement result received. finished={_finishedBroadcastCount}, players={LobbyManager.CurrentLobby?.Players?.Length ?? 0}, playing={LobbyManager.CurrentLobby?.IsPlaying}, resultFlow={BattleResultFlowManager.IsBattleResultFlowPending}");
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
            ScheduleTryShowSettlement(generation, 0);
        }

        private static void HandleChatMessageReceived(ChatPushMsg message)
        {
            if (!_initialized || message == null || !message.IsSystem) return;

            if (TryParseFinishedPlayerName(message.Message, out var playerName))
            {
                _finishedBroadcastCount++;
                MDEN.Managers.ClientLogManager.Msg($"Settlement finish broadcast counted: {_finishedBroadcastCount}/{LobbyManager.CurrentLobby?.Players?.Length ?? 0}");
                if (_pendingResult != null)
                {
                    var generation = _pendingGeneration;
                    ScheduleTryShowSettlement(generation, 0);
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
                    MDEN.Managers.ClientLogManager.Msg($"Settlement result pending until lobby is visible. playing={LobbyManager.CurrentLobby?.IsPlaying}, resultFlow={BattleResultFlowManager.IsBattleResultFlowPending}, homeVisible={RoomSceneOverlay.IsHomeVisible}, finished={_finishedBroadcastCount}/{LobbyManager.CurrentLobby?.Players?.Length ?? 0}");
                }

                ScheduleTryShowSettlement(generation);
                return;
            }

            var result = _pendingResult;
            var waitReason = GetSettlementWaitOverrideReason();
            _pendingResult = null;
            _finishedBroadcastCount = 0;
            _loggedWaitingForLobby = false;
            _retryScheduled = false;
            _pendingSinceUtc = default;
            if (result != null)
            {
                MDEN.Managers.ClientLogManager.Msg(string.IsNullOrEmpty(waitReason)
                    ? "Showing settlement result dialog."
                    : $"Showing settlement result dialog after wait override: {waitReason}");
                SettlementResultDialog.Show(result);
            }
        }

        private static void ScheduleTryShowSettlement(int generation, int delayFrames = RetryDelayFrames)
        {
            if (_retryScheduled) return;

            _retryScheduled = true;
            MainThreadDispatcher.Enqueue(() => RunDelayedTryShowSettlement(generation, delayFrames));
        }

        private static void RunDelayedTryShowSettlement(int generation, int framesRemaining)
        {
            if (!_initialized || generation != _pendingGeneration || _pendingResult == null)
            {
                _retryScheduled = false;
                return;
            }

            if (framesRemaining > 0)
            {
                MainThreadDispatcher.Enqueue(() => RunDelayedTryShowSettlement(generation, framesRemaining - 1));
                return;
            }

            _retryScheduled = false;
            TryShowSettlement(generation);
        }

        private static bool CanShowSettlement()
        {
            var waited = _pendingSinceUtc == default ? TimeSpan.Zero : DateTime.UtcNow - _pendingSinceUtc;
            var battleResultReady = !BattleResultFlowManager.IsBattleResultFlowPending ||
                                    waited >= BattleResultFlowWaitTimeout;
            var homeReady = RoomSceneOverlay.IsHomeVisible ||
                            waited >= HomeVisibleWaitTimeout;

            return LobbyManager.IsInLobby &&
                   LobbyManager.CurrentLobby?.IsPlaying != true &&
                   battleResultReady &&
                   homeReady &&
                   HasEveryoneFinished();
        }

        private static string GetSettlementWaitOverrideReason()
        {
            if (_pendingSinceUtc == default) return null;

            var waited = DateTime.UtcNow - _pendingSinceUtc;
            var reasons = new System.Collections.Generic.List<string>();
            if (BattleResultFlowManager.IsBattleResultFlowPending && waited >= BattleResultFlowWaitTimeout)
            {
                reasons.Add("battleResultFlowTimeout");
            }

            if (!RoomSceneOverlay.IsHomeVisible && waited >= HomeVisibleWaitTimeout)
            {
                reasons.Add("homeVisibleTimeout");
            }

            return reasons.Count == 0 ? null : string.Join(",", reasons);
        }

        private static bool HasEveryoneFinished()
        {
            var playerCount = LobbyManager.CurrentLobby?.Players?.Length ?? 0;
            return _pendingResult != null || playerCount > 0 && _finishedBroadcastCount >= playerCount;
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
