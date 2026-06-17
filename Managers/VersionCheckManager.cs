using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Il2CppUI.Controls;
using MDEN.UI.Core;
using MelonLoader;

namespace MDEN.Managers
{
    public static class VersionCheckManager
    {
        private const string VersionCheckUrl = "https://api.xmjjs.top/mpmd/version.php";
        private const string OutdatedMessage = "当前Ensemble版本过低，请前往喵斯兔群聊更新最新模组";
        private static readonly HttpClient Http = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(10)
        };
        private static bool _checkStarted;
        private static bool _warningShown;

        public static void CheckOnce()
        {
            if (_checkStarted) return;

            _checkStarted = true;
            _ = CheckVersionAsync();
        }

        private static async Task CheckVersionAsync()
        {
            try
            {
                var response = await Http.GetStringAsync(VersionCheckUrl);
                using var document = JsonDocument.Parse(response);
                if (!document.RootElement.TryGetProperty("version", out var versionElement))
                {
                    _checkStarted = false;
                    return;
                }

                var currentVersion = GetCurrentVersion();
                var versionText = versionElement.GetString();
                if (!Version.TryParse(versionText, out var remoteVersion) || remoteVersion <= currentVersion)
                {
                    MelonLogger.Msg($"Version check: up to date ({currentVersion})");
                    return;
                }

                MelonLogger.Warning($"New Ensemble version available: {remoteVersion} (current: {currentVersion})");
                MainThreadDispatcher.Enqueue(ShowOutdatedWarning);
            }
            catch (Exception ex)
            {
                _checkStarted = false;
                MelonLogger.Warning($"Version check failed: {ex.Message}");
            }
        }

        private static void ShowOutdatedWarning()
        {
            if (_warningShown) return;

            _warningShown = true;
            CommonMessageBox.ShowConfrimAndCancel(
                OutdatedMessage,
                new Action(() => { }),
                new Action(() => { }));
        }

        private static Version GetCurrentVersion()
        {
            var versionText = MelonBase.FindMelon("MDEN", "MDEN Team")?.Info?.Version;
            return Version.TryParse(versionText, out var version)
                ? version
                : new Version(0, 0, 0);
        }
    }
}
