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
            CampaignEvents.TickEvent.AddNonSerializedListener(this, new Action<float>(OnTick));
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, new Action<CampaignGameStarter>(OnSessionLaunched));
        }
        public override void SyncData(IDataStore dataStore)
        {
            try
            {
                dataStore.SyncData("_defenderSettlement", ref _defenderSettlement);
                dataStore.SyncData("_starvationPenalty", ref _starvationPenalty);
                dataStore.SyncData("_settlementBribeCooldown", ref _bribeCooldown);
                dataStore.SyncData("_hasOfferedBribe", ref _hasOfferedBribe);
            }
            catch (Exception ex)
            {
                InformationManager.DisplayMessage(new InformationMessage("Exception at SettlementBribeAndSurrenderBehavior.SyncData(): " + ex.Message));
            }
        }
        // Add a starvation penalty to the besieged settlement.
        public void OnSiegeStarted(SiegeEvent siegeEvent)
        {
            if (siegeEvent.BesiegerCamp.BesiegerParty.IsMainParty)
            {
                _defenderSettlement = siegeEvent.BesiegedSettlement;
                if (!_starvationPenalty.ContainsKey(_defenderSettlement))
                {
                    _starvationPenalty.Add(_defenderSettlement, 0);
                }
                if (!_hasOfferedBribe.ContainsKey(_defenderSettlement))
                {
                    _hasOfferedBribe.Add(_defenderSettlement, 0);
                }
            }
        }
        public void OnSiegeCompleted(Settlement settlement, MobileParty capturerParty, bool isWin, bool isSiege)
        {
            if (isWin)
            {
                _starvationPenalty.Remove(settlement);
                _bribeCooldown.Remove(settlement);
                _hasOfferedBribe.Remove(settlement);
            }
        }
        // If a settlement is no longer under siege, remove its starvation penalty.
        // If a settlement has a bribe cooldown, decrease the bribe cooldown by 1 day.
        // If a settlement's bribe cooldown is 0 days, remove its bribe cooldown.
        public void OnDailyTick()
        {
            foreach (Settlement settlement in _starvationPenalty.Keys.ToList())
            {
                if (settlement != _defenderSettlement)
                {
                    _starvationPenalty.Remove(settlement);
                }
            }
            foreach (Settlement settlement in _bribeCooldown.Keys.ToList())
            {
                _bribeCooldown[settlement]--;
                if (_bribeCooldown[settlement] == 0)
                {
                    _bribeCooldown.Remove(settlement);
                }
            }
            foreach (Settlement settlement in _hasOfferedBribe.Keys.ToList())
            {
                if (settlement != _defenderSettlement && !_bribeCooldown.ContainsKey(settlement))
                {
                    _hasOfferedBribe.Remove(settlement);
                }
            }
        }
        // If a settlement has no food, increase its starvation penalty.
        // If the settlement is willing to offer a bribe or surrender, make them request a parley with the player.
        public void OnHourlyTick()
        {
            if (_defenderSettlement != null)
            {
                _food = Math.Ceiling(_defenderSettlement.Town.FoodStocks / -_defenderSettlement.Town.FoodChange);
                if (_food > 0)
                {
                    _starvationPenalty[_defenderSettlement] = 0;
                }
                else
                {
                    _starvationPenalty[_defenderSettlement] += 8;
                }
                SurrenderTweaksHelper.SetBribeOrSurrender(_defenderSettlement.MilitiaPartyComponent?.MobileParty, MobileParty.MainParty, _food, _starvationPenalty[_defenderSettlement]);
                if ((SurrenderTweaksHelper.IsBribeFeasible && _hasOfferedBribe[_defenderSettlement] == 0) || (SurrenderTweaksHelper.IsSurrenderFeasible && _hasOfferedBribe[_defenderSettlement] == 1))
                {
                    RequestParley();
                    _hasOfferedBribe[_defenderSettlement] = 1;
                }
            }
        }
        public void OnTick(float dt)
        {
            if (PlayerSiege.PlayerSiegeEvent == null)
            {
                _defenderSettlement = null;
            }
            SurrenderTweaksHelper.SetSettlementBribeCooldown(_bribeCooldown);
        }
        public void OnSessionLaunched(CampaignGameStarter campaignGameStarter) => AddDialogs(campaignGameStarter);
        // Add dialog lines for a settlement offering a bribe or surrender.
        protected void AddDialogs(CampaignGameStarter starter)
        {
            starter.AddDialogLine("", "start", "settlement_do_bribe", "We are low on food. There is no need to starve us. We can pay you to end the siege. Here is {MONEY}{GOLD_ICON}. Just take it and have mercy on us.", new ConversationSentence.OnConditionDelegate(conversation_settlement_bribe_on_condition), null, 100, null);
            starter.AddPlayerLine("", "settlement_do_bribe", "close_window", "I will have mercy on you for now. You have escaped doom, but not for long.", null, new ConversationSentence.OnConsequenceDelegate(conversation_settlement_bribe_on_consequence), 100, null, null);
            starter.AddPlayerLine("", "settlement_do_bribe", "close_window", "What a joke! You will stop starving when this settlement falls... or when you fall.", null, null, 100, null, null);
            starter.AddDialogLine("", "start", "close_window", "We are out of food. We don't want to starve any longer. We yield.", new ConversationSentence.OnConditionDelegate(conversation_settlement_surrender_on_condition), delegate
            {
                Campaign.Current.ConversationManager.ConversationEndOneShot += conversation_settlement_surrender_on_consequence;
            }, 100, null);
        }
        private bool conversation_settlement_bribe_on_condition()
        {
            SurrenderTweaksHelper.BribeAmount(MobileParty.ConversationParty, PlayerSiege.BesiegedSettlement, out int num);
            MBTextManager.SetTextVariable("MONEY", num);
            return SurrenderTweaksHelper.IsBribeFeasible && !SurrenderTweaksHelper.IsSurrenderFeasible && MobileParty.ConversationParty != null && MobileParty.ConversationParty.IsMilitia;
        }
        private bool conversation_settlement_surrender_on_condition() => SurrenderTweaksHelper.IsSurrenderFeasible && MobileParty.ConversationParty != null && MobileParty.ConversationParty.IsMilitia;
        // If the player accepts a settlement's bribe, transfer the bribe amount from the settlement to the player and break the siege. Add a bribe cooldown to the settlement and set it to 10 days.
        private void conversation_settlement_bribe_on_consequence()
        {
            SurrenderTweaksHelper.BribeAmount(MobileParty.ConversationParty, PlayerSiege.BesiegedSettlement, out int num);
            GiveGoldAction.ApplyForSettlementToCharacter(PlayerSiege.BesiegedSettlement, Hero.MainHero, num, false);
            _bribeCooldown.Add(PlayerSiege.BesiegedSettlement, 10);
            typeof(SiegeEventCampaignBehavior).GetMethod("LeaveSiege", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, null);
        }
        // If the player accepts a settlement's surrender, capture the lords, capture all the troops in the settlement and capture all their trade items which do not belong to the settlement.
        // Capture the settlement.
        private void conversation_settlement_surrender_on_consequence()
        {
            PlayerEncounter.Init();
            PlayerEncounter.Current.SetupFields(PartyBase.MainParty, PlayerSiege.BesiegedSettlement.Party);
            PlayerEncounter.StartBattle();
            PlayerEncounter.Update();
            Dictionary<PartyBase, ItemRoster> dictionary = new Dictionary<PartyBase, ItemRoster>();
            ItemRoster value = new ItemRoster();
            TroopRoster troopRoster = TroopRoster.CreateDummyTroopRoster();
            foreach (PartyBase defender in PlayerSiege.BesiegedSettlement.SiegeParties.ToList())
            {
                if (defender != PlayerSiege.BesiegedSettlement.Party)
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
            PartyScreenManager.OpenScreenAsLoot(TroopRoster.CreateDummyTroopRoster(), troopRoster, PlayerSiege.BesiegedSettlement.Party.Name, troopRoster.TotalManCount, null);
        }
        // When the settlement requests a parley with the player, display a popup message.
        public void RequestParley() => InformationManager.ShowInquiry(new InquiryData("Defenders request to parley", "The defenders sound a horn and open the gates. A messenger rides out towards your camp and requests to parley.", true, false, new TextObject("OK", null).ToString(), "", new Action(AcceptParley), null, ""), true);
        // When the player accepts a parley, start a conversation with the settlement defenders.
        public void AcceptParley() => CampaignMapConversation.OpenConversation(new ConversationCharacterData(CharacterObject.PlayerCharacter, null, true, true, false, false), new ConversationCharacterData(_defenderSettlement.MilitiaPartyComponent.Party.Leader, _defenderSettlement.MilitiaPartyComponent.Party, false, true, false, false));
        private Settlement _defenderSettlement;
        private Dictionary<Settlement, int> _starvationPenalty = new Dictionary<Settlement, int>();
        private Dictionary<Settlement, int> _bribeCooldown = new Dictionary<Settlement, int>();
        private Dictionary<Settlement, int> _hasOfferedBribe = new Dictionary<Settlement, int>();
        private double _food;
    }
}
