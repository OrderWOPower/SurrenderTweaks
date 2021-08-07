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
            CampaignEvents.OnGameLoadedEvent.AddNonSerializedListener(this, new Action<CampaignGameStarter>(OnGameLoaded));
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
                dataStore.SyncData("_bribeCooldown", ref _bribeCooldown);
                dataStore.SyncData("_hasOfferedBribe", ref _hasOfferedBribe);
            }
            catch (Exception ex)
            {
                InformationManager.DisplayMessage(new InformationMessage("Exception at SettlementBribeAndSurrenderBehavior.SyncData(): " + ex.Message));
            }
        }
        private void OnGameLoaded(CampaignGameStarter campaignGameStarter)
        {
            if (_defenderSettlement != null)
            {
                SurrenderTweaksHelper.GetStarvationPenalty(_starvationPenalty[_defenderSettlement]);
            }
            SurrenderTweaksHelper.GetTruceSettlements(_bribeCooldown.Keys.ToList());
            SurrenderTweaksHelper.GetBribeCooldowns(_bribeCooldown.Values.ToList());
        }
        // Add a starvation penalty to the besieged settlement.
        private void OnSiegeStarted(SiegeEvent siegeEvent)
        {
            _defenderSettlement = SurrenderTweaksHelper.DefenderSettlement;
            if (_defenderSettlement != null)
            {
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
        // If a settlement has a bribe cooldown, decrease the bribe cooldown by 1 day. If a settlement's bribe cooldown is 0 days, remove its bribe cooldown.
        // If a settlement is no longer under siege, remove its starvation penalty.
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
            SurrenderTweaksHelper.GetTruceSettlements(_bribeCooldown.Keys.ToList());
            SurrenderTweaksHelper.GetBribeCooldowns(_bribeCooldown.Values.ToList());
        }
        // If the settlement is willing to offer a bribe or surrender, make them request a parley with the player.
        public void OnHourlyTick()
        {
            if (_defenderSettlement != null)
            {
                SurrenderTweaksHelper.SetStarvationPenalty(_starvationPenalty[_defenderSettlement]);
                _starvationPenalty[_defenderSettlement] = SurrenderTweaksHelper.StarvationPenalty;
                SurrenderTweaksHelper.SetBribeOrSurrender(_defenderSettlement.MilitiaPartyComponent.MobileParty, MobileParty.MainParty);
                if ((SurrenderTweaksHelper.IsBribeFeasible && _hasOfferedBribe[_defenderSettlement] == 0) || (SurrenderTweaksHelper.IsSurrenderFeasible && _hasOfferedBribe[_defenderSettlement] == 1))
                {
                    RequestParley();
                    _hasOfferedBribe[_defenderSettlement] = 1;
                }
            }
        }
        public void OnTick(float dt) => _defenderSettlement = SurrenderTweaksHelper.DefenderSettlement;
        public void OnSessionLaunched(CampaignGameStarter campaignGameStarter) => AddDialogs(campaignGameStarter);
        // Add dialog lines for a settlement offering a bribe or surrender.
        protected void AddDialogs(CampaignGameStarter starter)
        {
            starter.AddDialogLine("", "start", "settlement_do_bribe", "We are low on food. There is no need to starve us. We can pay you to end the siege. Here is {MONEY}{GOLD_ICON}. Just take it and have mercy on us.", new ConversationSentence.OnConditionDelegate(conversation_settlement_bribe_on_condition), null, 100, null);
            starter.AddPlayerLine("", "settlement_do_bribe", "close_window", "I will have mercy on you for now. You have escaped doom, but not for long.", null, delegate
            {
                Campaign.Current.ConversationManager.ConversationEndOneShot += delegate
                {
                    conversation_settlement_bribe_on_consequence(_defenderSettlement);
                };
            }, 100, null, null);
            starter.AddPlayerLine("", "settlement_do_bribe", "close_window", "What a joke! You will stop starving when this settlement falls... or when you fall.", null, null, 100, null, null);
            starter.AddDialogLine("", "start", "close_window", "We are out of food. We don't want to starve any longer. We yield.", new ConversationSentence.OnConditionDelegate(conversation_settlement_surrender_on_condition), delegate
            {
                Campaign.Current.ConversationManager.ConversationEndOneShot += delegate
                {
                    conversation_settlement_surrender_on_consequence(_defenderSettlement.SiegeParties.ToList());
                };
            }, 100, null);
        }
        private bool conversation_settlement_bribe_on_condition() => SurrenderTweaksHelper.IsBribeFeasible && !SurrenderTweaksHelper.IsSurrenderFeasible && MobileParty.ConversationParty.IsMilitia;
        private bool conversation_settlement_surrender_on_condition() => SurrenderTweaksHelper.IsSurrenderFeasible && MobileParty.ConversationParty.IsMilitia;
        // If the player accepts a settlement's bribe, transfer the bribe amount from the settlement to the player and break the siege. Add a bribe cooldown to the settlement and set it to 7 days.
        private void conversation_settlement_bribe_on_consequence(Settlement settlement)
        {
            GiveGoldAction.ApplyForSettlementToCharacter(settlement, Hero.MainHero, SurrenderTweaksHelper.BribeAmount(), false);
            _bribeCooldown.Add(settlement, 7);
            SurrenderTweaksHelper.GetTruceSettlements(_bribeCooldown.Keys.ToList());
            SurrenderTweaksHelper.GetBribeCooldowns(_bribeCooldown.Values.ToList());
            typeof(SiegeEventCampaignBehavior).GetMethod("LeaveSiege", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, null);
        }
        // If the player accepts a settlement's surrender, capture the lords, capture all the troops in the settlement and capture all their trade items which do not belong to the settlement.
        // Capture the settlement.
        private void conversation_settlement_surrender_on_consequence(List<PartyBase> defenders)
        {
            PlayerEncounter.Init();
            PlayerEncounter.Current.SetupFields(PartyBase.MainParty, _defenderSettlement.Party);
            PlayerEncounter.StartBattle();
            PlayerEncounter.Update();
            Dictionary<PartyBase, ItemRoster> dictionary = new Dictionary<PartyBase, ItemRoster>();
            ItemRoster value = new ItemRoster();
            TroopRoster troopRoster = TroopRoster.CreateDummyTroopRoster();
            foreach (PartyBase defender in defenders)
            {
                if (defender != _defenderSettlement.Party)
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
            PartyScreenManager.OpenScreenAsLoot(TroopRoster.CreateDummyTroopRoster(), troopRoster, _defenderSettlement.Party.Name, troopRoster.TotalManCount, null);
        }
        // When the settlement requests a parley with the player, display a popup message.
        public void RequestParley() => InformationManager.ShowInquiry(new InquiryData("Defenders request to parley", "The defenders sound a horn and the gates open. A messenger rides out towards your camp and requests to parley.", true, false, new TextObject("OK", null).ToString(), "", new Action(AcceptParley), null, ""), true);
        // When the player accepts a parley, start a conversation with the settlement defenders.
        public void AcceptParley() => CampaignMapConversation.OpenConversation(new ConversationCharacterData(CharacterObject.PlayerCharacter, null, true, true, false, false), new ConversationCharacterData(_defenderSettlement.MilitiaPartyComponent.Party.Leader, _defenderSettlement.MilitiaPartyComponent.Party, false, true, false, false));
        private Settlement _defenderSettlement;
        private Dictionary<Settlement, int> _starvationPenalty = new Dictionary<Settlement, int>();
        private Dictionary<Settlement, int> _bribeCooldown = new Dictionary<Settlement, int>();
        private Dictionary<Settlement, int> _hasOfferedBribe = new Dictionary<Settlement, int>();
    }
}
