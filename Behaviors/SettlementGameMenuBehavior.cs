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
            Dictionary<Settlement, int> bribeCooldown = SurrenderTweaksHelper.BribeCooldown;
            Settlement settlement = Settlement.CurrentSettlement;
            if (bribeCooldown.ContainsKey(settlement))
            {
                MBTextManager.SetTextVariable("BRIBE_COOLDOWN", bribeCooldown[settlement]);
                MBTextManager.SetTextVariable("PLURAL", (bribeCooldown[settlement] > 1) ? 1 : 0);
                args.Tooltip = new TextObject("You cannot attack this settlement for {BRIBE_COOLDOWN} {?PLURAL}days{?}day{\\?}.", null);
                args.IsEnabled = false;
            }
            else
            {
                args.IsEnabled = true;
            }
        }
    }
}
