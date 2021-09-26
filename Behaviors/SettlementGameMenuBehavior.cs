using System.Collections.Generic;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.SandBox.CampaignBehaviors;
using TaleWorlds.Localization;

namespace SurrenderTweaks.Behaviors
{
    [HarmonyPatch(typeof(EncounterGameMenuBehavior), "game_menu_town_town_besiege_on_condition")]
    public class SettlementGameMenuBehavior
    {
        // If a settlement has a bribe cooldown, disable the option for besieging the settlement. Display the bribe cooldown's number of days in the option's tooltip.
        private static void Postfix(MenuCallbackArgs args)
        {
            Dictionary<Settlement, int> bribeCooldown = SurrenderTweaksHelper.SettlementBribeCooldown;
            Settlement currentSettlement = Settlement.CurrentSettlement;
            if (bribeCooldown.ContainsKey(currentSettlement))
            {
                MBTextManager.SetTextVariable("SETTLEMENT_BRIBE_COOLDOWN", bribeCooldown[currentSettlement]);
                MBTextManager.SetTextVariable("PLURAL", (bribeCooldown[currentSettlement] > 1) ? 1 : 0);
                args.Tooltip = new TextObject("You cannot attack this settlement for {SETTLEMENT_BRIBE_COOLDOWN} {?PLURAL}days{?}day{\\?}.", null);
                args.IsEnabled = false;
            }
            else
            {
                args.IsEnabled = true;
            }
        }
    }
}
