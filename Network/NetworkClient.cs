using System;
using System.Collections.Concurrent;
using System.Net.Sockets;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MDEN.Protocol;
using MDEN.Protocol.Envelopes;
using MDEN.Protocol.Messages.System;
using MelonLoader;

namespace MDEN.Network
{
    public class NetworkClient
    {
        private const int ConnectTimeoutMs = 8000;
        private const int SendTimeoutMs = 8000;
        private static NetworkClient _instance;
        public static NetworkClient Instance => _instance ??= new NetworkClient();

        private readonly PacketFramer _framer = new PacketFramer();
        private readonly ConcurrentDictionary<uint, PendingRequest> _pendingRequests = new ConcurrentDictionary<uint, PendingRequest>();
        private readonly SemaphoreSlim _sendSemaphore = new SemaphoreSlim(1, 1);
        private TcpClient _tcpClient;
        private NetworkStream _stream;
        private bool _isConnected;
        private uint _nextReqId;
        private CancellationTokenSource _heartbeatCts;
        private int _connectionId;
        private DateTime _lastPingSentUtc;

        public bool IsConnected => _isConnected;

        public event Action<ushort, JsonElement> OnPushReceived;
        public event Action OnDisconnected;

        public async Task<bool> ConnectAsync(string host, int port)
        {
            try
            {
                Disconnect(false);

                _tcpClient = new TcpClient();
                var connectTask = _tcpClient.ConnectAsync(host, port);
                var timeoutTask = Task.Delay(ConnectTimeoutMs);
                if (await Task.WhenAny(connectTask, timeoutTask) != connectTask)
                {
                    throw new TimeoutException("Connect timed out.");
                }

                await connectTask;
                _stream = _tcpClient.GetStream();
                _isConnected = true;
                var connectionId = unchecked(++_connectionId);
                _ = ReceiveLoopAsync(connectionId, _stream);
                StartHeartbeat();
                return true;
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Failed to connect to server: {ex.Message}");
                CleanupConnection();
                return false;
            }
        }

        public void Disconnect()
        {
            Disconnect(false);
        }

        public void Disconnect(bool notifyDisconnected)
        {
            if (!_isConnected && _stream == null && _tcpClient == null) return;

            CleanupConnection();

            foreach (var pending in _pendingRequests.Values)
            {
                pending.TrySetException(new InvalidOperationException("Connection closed."));
            }
            _pendingRequests.Clear();

            if (notifyDisconnected)
            {
                OnDisconnected?.Invoke();
            }
        }

        public async Task<TResp> SendRequestAsync<TReq, TResp>(ushort opCode, TReq request, int timeoutMs = 15000)
        {
            if (!_isConnected || _stream == null)
            {
                throw new InvalidOperationException("Not connected to server.");
            }

            var reqId = unchecked(++_nextReqId);
            if (reqId == 0) reqId = unchecked(++_nextReqId);

            var pending = new PendingRequest(typeof(TResp));
            if (!_pendingRequests.TryAdd(reqId, pending))
            {
                throw new InvalidOperationException("Request id collision.");
            }

            using var timeout = new CancellationTokenSource(timeoutMs);
            using var registration = timeout.Token.Register(() =>
            {
                if (_pendingRequests.TryRemove(reqId, out var removed))
                {
                    removed.TrySetException(new TimeoutException("Request timed out."));
                }
            });

            try
            {
                await SendAsync(new ClientEnvelope
                {
                    Op = opCode,
                    ReqId = reqId,
                    Payload = request
                });

                var result = await pending.Task;
                return result == null ? default : (TResp)result;
            }
            finally
            {
                _pendingRequests.TryRemove(reqId, out _);
            }
        }

        public async Task SendNotifyAsync<T>(ushort opCode, T message)
        {
            await SendAsync(new ClientEnvelope
            {
                Op = opCode,
                Payload = message
            });
        }

        public async Task SendAsync(ClientEnvelope envelope)
        {
            if (!_isConnected || _stream == null) return;

            var connectionId = _connectionId;
            var stream = _stream;
            if (!await _sendSemaphore.WaitAsync(SendTimeoutMs))
            {
                DisconnectIfCurrent(connectionId, true);
                throw new TimeoutException("Timed out waiting for send lock.");
            }

            try
            {
                if (!IsCurrentConnection(connectionId) || !_isConnected || stream == null) return;

                var bytes = _framer.Encode(envelope);
                var writeTask = stream.WriteAsync(bytes, 0, bytes.Length);
                if (await Task.WhenAny(writeTask, Task.Delay(SendTimeoutMs)) != writeTask)
                {
                    throw new TimeoutException("Send timed out.");
                }

                await writeTask;

                if (!IsCurrentConnection(connectionId) || !_isConnected) return;

                var flushTask = stream.FlushAsync();
                if (await Task.WhenAny(flushTask, Task.Delay(SendTimeoutMs)) != flushTask)
                {
                    throw new TimeoutException("Flush timed out.");
                }

                await flushTask;
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Failed to send data: {ex.Message}");
                DisconnectIfCurrent(connectionId, true);
                throw;
            }
            finally
            {
                _sendSemaphore.Release();
            }
        }

        private async Task ReceiveLoopAsync(int connectionId, NetworkStream stream)
        {
            var buffer = new byte[4096];
            var receiveFramer = new PacketFramer();
            try
            {
                while (IsCurrentConnection(connectionId) && _isConnected && stream != null)
                {
                    int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
                    if (!IsCurrentConnection(connectionId))
                    {
                        break;
                    }

                    if (bytesRead == 0)
                    {
                        MelonLogger.Msg("Server disconnected.");
                        DisconnectIfCurrent(connectionId, true);
                        break;
                    }

                    receiveFramer.AppendData(buffer, 0, bytesRead);
                    while (receiveFramer.TryDecode(out ServerEnvelope envelope))
                    {
                        if (!IsCurrentConnection(connectionId))
                        {
                            break;
                        }

                        if (envelope.ReqId == null)
                        {
                            if (envelope.Op == OpCodes.Pong)
                            {
                                _ = ReportPingAsync();
                                continue;
                            }

                            if (envelope.Payload is JsonElement pushPayload)
                            {
                                DispatchPush(envelope.Op, pushPayload);
                            }
                            continue;
                        }

                        HandleResponse(envelope);
                    }
                }
            }
            catch (Exception ex)
            {
                if (IsCurrentConnection(connectionId) && _isConnected)
                {
                    MelonLogger.Error($"Receive loop exception: {ex.Message}");
                    DisconnectIfCurrent(connectionId, true);
                }
            }
        }

        private bool IsCurrentConnection(int connectionId)
        {
            return connectionId == _connectionId;
        }

        private void DispatchPush(ushort opCode, JsonElement payload)
        {
            try
            {
                OnPushReceived?.Invoke(opCode, payload);
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Push handler exception, OpCode: {opCode}, error: {ex}");
            }
        }

        private void DisconnectIfCurrent(int connectionId, bool notifyDisconnected)
        {
            if (!IsCurrentConnection(connectionId)) return;

            Disconnect(notifyDisconnected);
        }

        private void CleanupConnection()
        {
            unchecked { _connectionId++; }
            _isConnected = false;
            _lastPingSentUtc = default;
            StopHeartbeat();
            try { _stream?.Close(); } catch { }
            try { _tcpClient?.Close(); } catch { }
            _stream = null;
            _tcpClient = null;
        }

        private void StartHeartbeat()
        {
            StopHeartbeat();
            _heartbeatCts = new CancellationTokenSource();
            _ = HeartbeatLoopAsync(_heartbeatCts.Token);
        }

        private void StopHeartbeat()
        {
            try { _heartbeatCts?.Cancel(); } catch { }
            try { _heartbeatCts?.Dispose(); } catch { }
            _heartbeatCts = null;
        }

        private async Task HeartbeatLoopAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(15000, cancellationToken);
                    if (!_isConnected || _stream == null) continue;

                    _lastPingSentUtc = DateTime.UtcNow;
                    await SendAsync(new ClientEnvelope { Op = OpCodes.Ping });
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    MelonLogger.Warning($"Heartbeat failed: {ex.Message}");
                }
            }
        }

        private async Task ReportPingAsync()
        {
            if (_lastPingSentUtc == default || !_isConnected || _stream == null) return;

            var elapsedMs = (DateTime.UtcNow - _lastPingSentUtc).TotalMilliseconds;
            var ping = (ushort)Math.Max(0, Math.Min(ushort.MaxValue, (int)Math.Round(elapsedMs)));

            try
            {
                await SendNotifyAsync(OpCodes.PingReportNotify, new PingReportNotify { PingMS = ping });
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Report ping failed: {ex.Message}");
            }
        }

        private void HandleResponse(ServerEnvelope envelope)
        {
            if (!envelope.ReqId.HasValue) return;
            if (!_pendingRequests.TryRemove(envelope.ReqId.Value, out var pending)) return;

            if (!envelope.Success)
            {
                pending.TrySetException(new ProtocolException(ReadReason(envelope.Payload)));
                return;
            }

            try
            {
                if (pending.ResponseType == typeof(object) || envelope.Payload == null)
                {
                    pending.TrySetResult(null);
                    return;
                }

                if (envelope.Payload is JsonElement element)
                {
                    var result = element.Deserialize(pending.ResponseType, ProtocolJson.Options);
                    pending.TrySetResult(result);
                    return;
                }

                pending.TrySetResult(envelope.Payload);
            }
            catch (Exception ex)
            {
                pending.TrySetException(ex);
            }
        }

        private static string ReadReason(object payload)
        {
            if (payload is JsonElement element && element.TryGetProperty("Reason", out var reason))
            {
                return reason.GetString() ?? "Request failed.";
            }

            return "Request failed.";
        }

        private class PendingRequest
        {
            private readonly TaskCompletionSource<object> _tcs = new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);

            public PendingRequest(Type responseType)
            {
                ResponseType = responseType;
            }

            public Type ResponseType { get; }
            public Task<object> Task => _tcs.Task;
            public void TrySetResult(object result) => _tcs.TrySetResult(result);
            public void TrySetException(Exception ex) => _tcs.TrySetException(ex);
        }
    }
}
