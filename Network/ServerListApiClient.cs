using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Sockets;
using System.Threading;
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
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "MDEN-Network/1.0");
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
                MelonLogger.Warning($"Fetch official server list failed or timed out: {ex.Message}");
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
                var framer = new PacketFramer();
                var bytes = framer.Encode(req);
                await stream.WriteAsync(bytes, 0, bytes.Length);

                var headerBuf = new byte[4];
                if (await ReadExactAsync(stream, headerBuf, 4) < 4) return null;

                uint len = BitConverter.ToUInt32(headerBuf, 0);
                var payloadBuf = new byte[len];
                if (await ReadExactAsync(stream, payloadBuf, (int)len) < len) return null;

                var respEnv = JsonSerializer.Deserialize<ServerEnvelope>(payloadBuf, ProtocolJson.Options);
                if (respEnv != null && respEnv.Op == OpCodes.ServerInfoResp && respEnv.Success)
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

        private static async Task<int> ReadExactAsync(NetworkStream stream, byte[] buffer, int count)
        {
            int totalRead = 0;
            using var timeout = new CancellationTokenSource(2000);
            while (totalRead < count)
            {
                int read = await stream.ReadAsync(buffer, totalRead, count - totalRead, timeout.Token);
                if (read == 0) break;
                totalRead += read;
            }

            return totalRead;
        }
    }
}
