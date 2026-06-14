using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.Text.Json;
using System.Text.Json.Serialization;
using MelonLoader;
using MDEN.Protocol;
using MDEN.Protocol.Messages.System;
using MDEN.Protocol.Envelopes;

namespace MDEN.Network
{
    public class ApiServerEntry
    {
        [JsonPropertyName("id")]
        public string Id { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("address")]
        public string Address { get; set; }
    }

    public class ApiServerListResponse
    {
        [JsonPropertyName("servers")]
        public List<ApiServerEntry> Servers { get; set; }
    }

    public static class ServerListApiClient
    {
        private static readonly HttpClient _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(5)
        };
        private const string ApiUrl = "https://api.xmjjs.top/mpmd/serverlist.php";

        static ServerListApiClient()
        {
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "MuseDashEnsemble-Network/1.0");
        }

        public static async Task<List<ApiServerEntry>> FetchOfficialServersAsync()
        {
            try
            {
                var json = await _httpClient.GetStringAsync(ApiUrl);
                var response = JsonSerializer.Deserialize<ApiServerListResponse>(json);
                return response?.Servers ?? new List<ApiServerEntry>();
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"获取官方服务器节点列表超时或失败: {ex.Message}");
                return new List<ApiServerEntry>();
            }
        }

        public static async Task<ServerInfoResponse> PingServerAsync(string address)
        {
            try
            {
                var parts = address.Split(':');
                string host = parts[0];
                int port = 10423;
                if (parts.Length > 1) int.TryParse(parts[1], out port);

                using var client = new TcpClient();
                var connectTask = client.ConnectAsync(host, port);
                if (await Task.WhenAny(connectTask, Task.Delay(2000)) != connectTask)
                {
                    return null;
                }

                var stream = client.GetStream();
                var req = new ClientEnvelope { Op = OpCodes.ServerInfoReq, ReqId = 1, Payload = new ServerInfoRequest() };
                
                var bytes = JsonSerializer.SerializeToUtf8Bytes(req);
                var lengthBytes = BitConverter.GetBytes((uint)bytes.Length);
                await stream.WriteAsync(lengthBytes, 0, 4);
                await stream.WriteAsync(bytes, 0, bytes.Length);

                var headerBuf = new byte[4];
                int read = await stream.ReadAsync(headerBuf, 0, 4);
                if (read < 4) return null;

                uint len = BitConverter.ToUInt32(headerBuf, 0);
                var payloadBuf = new byte[len];
                read = await stream.ReadAsync(payloadBuf, 0, (int)len);
                if (read < len) return null;

                var respEnv = JsonSerializer.Deserialize<ServerEnvelope>(payloadBuf);
                if (respEnv != null && respEnv.Op == OpCodes.ServerInfoResp)
                {
                    return JsonSerializer.Deserialize<ServerInfoResponse>(((JsonElement)respEnv.Payload).GetRawText());
                }
            }
            catch
            {
                return null;
            }

            return null;
        }
    }
}
