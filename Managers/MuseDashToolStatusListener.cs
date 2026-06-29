using System;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MDEN.UI.Core;

namespace MDEN.Managers
{
    internal static class MuseDashToolStatusListener
    {
        private const string PipeName = "MDEN-MuseDashTOOL-Status";
        private const string StatusScheme = "mden";
        private const string MissingChartDownloadCommand = "missing-chart-download";
        private static CancellationTokenSource _cts;

        internal static void Initialize()
        {
            if (_cts != null) return;

            _cts = new CancellationTokenSource();
            _ = Task.Run(() => ListenAsync(_cts.Token));
        }

        internal static void Shutdown()
        {
            var cts = _cts;
            _cts = null;

            if (cts == null) return;
            cts.Cancel();
        }

        private static async Task ListenAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    using var server = new NamedPipeServerStream(
                        PipeName,
                        PipeDirection.In,
                        1,
                        PipeTransmissionMode.Byte,
                        PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);

                    await server.WaitForConnectionAsync(ct).ConfigureAwait(false);
                    using var reader = new StreamReader(server, new UTF8Encoding(false));
                    var message = await reader.ReadToEndAsync().ConfigureAwait(false);
                    HandleMessage(message);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    ClientLogManager.Warning($"MuseDashTOOL status listener failed: {ex.Message}");
                    try
                    {
                        await Task.Delay(1000, ct).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                }
            }
        }

        private static void HandleMessage(string message)
        {
            if (string.IsNullOrWhiteSpace(message) ||
                !Uri.TryCreate(message, UriKind.Absolute, out var uri) ||
                !string.Equals(uri.Scheme, StatusScheme, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            var command = string.IsNullOrWhiteSpace(uri.Host)
                ? uri.AbsolutePath.Trim('/')
                : uri.Host;
            if (!string.Equals(command, MissingChartDownloadCommand, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            var status = NormalizeOptional(GetQueryValue(uri.Query, "status"));
            var title = NormalizeOptional(GetQueryValue(uri.Query, "title"));
            var reason = NormalizeOptional(GetQueryValue(uri.Query, "reason"));

            MainThreadDispatcher.Enqueue(() => ShowMissingChartDownloadToast(status, title, reason));
        }

        private static void ShowMissingChartDownloadToast(string status, string title, string reason)
        {
            var messageKey = status?.ToLowerInvariant() switch
            {
                "started" => "missing_chart.download_started",
                "completed" => "missing_chart.download_completed",
                "failed" => string.IsNullOrWhiteSpace(reason)
                    ? "missing_chart.download_failed"
                    : "missing_chart.download_failed_reason",
                _ => null
            };

            if (string.IsNullOrWhiteSpace(messageKey)) return;

            title = string.IsNullOrWhiteSpace(title) ? I18nManager.T("chart.unknown") : title;
            var toast = string.Equals(messageKey, "missing_chart.download_failed_reason", StringComparison.Ordinal)
                ? I18nManager.Tf(messageKey, title, reason)
                : I18nManager.Tf(messageKey, title);
            UiNotificationManager.RequestToast(toast);
        }

        private static string GetQueryValue(string query, string key)
        {
            if (string.IsNullOrWhiteSpace(query)) return null;

            var value = query[0] == '?' ? query.Substring(1) : query;
            foreach (var part in value.Split(new[] { '&' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var pair = part.Split(new[] { '=' }, 2);
                var name = Uri.UnescapeDataString(pair[0].Replace("+", " "));
                if (!string.Equals(name, key, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                return pair.Length > 1
                    ? Uri.UnescapeDataString(pair[1].Replace("+", " "))
                    : string.Empty;
            }

            return null;
        }

        private static string NormalizeOptional(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }
    }
}
