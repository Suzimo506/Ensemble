using System.Collections.Generic;
using System.Threading.Tasks;
using MDEN.Network;
using MDEN.Protocol.Messages.System;

namespace MDEN.Managers
{
    // 服务器节点业务管理器
    public static class ServerManager
    {
        private static readonly List<ApiServerEntry> FallbackOfficialServers = new List<ApiServerEntry>
        {
            new ApiServerEntry { Id = "fallback-shanghai", Name = "上海", Address = "md.atri.wor1d:10423" },
            new ApiServerEntry { Id = "fallback-shandong", Name = "山东", Address = "mdcn.xmjjs.top:50160" },
            new ApiServerEntry { Id = "fallback-hongkong", Name = "香港", Address = "mdhk.xmjjs.top:10423" },
            new ApiServerEntry { Id = "fallback-hubei", Name = "湖北", Address = "mdcn2.xmjjs.top:31498" }
        };

        private static List<ApiServerEntry> _officialServerDataCache;
        private static bool _hasFetchedNodes = false;

        public static bool IsUsingFallbackOfficialServers { get; private set; }

        // 获取官方节点数据列表
        public static async Task<List<ApiServerEntry>> GetOfficialServersAsync()
        {
            if (!_hasFetchedNodes)
            {
                var fetchedServers = await ServerListApiClient.FetchOfficialServersAsync();
                if (fetchedServers == null || fetchedServers.Count == 0)
                {
                    _officialServerDataCache = GetFallbackOfficialServers();
                    IsUsingFallbackOfficialServers = true;
                }
                else
                {
                    _officialServerDataCache = fetchedServers;
                    IsUsingFallbackOfficialServers = false;
                }

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
            IsUsingFallbackOfficialServers = false;
        }

        private static List<ApiServerEntry> GetFallbackOfficialServers()
        {
            return new List<ApiServerEntry>(FallbackOfficialServers);
        }
    }
}
