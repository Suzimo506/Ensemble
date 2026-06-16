using MDEN.Managers;
using UnityEngine;

namespace MDEN.UI.Core
{
    internal static class StageDesignerTextController
    {
        private const string StageDesignerPath = "UI/Standerd/PnlPreparation/TxtStageDesigner";
        private static RectTransform _target;
        private static Vector2 _anchorMin;
        private static Vector2 _anchorMax;
        private static Vector2 _pivot;
        private static Vector2 _anchoredPosition;
        private static Vector2 _sizeDelta;
        private static bool _moved;

        public static void Update()
        {
            var shouldMove = LobbyManager.IsInLobby && IsPreparationVisible();
            if (!shouldMove)
            {
                Restore();
                return;
            }

            var target = GetTarget();
            if (target == null) return;

            CaptureOriginal(target);
            target.anchorMin = new Vector2(0f, 0f);
            target.anchorMax = new Vector2(0f, 0f);
            target.pivot = new Vector2(0f, 0f);
            target.anchoredPosition = new Vector2(36f, 42f);
            target.sizeDelta = new Vector2(640f, _sizeDelta.y);
            _moved = true;
        }

        public static void Restore()
        {
            if (!_moved || _target == null) return;

            _target.anchorMin = _anchorMin;
            _target.anchorMax = _anchorMax;
            _target.pivot = _pivot;
            _target.anchoredPosition = _anchoredPosition;
            _target.sizeDelta = _sizeDelta;
            _moved = false;
        }

        private static RectTransform GetTarget()
        {
            if (_target != null) return _target;

            var obj = GameObject.Find(StageDesignerPath);
            _target = obj == null ? null : obj.GetComponent<RectTransform>();
            return _target;
        }

        private static void CaptureOriginal(RectTransform target)
        {
            if (_moved) return;

            _target = target;
            _anchorMin = target.anchorMin;
            _anchorMax = target.anchorMax;
            _pivot = target.pivot;
            _anchoredPosition = target.anchoredPosition;
            _sizeDelta = target.sizeDelta;
        }

        private static bool IsPreparationVisible()
        {
            var preparation = GameObject.Find("UI/Standerd/PnlPreparation");
            return preparation != null && preparation.activeInHierarchy;
        }
    }
}
