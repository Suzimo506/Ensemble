using System;
using MDEN.Managers;
using UnityEngine;
using UnityEngine.UI;

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
        private static TextAnchor _alignment;
        private static HorizontalWrapMode _horizontalOverflow;
        private static VerticalWrapMode _verticalOverflow;
        private static int _siblingIndex;
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
            target.anchorMin = new Vector2(1f, 1f);
            target.anchorMax = new Vector2(1f, 1f);
            target.pivot = new Vector2(1f, 1f);
            target.anchoredPosition = new Vector2(-28f, -34f);
            target.sizeDelta = new Vector2(360f, _sizeDelta.y);
            target.SetAsLastSibling();
            var text = target.GetComponent<Text>();
            if (text != null)
            {
                text.alignment = TextAnchor.MiddleRight;
                text.horizontalOverflow = HorizontalWrapMode.Overflow;
                text.verticalOverflow = VerticalWrapMode.Overflow;
                text.text = FormatStageDesignerText(text.text);
            }
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
            _target.SetSiblingIndex(_siblingIndex);
            var text = _target.GetComponent<Text>();
            if (text != null)
            {
                text.alignment = _alignment;
                text.horizontalOverflow = _horizontalOverflow;
                text.verticalOverflow = _verticalOverflow;
            }
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
            _siblingIndex = target.GetSiblingIndex();
            var text = target.GetComponent<Text>();
            if (text != null)
            {
                _alignment = text.alignment;
                _horizontalOverflow = text.horizontalOverflow;
                _verticalOverflow = text.verticalOverflow;
            }
            else
            {
                _alignment = TextAnchor.MiddleLeft;
                _horizontalOverflow = HorizontalWrapMode.Wrap;
                _verticalOverflow = VerticalWrapMode.Truncate;
            }
        }

        private static string FormatStageDesignerText(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return value;

            var normalized = value.Replace("\r\n", "\n").Replace('\r', '\n').Trim();
            if (!normalized.Contains("\n"))
            {
                var separatorIndex = normalized.IndexOfAny(new[] { '：', ':' });
                return separatorIndex >= 0 && separatorIndex < normalized.Length - 1
                    ? normalized.Substring(separatorIndex + 1).Trim()
                    : normalized;
            }

            var parts = normalized.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2) return normalized.Replace("\n", string.Empty);

            var designer = parts[1].Trim();
            for (var i = 2; i < parts.Length; i++)
            {
                designer += " " + parts[i].Trim();
            }

            return designer;
        }

        private static bool IsPreparationVisible()
        {
            var preparation = GameObject.Find("UI/Standerd/PnlPreparation");
            return preparation != null && preparation.activeInHierarchy;
        }
    }
}
