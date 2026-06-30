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
        private const int SmallLobbyBattleUpdateIntervalMs = 2000;
        private const int LargeLobbyBattleUpdateIntervalMs = 3000;
        private const int LargeLobbyBattlePlayerThreshold = 5;
        private const int BattleReturnedRetryIntervalMs = 5000;
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
        private static DateTime _lastBattleReturnedAttemptUtc;
        private static bool _forcedDead;
        private static bool _accuracyInitialized;
        private static bool _multiplayerBattleActive;
        private static DateTime _battleStartedUtc;
        private static int _localBattleDifficulty;

        public static bool Synchronizing => _synchronizing;
        public static bool IsActiveMultiplayerBattle =>
            _multiplayerBattleActive || (LobbyManager.IsInLobby && LobbyManager.CurrentLobby?.IsPlaying == true);
        public static bool HasReportedBattleFinished => _finishReported;
        public static bool HasReportedFailedBattleFinished => _finishReported && _forcedDead;
        public static event Action<BattlePlayerEntry[]> BattleDataChanged;

        public static void Init()
        {
            PushDispatcher.Instance.Register<BattleDataPushMsg>(OpCodes.BattleDataPush, OnBattleDataPush);
            PushDispatcher.Instance.Register<BattleDataDeltaPushMsg>(OpCodes.BattleDataDeltaPush, OnBattleDataDeltaPush);
        }

        public static BattlePlayerEntry[] GetBattleDataSnapshot()
        {
            lock (BattleDataLock)
            {
                return CreateBattleDataSnapshotUnsafe();
            }
        }

        public static void PrepareForNewBattle()
        {
            MarkMultiplayerBattleStarting();
            _battleStartedUtc = DateTime.UtcNow;
            _activeBattleId = LobbyManager.CurrentLobby?.CurrentBattleId;
            _localBattleDifficulty = 0;
            _localBattleDifficulty = ResolveLocalBattleDifficulty();
            _lastBattleReturnedAttemptUtc = default;

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
            if (_finishReported)
            {
                return;
            }

            var now = DateTime.UtcNow;
            if (_lastBattleReturnedAttemptUtc != default &&
                now - _lastBattleReturnedAttemptUtc < TimeSpan.FromMilliseconds(BattleReturnedRetryIntervalMs))
            {
                return;
            }

            _lastBattleReturnedAttemptUtc = now;
            _forcedDead = !alive;
            if (alive)
            {
                StopSyncLoop();
            }

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
                _finishReported = true;
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
                        Difficulty = GetBattleDifficultyForPlayer(uid),
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
            _lastBattleReturnedAttemptUtc = default;
            _forcedDead = false;
            _activeBattleId = null;
            _taskStageTarget = null;
            _battleRoleAttributeComponent = null;
            _accuracyInitialized = false;
            _battleStartedUtc = default;
            _localBattleDifficulty = 0;

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
                await Task.Delay(GetBattleUpdateIntervalMs(), cancellationToken);
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
            if (!IsNetworkReady()) return;
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

                BattlePlayerEntry[] snapshot;
                lock (BattleDataLock)
                {
                    if (PlayerBattleData.TryGetValue(uid, out var entry) &&
                        IsSameEntry(
                            entry,
                            notify.Score,
                            notify.Accuracy,
                            notify.Perfects,
                            notify.Greats,
                            notify.Earlies,
                            notify.Lates,
                            notify.Misses,
                            notify.FC,
                            notify.Alive,
                            GetBattleDifficultyForPlayer(uid),
                            0))
                    {
                        return;
                    }

                    PlayerBattleData[uid] = new BattlePlayerEntry
                    {
                        Uid = uid,
                        Difficulty = GetBattleDifficultyForPlayer(uid),
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
                    snapshot = CreateBattleDataSnapshotUnsafe();
                }

                NotifyBattleDataChanged(snapshot);
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
                Difficulty = entry.Difficulty,
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
                    Accuracy = RoundBattleAccuracy(AccuracyManager.GetCalculatedAccuracy()),
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

        private static int GetBattleUpdateIntervalMs()
        {
            var playerCount = LobbyManager.CurrentLobby?.Players?.Length ?? 0;
            return playerCount >= LargeLobbyBattlePlayerThreshold
                ? LargeLobbyBattleUpdateIntervalMs
                : SmallLobbyBattleUpdateIntervalMs;
        }

        private static float RoundBattleAccuracy(float accuracy)
        {
            if (float.IsNaN(accuracy) || float.IsInfinity(accuracy)) return 0f;
            return (float)Math.Round(accuracy, 2);
        }

        private static void OnBattleDataPush(BattleDataPushMsg push)
        {
            ApplyPushedBattleData(push?.BattleId, push?.Players, replaceMissingRemote: true);
        }

        private static void OnBattleDataDeltaPush(BattleDataDeltaPushMsg push)
        {
            ApplyPushedBattleData(push?.BattleId, push?.Players, replaceMissingRemote: false);
        }

        private static void ApplyPushedBattleData(
            string pushBattleId,
            BattlePlayerEntry[] pushedPlayers,
            bool replaceMissingRemote)
        {
            var battleId = GetCurrentBattleId();
            if (string.IsNullOrWhiteSpace(battleId) || pushBattleId != battleId)
            {
                return;
            }

            var players = pushedPlayers ?? Array.Empty<BattlePlayerEntry>();
            var pushedUids = replaceMissingRemote ? new HashSet<string>() : null;
            var changed = false;
            BattlePlayerEntry[] snapshot;
            lock (BattleDataLock)
            {
                foreach (var player in players)
                {
                    if (player == null || string.IsNullOrWhiteSpace(player.Uid)) continue;
                    pushedUids?.Add(player.Uid);
                    var normalizedPlayer = NormalizePushedBattleEntry(player);
                    if (!PlayerBattleData.TryGetValue(player.Uid, out var entry))
                    {
                        PlayerBattleData[player.Uid] = normalizedPlayer;
                        changed = true;
                        continue;
                    }

                    if (SameBattleEntry(entry, normalizedPlayer)) continue;

                    PlayerBattleData[player.Uid] = normalizedPlayer;
                    changed = true;
                }

                if (replaceMissingRemote && pushedUids != null)
                {
                    var localUid = PlayerManager.CurrentUid;
                    var staleUids = new List<string>();
                    foreach (var uid in PlayerBattleData.Keys)
                    {
                        if (uid == localUid) continue;
                        if (!pushedUids.Contains(uid))
                        {
                            staleUids.Add(uid);
                        }
                    }

                    foreach (var uid in staleUids)
                    {
                        PlayerBattleData.Remove(uid);
                        changed = true;
                    }
                }

                if (!changed) return;
                snapshot = CreateBattleDataSnapshotUnsafe();
            }

            NotifyBattleDataChanged(snapshot);
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
                return _activeBattleId;
            }

            if (!string.IsNullOrWhiteSpace(_activeBattleId))
            {
                return _activeBattleId;
            }

            var currentBattleId = lobby.CurrentBattleId;
            if (!string.IsNullOrWhiteSpace(currentBattleId))
            {
                _activeBattleId = currentBattleId;
            }

            return _activeBattleId;
        }

        private static int ResolveLocalBattleDifficulty()
        {
            var difficulty = LobbyManager.GetCurrentBattleDifficulty(PlayerManager.CurrentUid);
            if (difficulty > 0) return difficulty;

            return ChartManager.CurrentDifficulty;
        }

        private static int GetBattleDifficultyForPlayer(string uid)
        {
            if (!string.IsNullOrWhiteSpace(uid) &&
                uid == PlayerManager.CurrentUid &&
                _localBattleDifficulty > 0)
            {
                return _localBattleDifficulty;
            }

            return LobbyManager.GetCurrentBattleDifficulty(uid);
        }

        private static BattlePlayerEntry NormalizePushedBattleEntry(BattlePlayerEntry entry)
        {
            if (entry == null) return null;
            if (entry.Uid != PlayerManager.CurrentUid || entry.Difficulty > 0) return entry;

            var difficulty = GetBattleDifficultyForPlayer(entry.Uid);
            if (difficulty <= 0) return entry;

            var clone = CloneBattleEntry(entry);
            clone.Difficulty = difficulty;
            return clone;
        }

        private static BattlePlayerEntry[] CreateBattleDataSnapshotUnsafe()
        {
            var result = new BattlePlayerEntry[PlayerBattleData.Count];
            PlayerBattleData.Values.CopyTo(result, 0);
            return result;
        }

        private static bool SameBattleEntry(BattlePlayerEntry left, BattlePlayerEntry right)
        {
            if (left == null || right == null) return left == right;

            return IsSameEntry(
                left,
                right.Score,
                right.Accuracy,
                right.Perfects,
                right.Greats,
                right.Earlies,
                right.Lates,
                right.Misses,
                right.FC,
                right.Alive,
                right.Difficulty,
                right.PingMS);
        }

        private static bool IsSameEntry(
            BattlePlayerEntry entry,
            uint score,
            float accuracy,
            ushort perfects,
            ushort greats,
            ushort earlies,
            ushort lates,
            ushort misses,
            bool fc,
            bool alive,
            int difficulty,
            ushort pingMs)
        {
            return entry.Score == score &&
                   entry.Difficulty == difficulty &&
                   Math.Abs(entry.Accuracy - accuracy) < 0.0001f &&
                   entry.Perfects == perfects &&
                   entry.Greats == greats &&
                   entry.Earlies == earlies &&
                   entry.Lates == lates &&
                   entry.Misses == misses &&
                   entry.FC == fc &&
                   entry.Alive == alive &&
                   entry.PingMS == pingMs;
        }

    }
}
