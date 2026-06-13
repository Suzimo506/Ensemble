using System.Collections.Generic;
using System.Threading.Tasks;
using MDEN.Network;
using MDEN.Protocol.Messages.System;

namespace MDEN.Managers
{
    // 服务器节点业务管理器
    public static class ServerManager
    {
        private static List<ApiServerEntry> _officialServerDataCache;
        private static bool _hasFetchedNodes = false;

        // 获取官方节点数据列表
        public static async Task<List<ApiServerEntry>> GetOfficialServersAsync()
        {
            if (!_hasFetchedNodes)
            {
                _officialServerDataCache = await ServerListApiClient.FetchOfficialServersAsync();
                _hasFetchedNodes = true;
            }
            return _officialServerDataCache ?? new List<ApiServerEntry>();
        }

        // 测试与官方节点的延迟
        public static async Task<ServerInfoResponse> PingServerAsync(string address)
        {
            return await ServerListApiClient.PingServerAsync(address);
        }

        // 刷新缓存重新获取节点
        public static void InvalidateCache()
        {
            _hasFetchedNodes = false;
        }
    }
}
