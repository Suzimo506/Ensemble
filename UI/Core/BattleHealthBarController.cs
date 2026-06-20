using UnityEngine;

namespace MDEN.UI.Core
{
    public static class BattleHealthBarController
    {
        public static void ApplyVisibility()
        {
            var healthBar = GameObject.Find("UI_2D/Standard/PnlBattle/PnlBattleUI/PnlBattleOthers/Below");
            if (healthBar == null)
            {
                var battleOthers = GameObject.Find("PnlBattleOthers");
                var below = battleOthers == null ? null : battleOthers.transform.Find("Below");
                healthBar = below == null ? null : below.gameObject;
            }

            if (healthBar != null)
            {
                healthBar.SetActive(!Managers.BattleManager.IsActiveMultiplayerBattle || !Managers.ModConfigManager.HideBattleHealthBar);
            }
        }
    }
}
