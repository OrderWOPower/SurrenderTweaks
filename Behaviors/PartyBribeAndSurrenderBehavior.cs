using HarmonyLib;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.CampaignSystem.SandBox.CampaignBehaviors;
using TaleWorlds.CampaignSystem.SandBox.CampaignBehaviors.VillageBehaviors;

namespace SurrenderTweaks.Behaviors
{
    public class PartyBribeAndSurrenderBehavior
    {
        // Replace the value of the chance of bandits, caravans and villagers offering a bribe with the value calculated in this mod.
        [HarmonyPatch]
        public class PartyBribeBehavior
        {
            public static IEnumerable<MethodBase> TargetMethods()
            {
                yield return AccessTools.Method(typeof(BanditsCampaignBehavior), "conversation_bandits_will_join_player_on_condition");
                yield return AccessTools.Method(typeof(CaravansCampaignBehavior), "IsBribeFeasible");
                yield return AccessTools.Method(typeof(VillagerCampaignBehavior), "IsBribeFeasible");
            }
            private static void Postfix(ref bool __result) => __result = SurrenderTweaksHelper.IsBribeFeasible;
        }
        // Replace the value of the chance of bandits, caravans and villagers offering a surrender with the value calculated in this mod.
        [HarmonyPatch]
        public class PartySurrenderBehavior
        {
            public static IEnumerable<MethodBase> TargetMethods()
            {
                yield return AccessTools.Method(typeof(BanditsCampaignBehavior), "conversation_bandits_surrender_on_condition");
                yield return AccessTools.Method(typeof(CaravansCampaignBehavior), "IsSurrenderFeasible");
                yield return AccessTools.Method(typeof(VillagerCampaignBehavior), "IsSurrenderFeasible");
            }
            private static void Postfix(ref bool __result) => __result = SurrenderTweaksHelper.IsSurrenderFeasible;
        }
    }
}
