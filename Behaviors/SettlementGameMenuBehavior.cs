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
        public static void Postfix(MenuCallbackArgs args)
        {
            for (int i = 0; i < SurrenderTweaksHelper.TruceSettlements?.Count; i++)
            {
                if (SurrenderTweaksHelper.TruceSettlements[i] == Settlement.CurrentSettlement)
                {
                    MBTextManager.SetTextVariable("BRIBE_COOLDOWN", SurrenderTweaksHelper.BribeCooldowns[i]);
                    MBTextManager.SetTextVariable("PLURAL", (SurrenderTweaksHelper.BribeCooldowns[i] > 1) ? 1 : 0);
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
}
