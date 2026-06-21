using UnityEngine;

namespace MDEN.UI.Core
{
    public static class BattleHealthBarController
    {
        private static GameObject _healthBar;
        private static CanvasGroup _canvasGroup;
        private static bool _createdCanvasGroup;
        private static bool _savedCanvasGroupState;
        private static float _savedAlpha;
        private static bool _savedInteractable;
        private static bool _savedBlocksRaycasts;
        private static bool _savedIgnoreParentGroups;

        public static void Reset()
        {
            _healthBar = null;
            _canvasGroup = null;
            _createdCanvasGroup = false;
            _savedCanvasGroupState = false;
        }

        public static void ApplyVisibility()
        {
            var healthBar = FindHealthBar();
            if (healthBar == null) return;

            var shouldHide = Managers.BattleManager.IsActiveMultiplayerBattle &&
                             Managers.ModConfigManager.HideBattleHealthBar;
            if (shouldHide)
            {
                HideWithoutDisabling(healthBar);
                return;
            }

            RestoreVisibility(healthBar);
        }

        private static GameObject FindHealthBar()
        {
            if (_healthBar != null) return _healthBar;

            _healthBar = GameObject.Find("UI_2D/Standard/PnlBattle/PnlBattleUI/PnlBattleOthers/Below");
            if (_healthBar != null) return _healthBar;

            var battleOthers = GameObject.Find("PnlBattleOthers");
            var below = battleOthers == null ? null : battleOthers.transform.Find("Below");
            _healthBar = below == null ? null : below.gameObject;
            return _healthBar;
        }

        private static void HideWithoutDisabling(GameObject healthBar)
        {
            if (!healthBar.activeSelf)
            {
                healthBar.SetActive(true);
            }

            var group = GetCanvasGroup(healthBar);
            if (group == null) return;

            SaveCanvasGroupState(group);
            group.alpha = 0f;
            group.interactable = false;
            group.blocksRaycasts = false;
            group.ignoreParentGroups = true;
        }

        private static void RestoreVisibility(GameObject healthBar)
        {
            if (!healthBar.activeSelf)
            {
                healthBar.SetActive(true);
            }

            var group = GetCanvasGroup(healthBar);
            if (group == null) return;

            RestoreCanvasGroupState(group);
        }

        private static CanvasGroup GetCanvasGroup(GameObject healthBar)
        {
            if (_canvasGroup != null && _canvasGroup.gameObject == healthBar) return _canvasGroup;
            _canvasGroup = healthBar.GetComponent<CanvasGroup>();
            _createdCanvasGroup = _canvasGroup == null;
            if (_createdCanvasGroup)
            {
                _canvasGroup = healthBar.AddComponent<CanvasGroup>();
            }

            _savedCanvasGroupState = false;
            return _canvasGroup;
        }

        private static void SaveCanvasGroupState(CanvasGroup group)
        {
            if (_savedCanvasGroupState) return;

            _savedAlpha = group.alpha;
            _savedInteractable = group.interactable;
            _savedBlocksRaycasts = group.blocksRaycasts;
            _savedIgnoreParentGroups = group.ignoreParentGroups;
            _savedCanvasGroupState = true;
        }

        private static void RestoreCanvasGroupState(CanvasGroup group)
        {
            if (_createdCanvasGroup)
            {
                UnityEngine.Object.Destroy(group);
                _canvasGroup = null;
                _createdCanvasGroup = false;
                _savedCanvasGroupState = false;
                return;
            }

            if (!_savedCanvasGroupState) return;

            group.alpha = _savedAlpha;
            group.interactable = _savedInteractable;
            group.blocksRaycasts = _savedBlocksRaycasts;
            group.ignoreParentGroups = _savedIgnoreParentGroups;
            _savedCanvasGroupState = false;
        }
    }
}
