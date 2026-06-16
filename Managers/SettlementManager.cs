using System;
using MDEN.Network;
using MDEN.Protocol;
using MDEN.Protocol.Messages.Battle;
using MDEN.UI.Core;

namespace MDEN.Managers
{
    public static class SettlementManager
    {
        public static event Action<SettlementResultPush> SettlementReceived;

        public static void Init()
        {
            PushDispatcher.Instance.Register<SettlementResultPush>(OpCodes.SettlementResultPush, OnSettlementResult);
        }

        private static void OnSettlementResult(SettlementResultPush push)
        {
            if (push == null) return;

            MainThreadDispatcher.Enqueue(() => SettlementReceived?.Invoke(push));
        }
    }
}
