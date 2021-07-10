using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.SandBox.CampaignBehaviors;
using TaleWorlds.Core;
using TaleWorlds.Localization;

namespace SurrenderTweaks.Behaviors
{
    public class SettlementBribeAndSurrenderBehavior : CampaignBehaviorBase
    {
        public override void RegisterEvents()
        {
            CampaignEvents.OnSiegeEventStartedEvent.AddNonSerializedListener(this, new Action<SiegeEvent>(OnSiegeStarted));
            CampaignEvents.SiegeCompletedEvent.AddNonSerializedListener(this, new Action<Settlement, MobileParty, bool, bool>(OnSiegeCompleted));
            CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, new Action(OnDailyTick));
            CampaignEvents.HourlyTickEvent.AddNonSerializedListener(this, new Action(OnHourlyTick));
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, new Action<CampaignGameStarter>(OnSessionLaunched));
        }
        public override void SyncData(IDataStore dataStore)
        {
            try
            {
                dataStore.SyncData("_defenderSettlement", ref _defenderSettlement);
                dataStore.SyncData("_settlementBribeCooldown", ref _settlementBribeCooldown);
                dataStore.SyncData("_settlementStarvationPenalty", ref _settlementStarvationPenalty);
                dataStore.SyncData("_settlementHasOfferedBribe", ref _settlementHasOfferedBribe);
            }
            catch (Exception ex)
            {
                InformationManager.DisplayMessage(new InformationMessage("Exception at SettlementBribeAndSurrenderBehavior.SyncData(): " + ex.Message));
            }
        }
        // Do calculations only if the siege is started by the player. Add a starvation penalty to the besieged settlement.
        private void OnSiegeStarted(SiegeEvent siegeEvent)
        {
            if (siegeEvent.BesiegerCamp.BesiegerParty == MobileParty.MainParty)
            {
                DefenderSettlement = siegeEvent.BesiegedSettlement;
                if (!_settlementStarvationPenalty.ContainsKey(DefenderSettlement))
                {
                    _settlementStarvationPenalty.Add(DefenderSettlement, 0);
                }
                if (!_settlementHasOfferedBribe.ContainsKey(DefenderSettlement))
                {
                    _settlementHasOfferedBribe.Add(DefenderSettlement, 0);
                }
            }
        }
        public void OnSiegeCompleted(Settlement settlement, MobileParty capturerParty, bool isWin, bool isSiege)
        {
            if (isWin)
            {
                SettlementBribeCooldown.Remove(settlement);
                _settlementStarvationPenalty.Remove(settlement);
                _settlementHasOfferedBribe.Remove(settlement);
            }
            if (settlement == DefenderSettlement)
            {
                DefenderSettlement = null;
            }
        }
        // If a settlement has a bribe cooldown, decrease the bribe cooldown by 1 day. If a settlement's bribe cooldown is 0 days, remove its bribe cooldown.
        // If a settlement is no longer under siege, remove its starvation penalty.
        public void OnDailyTick()
        {
            foreach (Settlement settlement in SettlementBribeCooldown.Keys.ToList())
            {
                SettlementBribeCooldown[settlement]--;
                if (SettlementBribeCooldown[settlement] == 0)
                {
                    SettlementBribeCooldown.Remove(settlement);
                }
            }
            foreach (Settlement settlement in _settlementStarvationPenalty.Keys.ToList())
            {
                if (settlement != DefenderSettlement)
                {
                    _settlementStarvationPenalty.Remove(settlement);
                }
            }
            foreach (Settlement settlement in _settlementHasOfferedBribe.Keys.ToList())
            {
                if (settlement != DefenderSettlement && !SettlementBribeCooldown.ContainsKey(settlement))
                {
                    _settlementHasOfferedBribe.Remove(settlement);
                }
            }
        }
        // If a settlement has no food, increase its starvation penalty. If the settlement is willing to offer a bribe or surrender, make them request a parley with the player.
        public void OnHourlyTick()
        {
            if (DefenderSettlement != null && MapEvent.PlayerMapEvent == null)
            {
                _settlementFood = Math.Ceiling(DefenderSettlement.Town.FoodStocks / -DefenderSettlement.Town.FoodChange);
                if (_settlementFood > 0)
                {
                    _settlementStarvationPenalty[DefenderSettlement] = 0;
                }
                else
                {
                    _settlementStarvationPenalty[DefenderSettlement] += 8;
                }
                if ((BribeAndSurrenderBehavior.IsBribeFeasible && _settlementHasOfferedBribe[DefenderSettlement] == 0) || (BribeAndSurrenderBehavior.IsSurrenderFeasible && _settlementHasOfferedBribe[DefenderSettlement] == 1))
                {
                    RequestParley();
                    _settlementHasOfferedBribe[DefenderSettlement] = 1;
                }
            }
        }
        public void OnSessionLaunched(CampaignGameStarter campaignGameStarter) => AddDialogs(campaignGameStarter);
        // Add dialog lines for a settlement offering a bribe or surrender.
        protected void AddDialogs(CampaignGameStarter starter)
        {
            starter.AddDialogLine("", "start", "settlement_do_bribe", "We are low on food. There is no need to starve us. We can pay you to end the siege. Here is {MONEY}{GOLD_ICON}. Just take it and have mercy on us.", new ConversationSentence.OnConditionDelegate(conversation_settlement_bribe_on_condition), null, 100, null);
            starter.AddPlayerLine("", "settlement_do_bribe", "close_window", "I will have mercy on you for now. You have escaped doom, but not for long.", null, delegate
            {
                Campaign.Current.ConversationManager.ConversationEndOneShot += delegate
                {
                    conversation_settlement_bribe_on_consequence(DefenderSettlement);
                };
            }, 100, null, null);
            starter.AddPlayerLine("", "settlement_do_bribe", "close_window", "What a joke! You will stop starving when this settlement falls... or when you fall.", null, null, 100, null, null);
            starter.AddDialogLine("", "start", "close_window", "We are out of food. We don't want to starve any longer. We yield.", new ConversationSentence.OnConditionDelegate(conversation_settlement_surrender_on_condition), delegate
            {
                Campaign.Current.ConversationManager.ConversationEndOneShot += delegate
                {
                    conversation_settlement_surrender_on_consequence(DefenderSettlement.SiegeParties.ToList());
                };
            }, 100, null);
        }
        private bool conversation_settlement_bribe_on_condition() => BribeAndSurrenderBehavior.IsBribeFeasible && !BribeAndSurrenderBehavior.IsSurrenderFeasible;
        private bool conversation_settlement_surrender_on_condition() => BribeAndSurrenderBehavior.IsSurrenderFeasible;
        // If the player accepts a settlement's bribe, transfer the bribe amount from the settlement to the player and break the siege. Add a bribe cooldown to the settlement and set it to 7 days.
        private void conversation_settlement_bribe_on_consequence(Settlement settlement)
        {
            GiveGoldAction.ApplyForSettlementToCharacter(settlement, Hero.MainHero, BribeAndSurrenderBehavior.BribeAmount, false);
            SettlementBribeCooldown.Add(settlement, 7);
            typeof(SiegeEventCampaignBehavior).GetMethod("LeaveSiege", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, null);
        }
        // If the player accepts a settlement's surrender, capture the lords, capture all the troops in the settlement and capture all their trade items which do not belong to the settlement.
        // Capture the settlement.
        private void conversation_settlement_surrender_on_consequence(List<PartyBase> defenders)
        {
            PlayerEncounter.Init();
            PlayerEncounter.Current.SetupFields(PartyBase.MainParty, DefenderSettlement.Party);
            PlayerEncounter.StartBattle();
            PlayerEncounter.Update();
            Dictionary<PartyBase, ItemRoster> dictionary = new Dictionary<PartyBase, ItemRoster>();
            ItemRoster value = new ItemRoster();
            TroopRoster troopRoster = TroopRoster.CreateDummyTroopRoster();
            foreach (PartyBase defender in defenders)
            {
                if (defender != DefenderSettlement.Party)
                {
                    value.Add(defender.ItemRoster);
                    defender.ItemRoster.Clear();
                }
                foreach (TroopRosterElement troopRosterElement in defender.MemberRoster.GetTroopRoster())
                {
                    if (troopRosterElement.Character.HeroObject == null)
                    {
                        troopRoster.AddToCounts(troopRosterElement.Character, troopRosterElement.Number, false, 0, 0, true, -1);
                    }
                }
                if (defender.LeaderHero != null)
                {
                    TakePrisonerAction.Apply(PartyBase.MainParty, defender.LeaderHero);
                }
                if (defender.MobileParty != null)
                {
                    DestroyPartyAction.Apply(PartyBase.MainParty, defender.MobileParty);
                }
            }
            dictionary.Add(PartyBase.MainParty, value);
            InventoryManager.OpenScreenAsLoot(dictionary);
            PartyScreenManager.OpenScreenAsLoot(TroopRoster.CreateDummyTroopRoster(), troopRoster, DefenderSettlement.Party.Name, troopRoster.TotalManCount, null);
        }
        // Compare the defenders' and attackers' relative strengths. Give the defenders a bonus for every day of food that they have. Give the defenders a penalty if they have no food.
        public static bool DoesSurrenderIsLogicalForSettlement(MobileParty defender, MobileParty attacker, float acceptablePowerRatio = 0.1f)
        {
            double num = defender.Party.TotalStrength;
            double num2 = attacker.Party.TotalStrength;
            foreach (PartyBase party in DefenderSettlement.SiegeParties)
            {
                if (party != defender.Party)
                {
                    num += party.TotalStrength;
                }
            }
            foreach (PartyBase party in DefenderSettlement.SiegeEvent.BesiegerCamp.SiegeParties)
            {
                if (party != attacker.Party)
                {
                    num2 += party.TotalStrength;
                }
            }
            double num3 = ((double)(num2 * acceptablePowerRatio) * (0.5f + 0.5f * (defender.Party.Random.GetValue(0) / 100f))) - (_settlementFood * 96) + _settlementStarvationPenalty[DefenderSettlement];
            return num < num3;
        }
        // When the settlement requests a parley with the player, display a popup message.
        public void RequestParley() => InformationManager.ShowInquiry(new InquiryData("Defenders request to parley", "The defenders sound a horn and the gates open. A messenger rides out towards your camp and requests to parley.", true, false, new TextObject("OK", null).ToString(), "", new Action(AcceptParley), null, ""), true);
        // When the player accepts a parley, start a conversation with the settlement defenders.
        public void AcceptParley() => CampaignMapConversation.OpenConversation(new ConversationCharacterData(CharacterObject.PlayerCharacter, null, true, true, false, false), new ConversationCharacterData(DefenderSettlement.MilitiaPartyComponent.Party.Leader, DefenderSettlement.MilitiaPartyComponent.Party, false, true, false, false));
        public static Settlement DefenderSettlement { get => _defenderSettlement; set => _defenderSettlement = value; }
        public static Dictionary<Settlement, int> SettlementBribeCooldown { get => _settlementBribeCooldown; set => _settlementBribeCooldown = value; }
        private static double _settlementFood;
        private static Settlement _defenderSettlement;
        private static Dictionary<Settlement, int> _settlementBribeCooldown = new Dictionary<Settlement, int>();
        private static Dictionary<Settlement, int> _settlementStarvationPenalty = new Dictionary<Settlement, int>();
        private Dictionary<Settlement, int> _settlementHasOfferedBribe = new Dictionary<Settlement, int>();
    }
}
