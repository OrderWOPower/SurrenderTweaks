using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.Core;

namespace SurrenderTweaks.Behaviors
{
    public class BribeAndSurrenderBehavior : CampaignBehaviorBase
    {
        public override void RegisterEvents()
        {
            CampaignEvents.SetupPreConversationEvent.AddNonSerializedListener(this, new Action(OnSetupPreConversation));
            CampaignEvents.MapEventStarted.AddNonSerializedListener(this, new Action<MapEvent, PartyBase, PartyBase>(OnMapEventStarted));
            CampaignEvents.TickEvent.AddNonSerializedListener(this, new Action<float>(OnTick));
        }
        public override void SyncData(IDataStore dataStore) { }
        public void OnSetupPreConversation()
        {
            if (MobileParty.ConversationParty != null && !MobileParty.ConversationParty.IsMilitia)
            {
                SurrenderTweaksHelper.SetBribeOrSurrender(MobileParty.ConversationParty, MobileParty.MainParty);
            }
        }
        // If a party is willing to offer a surrender to an AI attacker, capture the lord, capture all the troops in the party and capture all their trade items.
        public void OnMapEventStarted(MapEvent mapEvent, PartyBase attackerParty, PartyBase defenderParty)
        {
            MobileParty defender = defenderParty.MobileParty;
            MobileParty attacker = attackerParty.MobileParty;
            if (!mapEvent.IsPlayerMapEvent && SurrenderTweaksHelper.IsBribeOrSurrenderFeasible(defender, attacker, 0, 0, true))
            {
                foreach (ItemRosterElement itemRosterElement in defender.ItemRoster)
                {
                    attacker.ItemRoster.AddToCounts(itemRosterElement.EquipmentElement, itemRosterElement.Amount);
                }
                defender.ItemRoster.Clear();
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
                DestroyPartyAction.Apply(attackerParty, defender);
            }
        }
        public void OnTick(float dt)
        {
            if (PlayerSiege.PlayerSiegeEvent == null)
            {
                SurrenderTweaksHelper.SetBribeOrSurrender(null, null);
            }
        }
    }
}
