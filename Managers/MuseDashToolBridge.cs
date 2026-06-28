using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Text;

namespace MDEN.Managers
{
    internal static class MuseDashToolBridge
    {
        private const string PipeName = "MuseDashTOOL-DeepLink";
        private const int ConnectTimeoutMs = 500;

        internal static bool OpenGlobalSearch(string chartName, string chartKey = null, int difficulty = 0)
        {
            if (string.IsNullOrWhiteSpace(chartName))
            {
                return false;
            }

            try
            {
                var uri = BuildGlobalSearchUri(chartName, chartKey, difficulty);
                using var client = new NamedPipeClientStream(".", PipeName, PipeDirection.Out);
                client.Connect(ConnectTimeoutMs);
                using var writer = new StreamWriter(client, new UTF8Encoding(false));
                writer.Write(uri);
                writer.Flush();
                return true;
            }
            catch (Exception ex)
            {
                ClientLogManager.Warning($"MuseDashTOOL deep link failed: {ex.Message}");
                return false;
            }
        }

        private static string BuildGlobalSearchUri(string chartName, string chartKey, int difficulty)
        {
            var parts = new List<string>
            {
                "query=" + Uri.EscapeDataString(chartName)
            };

            if (!string.IsNullOrWhiteSpace(chartKey))
            {
                parts.Add("chartKey=" + Uri.EscapeDataString(chartKey));
            }

            if (difficulty > 0)
            {
                parts.Add("difficulty=" + difficulty);
            }

            return "musedashtool://global-search?" + string.Join("&", parts);
        }
    }
}
