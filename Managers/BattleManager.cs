using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Il2CppAssets.Scripts.GameCore.HostComponent;
using Il2CppFormulaBase;
using MDEN.Network;
using MDEN.Protocol;
using MDEN.Protocol.Messages.Battle;
using MDEN.Protocol.Models;
using MDEN.UI.Core;
using MelonLoader;

namespace MDEN.Managers
{
    public static class BattleManager
    {
        private const int BattleUpdateIntervalMs = 500;
        private static readonly object BattleDataLock = new();
        private static readonly object BattleDataDispatchLock = new();
        private static readonly Dictionary<string, BattlePlayerEntry> PlayerBattleData = new();
        private static CancellationTokenSource _syncCts;
        private static BattlePlayerEntry[] _pendingBattleDataDispatch = Array.Empty<BattlePlayerEntry>();
        private static bool _battleDataDispatchQueued;
        private static string _activeBattleId;
        private static TaskStageTarget _taskStageTarget;
        private static BattleRoleAttributeComponent _battleRoleAttributeComponent;
        private static bool _synchronizing;
        private static bool _finishReported;
        private static bool _forcedDead;
        private static bool _accuracyInitialized;
        private static bool _multiplayerBattleActive;
        private static DateTime _battleStartedUtc;

        public static bool Synchronizing => _synchronizing;
        public static bool IsActiveMultiplayerBattle =>
            LobbyManager.IsInLobby && (_multiplayerBattleActive || LobbyManager.CurrentLobby?.IsPlaying == true);
        public static event Action<BattlePlayerEntry[]> BattleDataChanged;

        public static void Init()
        {
            PushDispatcher.Instance.Register<BattleDataPushMsg>(OpCodes.BattleDataPush, OnBattleDataPush);
        }

        public static BattlePlayerEntry[] GetBattleDataSnapshot()
        {
            lock (BattleDataLock)
            {
                var result = new BattlePlayerEntry[PlayerBattleData.Count];
                PlayerBattleData.Values.CopyTo(result, 0);
                return result;
            }
        }

        public static void PrepareForNewBattle()
        {
            MarkMultiplayerBattleStarting();
            _battleStartedUtc = DateTime.UtcNow;
            _activeBattleId = LobbyManager.CurrentLobby?.CurrentBattleId;

            lock (BattleDataLock)
            {
                PlayerBattleData.Clear();
            }

            NotifyBattleDataChanged(Array.Empty<BattlePlayerEntry>());
        }

        public static async Task SyncStartAsync()
        {
            if (_synchronizing || !IsActiveMultiplayerBattle) return;

            _finishReported = false;
            _forcedDead = false;
            _accuracyInitialized = false;
            EnsureBattleComponents();
            _syncCts?.Cancel();
            _syncCts?.Dispose();
            _syncCts = new CancellationTokenSource();
            _synchronizing = true;

            try
            {
                await SyncLoopAsync(_syncCts.Token);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                MDEN.Managers.ClientLogManager.Warning($"Battle sync failed: {ex.Message}");
            }
            finally
            {
                _synchronizing = false;
                if (IsActiveMultiplayerBattle)
                {
                    await SendCurrentAsync();
                }
            }
        }

        public static async Task ReportBattleFinishedAsync(bool alive)
        {
            if (_finishReported) return;

            _finishReported = true;
            _forcedDead = !alive;
            StopSyncLoop();

            var playedSeconds = GetPlayedSeconds();
            await FlushFinalBattleDataAsync();

            if (!IsNetworkReady()) return;

            try
            {
                await NetworkClient.Instance.SendRequestAsync<BattleReturnedReq, BattleReturnedResp>(
                    OpCodes.BattleReturnedReq,
                    new BattleReturnedReq
                    {
                        BattleId = _activeBattleId,
                        PlayedSeconds = playedSeconds,
                        FinalPlayer = GetLocalBattleEntry()
                    });
            }
            catch (Exception ex)
            {
                MDEN.Managers.ClientLogManager.Warning($"Battle returned request failed: {ex.Message}");
            }
        }

        public static void MarkLocalBattleFinished(bool alive)
        {
            _forcedDead = !alive;

            var uid = PlayerManager.CurrentUid;
            if (string.IsNullOrWhiteSpace(uid)) return;

            try
            {
                if (EnsureBattleComponents())
                {
                    var notify = CreateCurrentNotify();
                    if (notify != null)
                    {
                        ApplyLocalBattleData(notify);
                        return;
                    }
                }
            }
            catch (Exception ex)
            {
                MDEN.Managers.ClientLogManager.Warning($"Failed to mark local battle finish state: {ex.Message}");
            }

            lock (BattleDataLock)
            {
                if (PlayerBattleData.TryGetValue(uid, out var existing))
                {
                    existing.Alive = alive;
                    existing.FC = alive && existing.FC;
                }
                else
                {
                    PlayerBattleData[uid] = new BattlePlayerEntry
                    {
                        Uid = uid,
                        Alive = alive,
                        FC = false
                    };
                }
            }

            NotifyBattleDataChanged(GetBattleDataSnapshot());
        }

        public static void ReportBattleStartFailed(int lobbyId, string battleId, string entry, string reasonCode, string reason)
        {
            if (!ConnectionManager.CanSendRequests || string.IsNullOrWhiteSpace(battleId)) return;

            _ = ReportBattleStartFailedAsync(lobbyId, battleId, entry, reasonCode, reason);
        }

        private static float GetPlayedSeconds()
        {
            if (_battleStartedUtc == default) return 0f;

            var seconds = (float)(DateTime.UtcNow - _battleStartedUtc).TotalSeconds;
            if (float.IsNaN(seconds) || float.IsInfinity(seconds) || seconds <= 0f) return 0f;
            return seconds > 7200f ? 7200f : seconds;
        }

        public static void StopSyncLoop()
        {
            try
            {
                _syncCts?.Cancel();
            }
            catch
            {
            }
        }

        public static void MarkMultiplayerBattleStarting()
        {
            _multiplayerBattleActive = true;
        }

        public static void MarkMultiplayerBattleEnded()
        {
            _multiplayerBattleActive = false;
        }

        public static void Reset()
        {
            StopSyncLoop();
            MarkMultiplayerBattleEnded();
            _finishReported = false;
            _forcedDead = false;
            _activeBattleId = null;
            _taskStageTarget = null;
            _battleRoleAttributeComponent = null;
            _accuracyInitialized = false;
            _battleStartedUtc = default;

            lock (BattleDataLock)
            {
                PlayerBattleData.Clear();
            }

            NotifyBattleDataChanged(Array.Empty<BattlePlayerEntry>());
        }

        private static async Task SyncLoopAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested && IsActiveMultiplayerBattle)
            {
                await SendCurrentAsync();
                await Task.Delay(BattleUpdateIntervalMs, cancellationToken);
            }
        }

        private static async Task FlushFinalBattleDataAsync()
        {
            await SendCurrentAsync();
            await Task.Delay(250);
            await SendCurrentAsync();
            await Task.Delay(750);
            await SendCurrentAsync();
        }

        private static async Task SendCurrentAsync()
        {
            if (!IsNetworkReady() || !EnsureBattleComponents()) return;
            var battleId = GetCurrentBattleId();
            if (string.IsNullOrWhiteSpace(battleId)) return;

            try
            {
                var notify = await CreateCurrentNotifyAsync();
                if (notify == null) return;
                notify.BattleId = battleId;
                ApplyLocalBattleData(notify);

                await NetworkClient.Instance.SendNotifyAsync(
                    OpCodes.BattleDataNotify,
                    notify);
            }
            catch (Exception ex)
            {
                MDEN.Managers.ClientLogManager.Warning($"Battle data notify failed: {ex.Message}");
            }
        }

        private static async Task ReportBattleStartFailedAsync(
            int lobbyId,
            string battleId,
            string entry,
            string reasonCode,
            string reason)
        {
            try
            {
                await NetworkClient.Instance.SendNotifyAsync(
                    OpCodes.BattleStartFailedNotify,
                    new BattleStartFailedNotify
                    {
                        LobbyId = lobbyId,
                        BattleId = battleId,
                        Entry = entry,
                        ReasonCode = reasonCode,
                        Reason = reason
                    });
            }
            catch (Exception ex)
            {
                MDEN.Managers.ClientLogManager.Warning($"Battle start failure report failed: {ex.Message}");
            }
        }

        private static void ApplyLocalBattleData(BattleDataNotifyMsg notify)
        {
            using (PerfTrace.Measure("MDEN.BattleManager.ApplyLocalBattleData"))
            {
                var uid = PlayerManager.CurrentUid;
                if (string.IsNullOrWhiteSpace(uid)) return;

                lock (BattleDataLock)
                {
                    PlayerBattleData[uid] = new BattlePlayerEntry
                    {
                        Uid = uid,
                        Score = notify.Score,
                        Accuracy = notify.Accuracy,
                        Perfects = notify.Perfects,
                        Greats = notify.Greats,
                        Earlies = notify.Earlies,
                        Lates = notify.Lates,
                        Misses = notify.Misses,
                        FC = notify.FC,
                        Alive = notify.Alive,
                        PingMS = 0
                    };
                }

                NotifyBattleDataChanged(GetBattleDataSnapshot());
            }
        }

        private static BattlePlayerEntry GetLocalBattleEntry()
        {
            var uid = PlayerManager.CurrentUid;
            if (string.IsNullOrWhiteSpace(uid)) return null;

            lock (BattleDataLock)
            {
                return PlayerBattleData.TryGetValue(uid, out var entry) ? CloneBattleEntry(entry) : null;
            }
        }

        private static BattlePlayerEntry CloneBattleEntry(BattlePlayerEntry entry)
        {
            if (entry == null) return null;

            return new BattlePlayerEntry
            {
                Uid = entry.Uid,
                Score = entry.Score,
                Accuracy = entry.Accuracy,
                Perfects = entry.Perfects,
                Greats = entry.Greats,
                Earlies = entry.Earlies,
                Lates = entry.Lates,
                Misses = entry.Misses,
                FC = entry.FC,
                Alive = entry.Alive,
                PingMS = entry.PingMS
            };
        }

        private static Task<BattleDataNotifyMsg> CreateCurrentNotifyAsync()
        {
            var source = new TaskCompletionSource<BattleDataNotifyMsg>(TaskCreationOptions.RunContinuationsAsynchronously);
            MainThreadDispatcher.Enqueue(() =>
            {
                try
                {
                    source.TrySetResult(CreateCurrentNotify());
                }
                catch (Exception ex)
                {
                    source.TrySetException(ex);
                }
            });
            return source.Task;
        }

        private static BattleDataNotifyMsg CreateCurrentNotify()
        {
            using (PerfTrace.Measure("MDEN.BattleManager.CreateCurrentNotify"))
            {
                EnsureBattleComponents();
                if (_taskStageTarget == null) return null;

                var alive = !_forcedDead && (_battleRoleAttributeComponent == null || !_battleRoleAttributeComponent.IsDead());

                return new BattleDataNotifyMsg
                {
                    BattleId = GetCurrentBattleId(),
                    Score = (uint)_taskStageTarget.GetScore(),
                    Accuracy = AccuracyManager.GetCalculatedAccuracy(),
                    Perfects = (ushort)_taskStageTarget.m_PerfectResult,
                    Greats = (ushort)_taskStageTarget.m_GreatResult,
                    Earlies = (ushort)(_battleRoleAttributeComponent?.early ?? 0),
                    Lates = (ushort)(_battleRoleAttributeComponent?.late ?? 0),
                    Misses = (ushort)_taskStageTarget.GetComboMiss(),
                    FC = _taskStageTarget.IsFullCombo(),
                    Alive = alive
                };
            }
        }

        private static void OnBattleDataPush(BattleDataPushMsg push)
        {
            var battleId = GetCurrentBattleId();
            if (string.IsNullOrWhiteSpace(battleId) || push?.BattleId != battleId)
            {
                return;
            }

            var players = push?.Players ?? Array.Empty<BattlePlayerEntry>();
            lock (BattleDataLock)
            {
                foreach (var player in players)
                {
                    if (player == null || string.IsNullOrWhiteSpace(player.Uid)) continue;
                    PlayerBattleData[player.Uid] = player;
                }
            }

            NotifyBattleDataChanged(GetBattleDataSnapshot());
        }

        private static void NotifyBattleDataChanged(BattlePlayerEntry[] players)
        {
            lock (BattleDataDispatchLock)
            {
                _pendingBattleDataDispatch = players ?? Array.Empty<BattlePlayerEntry>();
                if (_battleDataDispatchQueued) return;

                _battleDataDispatchQueued = true;
            }

            MainThreadDispatcher.Enqueue(DispatchBattleDataChanged);
        }

        private static void DispatchBattleDataChanged()
        {
            BattlePlayerEntry[] players;
            lock (BattleDataDispatchLock)
            {
                players = _pendingBattleDataDispatch ?? Array.Empty<BattlePlayerEntry>();
                _pendingBattleDataDispatch = Array.Empty<BattlePlayerEntry>();
                _battleDataDispatchQueued = false;
            }

            using (PerfTrace.Measure("MDEN.BattleManager.NotifyBattleDataChanged"))
            {
                BattleDataChanged?.Invoke(players);
            }
        }

        private static bool EnsureBattleComponents()
        {
            _taskStageTarget ??= TaskStageTarget.instance;
            _battleRoleAttributeComponent ??= BattleRoleAttributeComponent.instance;

            if (!_accuracyInitialized && _taskStageTarget != null && StageBattleComponent.instance != null)
            {
                AccuracyManager.Init();
                _accuracyInitialized = true;
                _taskStageTarget ??= TaskStageTarget.instance;
                _battleRoleAttributeComponent ??= BattleRoleAttributeComponent.instance;
            }

            return _taskStageTarget != null;
        }

        private static bool IsNetworkReady()
        {
            return IsActiveMultiplayerBattle &&
                   ConnectionManager.CanSendRequests;
        }

        private static string GetCurrentBattleId()
        {
            var lobby = LobbyManager.CurrentLobby;
            if (lobby == null || !lobby.IsPlaying)
            {
                _activeBattleId = null;
                return null;
            }

            var currentBattleId = lobby.CurrentBattleId;
            if (!string.IsNullOrWhiteSpace(currentBattleId))
            {
                _activeBattleId = currentBattleId;
            }

            return _activeBattleId;
        }
    }
}
