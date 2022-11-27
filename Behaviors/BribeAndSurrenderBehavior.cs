using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Siege;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace SurrenderTweaks.Behaviors
{
    public class BribeAndSurrenderBehavior : CampaignBehaviorBase
    {
        private bool _isBribeFeasible;
        private bool _isSurrenderFeasible;

        public override void RegisterEvents()
        {
            CampaignEvents.OnGameLoadedEvent.AddNonSerializedListener(this, new Action<CampaignGameStarter>(OnGameLoaded));
            CampaignEvents.MapEventStarted.AddNonSerializedListener(this, new Action<MapEvent, PartyBase, PartyBase>(OnMapEventStarted));
            CampaignEvents.SetupPreConversationEvent.AddNonSerializedListener(this, new Action(OnSetupPreConversation));
            CampaignEvents.TickEvent.AddNonSerializedListener(this, new Action<float>(OnTick));
        }

        public override void SyncData(IDataStore dataStore)
        {
            try
            {
                dataStore.SyncData("_isBribeFeasible", ref _isBribeFeasible);
                dataStore.SyncData("_isSurrenderFeasible", ref _isSurrenderFeasible);
            }
            catch (Exception ex)
            {
                InformationManager.DisplayMessage(new InformationMessage(ex.Message + "\r\n" + ex.StackTrace));
            }
        }

        private void OnGameLoaded(CampaignGameStarter campaignGameStarter) => SurrenderEvent.PlayerSurrenderEvent.SetBribeOrSurrender(_isBribeFeasible, _isSurrenderFeasible);

        // If a party is willing to offer a surrender to an AI attacker, capture the lord, capture all the troops in the party and capture all their trade items.
        private void OnMapEventStarted(MapEvent mapEvent, PartyBase attackerParty, PartyBase defenderParty)
        {
            MobileParty defender = defenderParty.MobileParty;
            MobileParty attacker = attackerParty.MobileParty;
            if (!mapEvent.IsPlayerMapEvent && SurrenderHelper.IsBribeOrSurrenderFeasible(defender, attacker, 0, 0, true))
            {
                foreach (ItemRosterElement itemRosterElement in defender.ItemRoster)
                {
                    attacker.ItemRoster.AddToCounts(itemRosterElement.EquipmentElement, itemRosterElement.Amount);
                }
                defender.ItemRoster.Clear();
                SurrenderHelper.AddPrisonersAsCasualties(attacker, defender);
                foreach (TroopRosterElement troopRosterElement in defender.MemberRoster.GetTroopRoster())
                {
                    if (!troopRosterElement.Character.IsHero)
                    {
                        attacker.PrisonRoster.AddToCounts(troopRosterElement.Character, troopRosterElement.Number, false, 0, 0, true, -1);
                    }
                    else
                    {
                        TakePrisonerAction.Apply(attackerParty, troopRosterElement.Character.HeroObject);
                    }
                }
            }
        }

        private void OnSetupPreConversation()
        {
            SurrenderEvent surrenderEvent = SurrenderEvent.PlayerSurrenderEvent;
            if (MobileParty.ConversationParty != null && MobileParty.ConversationParty.Army == null && !MobileParty.ConversationParty.IsMilitia)
            {
                surrenderEvent.SetBribeOrSurrender(MobileParty.ConversationParty, MobileParty.MainParty);
            }
            _isBribeFeasible = surrenderEvent.IsBribeFeasible;
            _isSurrenderFeasible = surrenderEvent.IsSurrenderFeasible;
        }

        private void OnTick(float dt)
        {
            SurrenderEvent surrenderEvent = SurrenderEvent.PlayerSurrenderEvent;
            if (MapEvent.PlayerMapEvent == null && PlayerSiege.PlayerSiegeEvent == null)
            {
                surrenderEvent.SetBribeOrSurrender(null, null);
            }
            _isBribeFeasible = surrenderEvent.IsBribeFeasible;
            _isSurrenderFeasible = surrenderEvent.IsSurrenderFeasible;
        }

        [HarmonyPatch]
        public class BribeConditionBehavior
        {
            private static IEnumerable<MethodBase> TargetMethods()
            {
                yield return AccessTools.Method(typeof(BanditsCampaignBehavior), "conversation_bandits_will_join_player_on_condition");
                yield return AccessTools.Method(typeof(CaravansCampaignBehavior), "IsBribeFeasible");
                yield return AccessTools.Method(typeof(VillagerCampaignBehavior), "IsBribeFeasible");
            }

            private static void Postfix(MethodBase __originalMethod, ref bool __result) => __result = __originalMethod.DeclaringType == typeof(BanditsCampaignBehavior) && Hero.MainHero.GetPerkValue(DefaultPerks.Roguery.PartnersInCrime) ? true : SurrenderEvent.PlayerSurrenderEvent.IsBribeFeasible;
        }

        [HarmonyPatch]
        public class SurrenderConditionBehavior
        {
            private static IEnumerable<MethodBase> TargetMethods()
            {
                yield return AccessTools.Method(typeof(BanditsCampaignBehavior), "conversation_bandits_surrender_on_condition");
                yield return AccessTools.Method(typeof(CaravansCampaignBehavior), "IsSurrenderFeasible");
                yield return AccessTools.Method(typeof(VillagerCampaignBehavior), "IsSurrenderFeasible");
            }

            private static void Postfix(ref bool __result) => __result = SurrenderEvent.PlayerSurrenderEvent.IsSurrenderFeasible;
        }

        [HarmonyPatch]
        public class SurrenderConsequenceBehavior
        {
            private static IEnumerable<MethodBase> TargetMethods()
            {
                yield return AccessTools.Method(typeof(CaravansCampaignBehavior), "conversation_caravan_took_prisoner_on_consequence");
                yield return AccessTools.Method(typeof(VillagerCampaignBehavior), "conversation_village_farmer_took_prisoner_on_consequence");
            }

            private static void Prefix() => SurrenderHelper.AddPrisonersAsCasualties(MobileParty.MainParty, PlayerEncounter.EncounteredMobileParty);
        }
    }
}
