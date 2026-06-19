using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Il2CppUI.Controls;
using MDEN.UI.Core;
using MelonLoader;
using UnityEngine;

namespace MDEN.Managers
{
    public static class VersionCheckManager
    {
        private const string VersionCheckUrl = "https://api.xmjjs.top/mpmd/version.php";
        private const string OutdatedMessage = "当前Ensemble版本过低，请前往喵斯兔群聊更新最新模组\n点击确认自动跳转至更新网站\n<color=#ff3333>不更新无法进行游戏</color>";
        private const bool ForceShowUpdateDialogForTest = false;
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
                if (!Version.TryParse(versionText, out var remoteVersion))
                {
                    _checkStarted = false;
                    return;
                }

                if (!ForceShowUpdateDialogForTest && remoteVersion <= currentVersion)
                {
                    MDEN.Managers.ClientLogManager.Msg($"Version check: up to date ({currentVersion})");
                    return;
                }

                var downloadUrl = GetDownloadUrl(document.RootElement);
                MDEN.Managers.ClientLogManager.Warning($"New Ensemble version available: {remoteVersion} (current: {currentVersion})");
                MainThreadDispatcher.Enqueue(() => ShowOutdatedWarning(downloadUrl));
            }
            catch (Exception ex)
            {
                _checkStarted = false;
                MDEN.Managers.ClientLogManager.Warning($"Version check failed: {ex.Message}");
            }
        }

        private static void ShowOutdatedWarning(string downloadUrl)
        {
            if (_warningShown) return;

            _warningShown = true;
            CommonMessageBox.ShowConfrimAndCancel(
                OutdatedMessage,
                new Action(() => OpenDownloadAndQuit(downloadUrl)),
                new Action(QuitGame));
        }

        private static string GetDownloadUrl(JsonElement root)
        {
            if (TryGetString(root, "downloadUrl", out var value) ||
                TryGetString(root, "downloadurl", out value) ||
                TryGetString(root, "download_url", out value))
            {
                return value;
            }

            return null;
        }

        private static bool TryGetString(JsonElement root, string propertyName, out string value)
        {
            value = null;
            if (!root.TryGetProperty(propertyName, out var element) ||
                element.ValueKind != JsonValueKind.String)
            {
                return false;
            }

            value = element.GetString();
            return !string.IsNullOrWhiteSpace(value);
        }

        private static void OpenDownloadAndQuit(string downloadUrl)
        {
            if (!string.IsNullOrWhiteSpace(downloadUrl))
            {
                Application.OpenURL(downloadUrl);
            }

            Application.Quit();
        }

        private static void QuitGame()
        {
            Application.Quit();
        }

        private static Version GetCurrentVersion()
        {
            var versionText = MelonBase.FindMelon("Ensemble", "MDENTeam")?.Info?.Version;
            return Version.TryParse(versionText, out var version)
                ? version
                : new Version(0, 0, 0);
        }
    }
}
