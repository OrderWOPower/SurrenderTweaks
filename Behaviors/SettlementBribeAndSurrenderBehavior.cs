using HarmonyLib;
using Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Conversation;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.Inventory;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Siege;
using TaleWorlds.CampaignSystem.ViewModelCollection;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace SurrenderTweaks.Behaviors
{
    [HarmonyPatch(typeof(EncounterGameMenuBehavior), "game_menu_town_town_besiege_on_condition")]
    public class SettlementBribeAndSurrenderBehavior : CampaignBehaviorBase
    {
        private static Dictionary<Settlement, int> _bribeCooldown = new Dictionary<Settlement, int>();
        private Dictionary<Settlement, int> _hasOfferedBribe = new Dictionary<Settlement, int>();
        private Dictionary<Settlement, int> _starvationPenalty = new Dictionary<Settlement, int>();

        // If a settlement has a bribe cooldown, disable the option for besieging the settlement. Display the bribe cooldown's number of days in the option's tooltip.
        private static void Postfix(MenuCallbackArgs args)
        {
            Settlement currentSettlement = Settlement.CurrentSettlement;
            if (_bribeCooldown.ContainsKey(currentSettlement))
            {
                MBTextManager.SetTextVariable("SETTLEMENT_BRIBE_COOLDOWN", _bribeCooldown[currentSettlement]);
                MBTextManager.SetTextVariable("PLURAL", (_bribeCooldown[currentSettlement] > 1) ? 1 : 0);
                args.Tooltip = new TextObject("{=SurrenderTweaks09}You cannot attack this settlement for {SETTLEMENT_BRIBE_COOLDOWN} {?PLURAL}days{?}day{\\?}.", null);
                args.IsEnabled = false;
            }
        }

        public override void RegisterEvents()
        {
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, new Action<CampaignGameStarter>(OnSessionLaunched));
            CampaignEvents.OnSiegeEventStartedEvent.AddNonSerializedListener(this, new Action<SiegeEvent>(OnSiegeStarted));
            CampaignEvents.SiegeCompletedEvent.AddNonSerializedListener(this, new Action<Settlement, MobileParty, bool, bool>(OnSiegeCompleted));
            CampaignEvents.DailyTickSettlementEvent.AddNonSerializedListener(this, new Action<Settlement>(OnDailyTickSettlement));
            CampaignEvents.HourlyTickSettlementEvent.AddNonSerializedListener(this, new Action<Settlement>(OnHourlyTickSettlement));
        }

        public override void SyncData(IDataStore dataStore)
        {
            try
            {
                dataStore.SyncData("_settlementBribeCooldown", ref _bribeCooldown);
                dataStore.SyncData("_hasOfferedBribe", ref _hasOfferedBribe);
                dataStore.SyncData("_starvationPenalty", ref _starvationPenalty);
            }
            catch (Exception ex)
            {
                InformationManager.DisplayMessage(new InformationMessage(ex.Message + "\r\n" + ex.StackTrace));
            }
        }

        public void OnSessionLaunched(CampaignGameStarter campaignGameStarter) => AddDialogs(campaignGameStarter);

        // Add a starvation penalty to the besieged settlement.
        public void OnSiegeStarted(SiegeEvent siegeEvent)
        {
            Settlement settlement = siegeEvent.BesiegedSettlement;
            if (!_hasOfferedBribe.ContainsKey(settlement))
            {
                _hasOfferedBribe.Add(settlement, 0);
            }
            if (!_starvationPenalty.ContainsKey(settlement))
            {
                _starvationPenalty.Add(settlement, 0);
            }
        }

        public void OnSiegeCompleted(Settlement settlement, MobileParty capturerParty, bool isWin, bool isSiege)
        {
            if (isWin)
            {
                _bribeCooldown.Remove(settlement);
                _hasOfferedBribe.Remove(settlement);
                _starvationPenalty.Remove(settlement);
            }
        }

        // If a settlement has a bribe cooldown, decrease the bribe cooldown by 1 day.
        // If a settlement's bribe cooldown is 0 days, remove its bribe cooldown.
        // If a settlement is no longer under siege, remove its starvation penalty.
        public void OnDailyTickSettlement(Settlement settlement)
        {
            if (_bribeCooldown.ContainsKey(settlement))
            {
                _bribeCooldown[settlement]--;
                if (_bribeCooldown[settlement] <= 0)
                {
                    _bribeCooldown.Remove(settlement);
                }
            }
            if (settlement.SiegeEvent == null)
            {
                if (!_bribeCooldown.ContainsKey(settlement))
                {
                    _hasOfferedBribe.Remove(settlement);
                }
                _starvationPenalty.Remove(settlement);
            }
        }

        // If a settlement has no food, increase its starvation penalty.
        // If the settlement is willing to offer a bribe or surrender to the player, make them request a parley with the player.
        // If the settlement is willing to offer a surrender to an AI attacker, capture the lords, capture all the troops in the settlement and capture all their trade items which do not belong to the settlement.
        // Capture the settlement.
        public void OnHourlyTickSettlement(Settlement settlement)
        {
            if (settlement.SiegeEvent != null)
            {
                float foodChange = settlement.Town.FoodChangeWithoutMarketStocks;
                ValueTuple<int, int> townFoodAndMarketStocks = TownHelpers.GetTownFoodAndMarketStocks(settlement.Town);
                int daysUntilNoFood = MathF.Ceiling(MathF.Abs((townFoodAndMarketStocks.Item1 + townFoodAndMarketStocks.Item2) / foodChange));
                MobileParty attacker = settlement.SiegeEvent.BesiegerCamp.BesiegerParty;
                if (!SettlementHelper.IsGarrisonStarving(settlement))
                {
                    _starvationPenalty[settlement] = 0;
                }
                else
                {
                    _starvationPenalty[settlement] += 4;
                }
                if (attacker.IsMainParty)
                {
                    SurrenderTweaksHelper.SetBribeOrSurrender(settlement.MilitiaPartyComponent?.MobileParty, attacker, daysUntilNoFood, _starvationPenalty[settlement]);
                    if ((SurrenderTweaksHelper.IsBribeFeasible && _hasOfferedBribe[settlement] == 0) || (SurrenderTweaksHelper.IsSurrenderFeasible && _hasOfferedBribe[settlement] == 1))
                    {
                        RequestParley();
                        _hasOfferedBribe[settlement] = 1;
                    }
                }
                else if (!settlement.SiegeEvent.IsPlayerSiegeEvent && settlement.Party.MapEvent == null && SurrenderTweaksHelper.IsBribeOrSurrenderFeasible(settlement.MilitiaPartyComponent?.MobileParty, attacker, daysUntilNoFood, _starvationPenalty[settlement], true))
                {
                    foreach (PartyBase defender in settlement.SiegeParties.ToList())
                    {
                        if (defender != settlement.Party)
                        {
                            foreach (ItemRosterElement itemRosterElement in defender.ItemRoster)
                            {
                                attacker.ItemRoster.AddToCounts(itemRosterElement.EquipmentElement, itemRosterElement.Amount);
                            }
                            defender.ItemRoster.Clear();
                        }
                        foreach (TroopRosterElement troopRosterElement in defender.MemberRoster.GetTroopRoster())
                        {
                            if (!troopRosterElement.Character.IsHero)
                            {
                                attacker.PrisonRoster.AddToCounts(troopRosterElement.Character, troopRosterElement.Number, false, 0, 0, true, -1);
                            }
                            else
                            {
                                TakePrisonerAction.Apply(attacker.Party, troopRosterElement.Character.HeroObject);
                            }
                        }
                    }
                    settlement.SiegeEvent.BesiegerCamp.SiegeEngines.SiegePreparations.SetProgress(1f);
                }
            }
        }

        // Add dialog lines for a settlement offering a bribe or surrender.
        private void AddDialogs(CampaignGameStarter starter)
        {
            starter.AddDialogLine("", "start", "settlement_do_bribe", "{=SurrenderTweaks10}We are low on food. There is no need to starve us. We can pay you to end the siege. Here is {MONEY}{GOLD_ICON}. Just take it and have mercy on us.", new ConversationSentence.OnConditionDelegate(conversation_settlement_bribe_on_condition), null, 100, null);
            starter.AddPlayerLine("", "settlement_do_bribe", "close_window", "{=SurrenderTweaks11}I will have mercy on you for now. You have escaped doom, but not for long.", null, new ConversationSentence.OnConsequenceDelegate(conversation_settlement_bribe_on_consequence), 100, null, null);
            starter.AddPlayerLine("", "settlement_do_bribe", "close_window", "{=SurrenderTweaks12}What a joke! You will stop starving when this settlement falls... or when you fall.", null, null, 100, null, null);
            starter.AddDialogLine("", "start", "close_window", "{=SurrenderTweaks13}We are out of food. We don't want to starve any longer. We yield.", new ConversationSentence.OnConditionDelegate(conversation_settlement_surrender_on_condition), delegate
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
            _bribeCooldown.Add(PlayerSiege.BesiegedSettlement, SurrenderTweaksHelper.Settings.SettlementBribeCooldownDays);
            typeof(SiegeEventCampaignBehavior).GetMethod("LeaveSiege", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, null);
        }

        // If the player accepts a settlement's surrender, capture the lords, capture all the troops in the settlement and capture all their trade items which do not belong to the settlement.
        // Capture the settlement.
        private void conversation_settlement_surrender_on_consequence()
        {
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
                    if (!troopRosterElement.Character.IsHero)
                    {
                        troopRoster.AddToCounts(troopRosterElement.Character, troopRosterElement.Number, false, 0, 0, true, -1);
                    }
                    else
                    {
                        TakePrisonerAction.Apply(PartyBase.MainParty, troopRosterElement.Character.HeroObject);
                    }
                }
                if (defender.MobileParty != null)
                {
                    DestroyPartyAction.Apply(PartyBase.MainParty, defender.MobileParty);
                }
            }
            dictionary.Add(PartyBase.MainParty, value);
            InventoryManager.OpenScreenAsLoot(dictionary);
            PartyScreenManager.OpenScreenAsLoot(TroopRoster.CreateDummyTroopRoster(), troopRoster, PlayerSiege.BesiegedSettlement.Party.Name, troopRoster.TotalManCount, null);
            PlayerEncounter.Init();
            PlayerEncounter.Current.SetupFields(PartyBase.MainParty, PlayerSiege.BesiegedSettlement.Party);
            PlayerEncounter.StartBattle();
            PlayerEncounter.Update();
        }

        // When the settlement requests a parley with the player, display a popup message.
        private void RequestParley() => InformationManager.ShowInquiry(new InquiryData(new TextObject("{=SurrenderTweaks14}Defenders request to parley").ToString(), new TextObject("{=SurrenderTweaks15}The defenders sound a horn and open the gates. A messenger rides out towards your camp and requests to parley.").ToString(), true, false, new TextObject("{=SurrenderTweaks16}OK", null).ToString(), "", new Action(AcceptParley), null, ""), true);

        // When the player accepts a parley, start a conversation with the settlement defenders.
        private void AcceptParley()
        {
            Campaign.Current.CurrentConversationContext = ConversationContext.Default;
            CampaignMapConversation.OpenConversation(new ConversationCharacterData(CharacterObject.PlayerCharacter, null, true, true, false, false), new ConversationCharacterData(PlayerSiege.BesiegedSettlement.MilitiaPartyComponent.Party.MemberRoster.GetCharacterAtIndex(0), PlayerSiege.BesiegedSettlement.MilitiaPartyComponent.Party, false, true, false, false));
        }
    }
}
