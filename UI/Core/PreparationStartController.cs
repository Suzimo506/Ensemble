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

            var enabled = !_busy && PlaylistManager.CanUsePreparationButton();
            _boundButton.enabled = enabled;
            if (_boundKeyBinding != null) _boundKeyBinding.enabled = enabled;
            if (_boundKeyIcon != null) _boundKeyIcon.SetActive(enabled);
            if (_boundText != null) _boundText.text = PlaylistManager.GetPreparationButtonText();
        }

        private static async void OnClick()
        {
            if (_busy) return;
            if (!LobbyManager.IsInLobby) return;

            _busy = true;
            Refresh();
            using var _ = WindowStackController.LockUI("Updating playlist...");

            try
            {
                var result = await PlaylistManager.ToggleCurrentChartAsync();
                MainThreadDispatcher.Enqueue(() => ShowText.ShowInfo(
                    result == PlaylistToggleResult.Added ? "成功加入歌曲列表" : "成功移除歌曲列表"));
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Toggle playlist chart failed: {ex.Message}");
                MainThreadDispatcher.Enqueue(() => ShowText.ShowInfo(ex.Message));
            }
            finally
            {
                _busy = false;
                MainThreadDispatcher.Enqueue(Refresh);
            }
        }
    }
}
