using System;
using Il2CppAssets.Scripts.PeroTools.UI;
using Il2CppAssets.Scripts.UI.Controls;
using MDEN.Managers;
using MelonLoader;
using UnityEngine;
using UnityEngine.UI;

namespace MDEN.UI.Core
{
    public static class PreparationStartController
    {
        private static Button _boundButton;
        private static InputKeyBinding _boundKeyBinding;
        private static Text _boundText;
        private static GameObject _boundKeyIcon;
        private static bool _busy;

        public static void BindOrRefresh()
        {
            var buttonObj = GameObject.Find("UI/Standerd/PnlPreparation/Start/BtnStart");
            if (buttonObj == null) return;

            var button = buttonObj.GetComponent<Button>();
            if (button == null) return;

            if (_boundButton != button)
            {
                _boundButton = button;
                _boundKeyBinding = buttonObj.GetComponent<InputKeyBinding>();
                _boundText = buttonObj.transform.Find("TxtStart")?.GetComponent<Text>();
                var keyIcon = buttonObj.transform.Find("TxtStart/ImgBtnA");
                _boundKeyIcon = keyIcon == null ? null : keyIcon.gameObject;
            }

            Refresh();
        }

        public static bool ShouldBlockNativeBattleStart()
        {
            if (!LobbyManager.IsInLobby) return false;
            if (RoomHudController.IsChatConsumingInput) return true;

            OnClick();
            return true;
        }

        public static void Refresh()
        {
            if (_boundButton == null) return;
            if (!LobbyManager.IsInLobby)
            {
                _boundButton.enabled = true;
                if (_boundKeyBinding != null) _boundKeyBinding.enabled = true;
                if (_boundKeyIcon != null) _boundKeyIcon.SetActive(true);
                if (_boundText != null) _boundText.text = "PLAY!";
                return;
            }

            var enabled = !_busy && (PlaylistManager.CanUsePreparationButton() || PlaylistManager.IsCurrentChartUnsupported());
            _boundButton.enabled = enabled;
            if (_boundKeyBinding != null) _boundKeyBinding.enabled = enabled;
            if (_boundKeyIcon != null) _boundKeyIcon.SetActive(enabled);
            if (_boundText != null) _boundText.text = PlaylistManager.GetPreparationButtonText();
        }

        private static async void OnClick()
        {
            if (_busy) return;
            if (!LobbyManager.IsInLobby) return;

            if (PlaylistManager.IsCurrentChartUnsupported())
            {
                ShowText.ShowInfo(PlaylistManager.GetPreparationButtonText());
                return;
            }

            if (PlaylistManager.IsRookieReadySelectionActive())
            {
                await SetRookieReadyAsync();
                return;
            }

            _busy = true;
            Refresh();
            IDisposable uiLock = WindowStackController.LockUI("Updating playlist...");

            try
            {
                var result = await PlaylistManager.ToggleCurrentChartAsync();
                MainThreadDispatcher.Enqueue(() => ShowText.ShowInfo(
                    result == PlaylistToggleResult.Added ? "成功加入歌曲列表" : "成功移除歌曲列表"));
            }
            catch (Exception ex)
            {
                MDEN.Managers.ClientLogManager.Warning($"Toggle playlist chart failed: {ex.Message}");
                MainThreadDispatcher.Enqueue(() => ShowText.ShowInfo(ex.Message));
            }
            finally
            {
                _busy = false;
                MainThreadDispatcher.Enqueue(Refresh);
                await MainThreadDispatcher.InvokeAsync(() => uiLock?.Dispose());
            }
        }

        private static async System.Threading.Tasks.Task SetRookieReadyAsync()
        {
            var entry = PlaylistManager.GetCurrentPlaylistEntry();
            if (!ChartManager.IsCurrentSelectedChart(entry))
            {
                ShowText.ShowInfo("请先选择本局谱面");
                return;
            }

            var difficulty = ChartManager.CurrentDifficulty;
            if (!MDEN.Protocol.Rules.DifficultyDisplayRules.IsKnownDifficulty(difficulty))
            {
                ShowText.ShowInfo("请选择有效难度");
                return;
            }

            _busy = true;
            Refresh();
            IDisposable uiLock = WindowStackController.LockUI("Setting ready...");

            try
            {
                await PlaylistManager.SetReadyAsync(true, difficulty);
                MainThreadDispatcher.Enqueue(() => ShowText.ShowInfo("已选择并准备"));
            }
            catch (Exception ex)
            {
                MDEN.Managers.ClientLogManager.Warning($"Set rookie ready failed: {ex.Message}");
                MainThreadDispatcher.Enqueue(() => ShowText.ShowInfo($"准备失败：{ex.Message}"));
            }
            finally
            {
                _busy = false;
                MainThreadDispatcher.Enqueue(Refresh);
                await MainThreadDispatcher.InvokeAsync(() => uiLock?.Dispose());
            }
        }
    }
}
