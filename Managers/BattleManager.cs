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
        private static readonly Dictionary<string, BattlePlayerEntry> PlayerBattleData = new();
        private static CancellationTokenSource _syncCts;
        private static TaskStageTarget _taskStageTarget;
        private static BattleRoleAttributeComponent _battleRoleAttributeComponent;
        private static bool _synchronizing;
        private static bool _finishReported;
        private static bool _forcedDead;

        public static bool Synchronizing => _synchronizing;
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
            lock (BattleDataLock)
            {
                PlayerBattleData.Clear();
            }

            NotifyBattleDataChanged(Array.Empty<BattlePlayerEntry>());
        }

        public static async Task SyncStartAsync()
        {
            if (_synchronizing || !LobbyManager.IsInLobby) return;

            _taskStageTarget = TaskStageTarget.instance;
            _battleRoleAttributeComponent = BattleRoleAttributeComponent.instance;
            AccuracyManager.Init();

            _finishReported = false;
            _forcedDead = false;
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
                MelonLogger.Warning($"Battle sync failed: {ex.Message}");
            }
            finally
            {
                _synchronizing = false;
                if (LobbyManager.IsInLobby)
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

            await FlushFinalBattleDataAsync();

            if (!IsNetworkReady()) return;

            try
            {
                await NetworkClient.Instance.SendRequestAsync<BattleReturnedReq, BattleReturnedResp>(
                    OpCodes.BattleReturnedReq,
                    new BattleReturnedReq());
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Battle returned request failed: {ex.Message}");
            }
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

        public static void Reset()
        {
            StopSyncLoop();
            _finishReported = false;
            _forcedDead = false;
            _taskStageTarget = null;
            _battleRoleAttributeComponent = null;

            lock (BattleDataLock)
            {
                PlayerBattleData.Clear();
            }

            NotifyBattleDataChanged(Array.Empty<BattlePlayerEntry>());
        }

        private static async Task SyncLoopAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested && LobbyManager.IsInLobby)
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
            if (!IsNetworkReady() || _taskStageTarget == null) return;

            try
            {
                var notify = await CreateCurrentNotifyAsync();
                if (notify == null) return;

                await NetworkClient.Instance.SendNotifyAsync(
                    OpCodes.BattleDataNotify,
                    notify);
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Battle data notify failed: {ex.Message}");
            }
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
            var alive = !_forcedDead && (_battleRoleAttributeComponent == null || !_battleRoleAttributeComponent.IsDead());

            return new BattleDataNotifyMsg
            {
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

        private static void OnBattleDataPush(BattleDataPushMsg push)
        {
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
            MainThreadDispatcher.Enqueue(() => BattleDataChanged?.Invoke(players));
        }

        private static bool IsNetworkReady()
        {
            return LobbyManager.IsInLobby &&
                   ConnectionManager.IsLoggedIn &&
                   NetworkClient.Instance.IsConnected;
        }
    }
}
