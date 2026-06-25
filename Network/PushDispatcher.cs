using System;
using System.Collections.Generic;
using System.Text.Json;
using MDEN.Protocol;
using MDEN.Threading;
using MelonLoader;

namespace MDEN.Network
{
    // 将服务端推送的通知分发到各个业务模块
    public class PushDispatcher
    {
        private static PushDispatcher _instance;
        public static PushDispatcher Instance => _instance ??= new PushDispatcher();

        private readonly Dictionary<ushort, Action<JsonElement>> _handlers = new Dictionary<ushort, Action<JsonElement>>();

        public void Register<T>(ushort opCode, Action<T> handler)
        {
            _handlers[opCode] = (element) =>
            {
                T msg;
                try
                {
                    msg = element.Deserialize<T>(ProtocolJson.Options);
                }
                catch (Exception ex)
                {
                    MelonLogger.Error($"Failed to parse push message {opCode}: {ex}");
                    return;
                }

                ClientThreadDispatcher.Enqueue(() => handler(msg));
            };
        }

        public void Dispatch(ushort opCode, JsonElement element)
        {
            if (_handlers.TryGetValue(opCode, out var handler))
            {
                handler(element);
            }
            else
            {
                MDEN.Managers.ClientLogManager.Warning($"Received unregistered push message, OpCode: {opCode}");
            }
        }

        public void Init()
        {
            NetworkClient.Instance.OnPushReceived -= Dispatch;
            NetworkClient.Instance.OnPushReceived += Dispatch;
        }
    }
}
