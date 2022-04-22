using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Siege;
using TaleWorlds.Core;

namespace SurrenderTweaks.Behaviors
{
    public class BribeAndSurrenderBehavior : CampaignBehaviorBase
    {
        private bool _isBribeFeasible;

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
            }
            catch (Exception ex)
            {
                InformationManager.DisplayMessage(new InformationMessage(ex.Message + "\r\n" + ex.StackTrace));
            }
        }

        private void OnGameLoaded(CampaignGameStarter campaignGameStarter) => SurrenderEvent.PlayerSurrenderEvent.SetBribe(_isBribeFeasible);

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
            if (MobileParty.ConversationParty != null && !MobileParty.ConversationParty.IsMilitia)
            {
                surrenderEvent.SetBribeOrSurrender(MobileParty.ConversationParty, MobileParty.MainParty);
            }
            _isBribeFeasible = surrenderEvent.IsBribeFeasible;
        }

        private void OnTick(float dt)
        {
            SurrenderEvent surrenderEvent = SurrenderEvent.PlayerSurrenderEvent;
            if (MapEvent.PlayerMapEvent == null && PlayerSiege.PlayerSiegeEvent == null)
            {
                surrenderEvent.SetBribeOrSurrender(null, null);
            }
            _isBribeFeasible = surrenderEvent.IsBribeFeasible;
        }
    }
}
