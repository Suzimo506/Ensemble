using MelonLoader;
using MDEN.Managers;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

namespace MDEN.UI.Core
{
    public static class CloudSyncIndicator
    {
        private static GameObject _message;
        private static GameObject _pending;
        private static GameObject _completed;
        private static GameObject _failed;
        private static Text _pendingText;
        private static CancellationTokenSource _hideCts;
        private static int _operationId;

        public static void Initialize()
        {
            if (_message != null) return;

            var source = GameObject.Find("UI/Standerd/PnlCloudMessage");
            if (source == null) return;

            _message = UnityEngine.Object.Instantiate(source, source.transform.parent);
            _message.name = "PnlCloudMessageMDEN";
            _message.SetActive(false);

            var imgBase = _message.transform.Find("ImgBase");
            if (imgBase == null) return;

            _pending = imgBase.Find("Synchronizing")?.gameObject;
            _completed = imgBase.Find("SynchronizingCompleted")?.gameObject;
            _failed = imgBase.Find("SynchronizingFail")?.gameObject;
            _pendingText = _pending?.transform.Find("TxtSynchronizing")?.GetComponent<Text>();

            RemoveComponent(_pending, "OnCustomEvent");
            RemoveComponent(_completed, "OnCustomEvent");
            RemoveComponent(_failed, "OnCustomEvent");

            var localization = _pendingText?.GetComponent<Il2CppAssets.Scripts.PeroTools.GeneralLocalization.Localization>();
            if (localization != null)
            {
                UnityEngine.Object.Destroy(localization);
            }
        }

        public static void Start(string text)
        {
            var operationId = Interlocked.Increment(ref _operationId);
            MainThreadDispatcher.Enqueue(() => StartOnMainThread(text, operationId));
        }

        public static void Finish(bool success)
        {
            var operationId = Volatile.Read(ref _operationId);
            MainThreadDispatcher.Enqueue(() => FinishOnMainThread(success, operationId));
        }

        private static void StartOnMainThread(string text, int operationId)
        {
            if (operationId != _operationId) return;

            Initialize();
            if (_message == null) return;

            if (_pendingText != null)
            {
                _pendingText.text = string.IsNullOrEmpty(text) ? "同步中..." : text;
            }

            CancelHide();
            SetActive(_completed, false);
            SetActive(_failed, false);
            SetActive(_pending, true);
            SetActive(_message, true);
        }

        private static void FinishOnMainThread(bool success, int operationId)
        {
            if (operationId != _operationId) return;
            if (_message == null || !_message.activeSelf) return;

            SetActive(_pending, false);
            SetActive(_completed, success);
            SetActive(_failed, !success);
            HideLaterAsync(operationId);
        }

        private static async void HideLaterAsync(int operationId)
        {
            CancelHide();
            _hideCts = new CancellationTokenSource();
            var token = _hideCts.Token;

            try
            {
                await Task.Delay(1800, token);
                MainThreadDispatcher.Enqueue(() =>
                {
                    if (operationId != _operationId) return;
                    if (token.IsCancellationRequested || _message == null) return;
                    SetActive(_message, false);
                    SetActive(_pending, false);
                    SetActive(_completed, false);
                    SetActive(_failed, false);
                });
            }
            catch (TaskCanceledException)
            {
            }
        }

        private static void CancelHide()
        {
            try
            {
                _hideCts?.Cancel();
                _hideCts?.Dispose();
            }
            catch
            {
            }

            _hideCts = null;
        }

        private static void SetActive(GameObject target, bool active)
        {
            if (target != null)
            {
                target.SetActive(active);
            }
        }

        private static void RemoveComponent(GameObject target, string typeName)
        {
            if (target == null) return;

            var component = target.GetComponent(typeName);
            if (component == null) return;

            try
            {
                UnityEngine.Object.Destroy(component);
            }
            catch (System.Exception ex)
            {
                MelonLogger.Warning($"Failed to remove {typeName}: {ex.Message}");
            }
        }
    }
}
