using System;
using System.Collections.Generic;
using MDEN.Managers;
using PeroInputManager = Il2CppAssets.Scripts.PeroTools.Managers.InputManager;

namespace MDEN.UI.Core
{
    internal static class NativeInputBlocker
    {
        private static readonly HashSet<string> Reasons = new();
        private static bool? _previousStopKeyAction;

        public static void SetBlocked(string reason, bool blocked)
        {
            if (string.IsNullOrWhiteSpace(reason)) return;

            if (blocked)
            {
                if (!Reasons.Add(reason)) return;
                ApplyBlocked();
                return;
            }

            if (!Reasons.Remove(reason)) return;
            if (Reasons.Count == 0)
            {
                RestorePreviousState();
            }
        }

        public static void Clear(string reason)
        {
            SetBlocked(reason, false);
        }

        public static void ClearAll()
        {
            Reasons.Clear();
            ForceUnblock();
        }

        private static void ApplyBlocked()
        {
            try
            {
                var manager = PeroInputManager.instance;
                if (manager == null) return;

                _previousStopKeyAction ??= manager.isStopKeyAction;
                manager.isStopKeyAction = true;
            }
            catch (Exception ex)
            {
                ClientLogManager.Warning($"Set native input block failed: {ex.Message}");
            }
        }

        private static void RestorePreviousState()
        {
            try
            {
                var manager = PeroInputManager.instance;
                if (manager != null)
                {
                    manager.isStopKeyAction = _previousStopKeyAction ?? false;
                }
            }
            catch (Exception ex)
            {
                ClientLogManager.Warning($"Restore native input block failed: {ex.Message}");
            }
            finally
            {
                _previousStopKeyAction = null;
            }
        }

        private static void ForceUnblock()
        {
            try
            {
                var manager = PeroInputManager.instance;
                if (manager != null)
                {
                    manager.isStopKeyAction = false;
                }
            }
            catch (Exception ex)
            {
                ClientLogManager.Warning($"Force clear native input block failed: {ex.Message}");
            }
            finally
            {
                _previousStopKeyAction = null;
            }
        }
    }
}
