using System.Collections.Generic;
using System.Linq;
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
            new ApiServerEntry { Id = "fallback-chengdu", Name = "成都", Address = "42.193.20.197:10423" },
            new ApiServerEntry { Id = "fallback-shanghai", Name = "上海", Address = "mdsh.mden.top:10423" },
            new ApiServerEntry { Id = "fallback-hongkong", Name = "香港", Address = "mdhk.mden.top:10423" },
            new ApiServerEntry { Id = "fallback-hubei", Name = "湖北", Address = "mdhb.mden.top:31498" },
            new ApiServerEntry { Id = "fallback-jiangsu", Name = "江苏", Address = "mdjs.mden.top:50160" }
        };
        private static readonly List<ApiServerEntry> PinnedOfficialServers = new List<ApiServerEntry>
        {
            new ApiServerEntry { Id = "pinned-chengdu", Name = "成都", Address = "42.193.20.197:10423" }
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
                    _officialServerDataCache = MergeWithPinnedOfficialServers(fetchedServers);
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

        private static List<ApiServerEntry> MergeWithPinnedOfficialServers(List<ApiServerEntry> servers)
        {
            var merged = servers == null ? new List<ApiServerEntry>() : new List<ApiServerEntry>(servers);
            var addresses = new HashSet<string>(
                merged
                    .Where(server => !string.IsNullOrWhiteSpace(server?.Address))
                    .Select(server => server.Address),
                System.StringComparer.OrdinalIgnoreCase);

            foreach (var pinnedServer in PinnedOfficialServers)
            {
                if (addresses.Add(pinnedServer.Address))
                {
                    merged.Add(pinnedServer);
                }
            }

            return merged;
        }
    }
}
