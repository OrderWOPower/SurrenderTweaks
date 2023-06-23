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
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Siege;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace SurrenderTweaks.Behaviors
{
    [HarmonyPatch(typeof(EncounterGameMenuBehavior), "game_menu_town_town_besiege_on_condition")]
    public class SettlementBribeAndSurrenderBehavior : CampaignBehaviorBase
    {
        private static Dictionary<Settlement, int> _bribeCooldown;

        private Dictionary<Settlement, int> _bribeCount, _surrenderCount, _starvationPenalty;

        private static void Postfix(MenuCallbackArgs args)
        {
            Settlement currentSettlement = Settlement.CurrentSettlement;

            if (_bribeCooldown.ContainsKey(currentSettlement))
            {
                MBTextManager.SetTextVariable("SETTLEMENT_BRIBE_COOLDOWN", _bribeCooldown[currentSettlement]);
                MBTextManager.SetTextVariable("PLURAL", _bribeCooldown[currentSettlement] > 1 ? 1 : 0);
                // Display the bribe cooldown's number of days in the option's tooltip.
                args.Tooltip = new TextObject("{=SurrenderTweaks09}You cannot attack this settlement for {SETTLEMENT_BRIBE_COOLDOWN} {?PLURAL}days{?}day{\\?}.", null);
                // Disable the option for besieging the settlement.
                args.IsEnabled = false;
            }
        }

        public SettlementBribeAndSurrenderBehavior()
        {
            _bribeCooldown = new Dictionary<Settlement, int>();
            _bribeCount = new Dictionary<Settlement, int>();
            _surrenderCount = new Dictionary<Settlement, int>();
            _starvationPenalty = new Dictionary<Settlement, int>();
        }

        public override void RegisterEvents()
        {
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, new Action<CampaignGameStarter>(OnSessionLaunched));
            CampaignEvents.OnSiegeEventStartedEvent.AddNonSerializedListener(this, new Action<SiegeEvent>(OnSiegeStarted));
            CampaignEvents.SiegeCompletedEvent.AddNonSerializedListener(this, new Action<Settlement, MobileParty, bool, MapEvent.BattleTypes>(OnSiegeCompleted));
            CampaignEvents.DailyTickSettlementEvent.AddNonSerializedListener(this, new Action<Settlement>(OnDailyTickSettlement));
            CampaignEvents.HourlyTickSettlementEvent.AddNonSerializedListener(this, new Action<Settlement>(OnHourlyTickSettlement));
        }

        public override void SyncData(IDataStore dataStore)
        {
            try
            {
                dataStore.SyncData("_settlementBribeCooldown", ref _bribeCooldown);
                dataStore.SyncData("_bribeCount", ref _bribeCount);
                dataStore.SyncData("_surrenderCount", ref _surrenderCount);
                dataStore.SyncData("_starvationPenalty", ref _starvationPenalty);
            }
            catch (Exception)
            {
                if (dataStore.IsLoading)
                {
                    InformationManager.DisplayMessage(new InformationMessage(MethodBase.GetCurrentMethod().DeclaringType.FullName + "." + MethodBase.GetCurrentMethod().Name + ": Error loading save file!"));
                }
            }
        }

        public void OnSessionLaunched(CampaignGameStarter campaignGameStarter) => AddDialogs(campaignGameStarter);

        public void OnSiegeStarted(SiegeEvent siegeEvent)
        {
            Settlement settlement = siegeEvent.BesiegedSettlement;

            if (!_bribeCount.ContainsKey(settlement))
            {
                _bribeCount.Add(settlement, 0);
            }

            if (!_surrenderCount.ContainsKey(settlement))
            {
                _surrenderCount.Add(settlement, 0);
            }

            if (!_starvationPenalty.ContainsKey(settlement))
            {
                // Add a starvation penalty to the besieged settlement.
                _starvationPenalty.Add(settlement, 0);
            }
        }

        public void OnSiegeCompleted(Settlement settlement, MobileParty capturerParty, bool isWin, MapEvent.BattleTypes battleType)
        {
            if (isWin)
            {
                _bribeCooldown.Remove(settlement);
                _bribeCount.Remove(settlement);
                _surrenderCount.Remove(settlement);
                _starvationPenalty.Remove(settlement);
            }
        }

        public void OnDailyTickSettlement(Settlement settlement)
        {
            if (_bribeCooldown.ContainsKey(settlement))
            {
                // If a settlement has a bribe cooldown, decrease the bribe cooldown by 1 day.
                _bribeCooldown[settlement]--;

                if (_bribeCooldown[settlement] <= 0)
                {
                    // If a settlement's bribe cooldown is 0 days, remove the bribe cooldown.
                    _bribeCooldown.Remove(settlement);
                }
            }

            if (settlement.SiegeEvent == null)
            {
                if (!_bribeCooldown.ContainsKey(settlement))
                {
                    _bribeCount.Remove(settlement);
                    _surrenderCount.Remove(settlement);
                }

                // If a settlement is no longer under siege, remove its starvation penalty.
                _starvationPenalty.Remove(settlement);
            }
            else
            {
                if (_bribeCount.ContainsKey(settlement) && _surrenderCount.ContainsKey(settlement) && _starvationPenalty.ContainsKey(settlement))
                {
                    MobileParty attacker = settlement.SiegeEvent.BesiegerCamp.BesiegerParty;
                    SurrenderEvent surrenderEvent = SurrenderEvent.PlayerSurrenderEvent;
                    ValueTuple<int, int> townFoodAndMarketStocks = TownHelpers.GetTownFoodAndMarketStocks(settlement.Town);
                    float totalFood = townFoodAndMarketStocks.Item1 + townFoodAndMarketStocks.Item2, foodChange = settlement.Town.FoodChangeWithoutMarketStocks;
                    int daysUntilNoFood = MathF.Ceiling(MathF.Abs(totalFood / foodChange));

                    if (attacker.IsMainParty)
                    {
                        surrenderEvent.SetBribeOrSurrender(settlement.MilitiaPartyComponent?.MobileParty, attacker, daysUntilNoFood, _starvationPenalty[settlement]);

                        if (!InformationManager.IsAnyInquiryActive())
                        {
                            // If the settlement is willing to offer a bribe or surrender to the player, make them request a parley with the player.
                            if (surrenderEvent.IsBribeFeasible && !surrenderEvent.IsSurrenderFeasible && _bribeCount[settlement] == 0)
                            {
                                RequestParley();
                                _bribeCount[settlement]++;
                            }
                            else if (surrenderEvent.IsSurrenderFeasible && _surrenderCount[settlement] == 0)
                            {
                                RequestParley();
                                _surrenderCount[settlement]++;
                            }
                        }
                    }
                    else if (!settlement.SiegeEvent.IsPlayerSiegeEvent && settlement.Party.MapEvent == null && SurrenderHelper.IsBribeOrSurrenderFeasible(settlement.MilitiaPartyComponent?.MobileParty, attacker, daysUntilNoFood, _starvationPenalty[settlement], true))
                    {
                        foreach (PartyBase defender in settlement.GetInvolvedPartiesForEventType(MapEvent.BattleTypes.Siege).ToList())
                        {
                            if (defender != settlement.Party)
                            {
                                // Capture the trade items which do not belong to the settlement.
                                attacker.ItemRoster.Add(defender.ItemRoster);
                                defender.ItemRoster.Clear();
                                SurrenderHelper.AddPrisonersAsCasualties(attacker, defender.MobileParty);
                            }

                            foreach (TroopRosterElement troopRosterElement in defender.MemberRoster.GetTroopRoster().ToList())
                            {
                                if (!troopRosterElement.Character.IsHero)
                                {
                                    // Capture the troops.
                                    attacker.PrisonRoster.AddToCounts(troopRosterElement.Character, troopRosterElement.Number, false, 0, 0, true, -1);
                                }
                                else
                                {
                                    // Capture the lords.
                                    TakePrisonerAction.Apply(attacker.Party, troopRosterElement.Character.HeroObject);
                                }
                            }

                            defender.MemberRoster.Clear();
                        }

                        // Capture the settlement.
                        settlement.SiegeEvent.BesiegerCamp.SiegeEngines.SiegePreparations.SetProgress(1f);
                    }
                }
            }
        }

        public void OnHourlyTickSettlement(Settlement settlement)
        {
            if (settlement.SiegeEvent != null && _starvationPenalty.ContainsKey(settlement))
            {
                if (!SettlementHelper.IsGarrisonStarving(settlement))
                {
                    _starvationPenalty[settlement] = 0;
                }
                else
                {
                    // If a settlement has no food, increase its starvation penalty.
                    _starvationPenalty[settlement] += 4;
                }
            }
        }

        private void AddDialogs(CampaignGameStarter starter)
        {
            // Add dialog lines for a settlement offering a bribe or surrender.
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
            MBTextManager.SetTextVariable("MONEY", SurrenderHelper.GetBribeAmount(MobileParty.ConversationParty, PlayerSiege.BesiegedSettlement));

            return SurrenderEvent.PlayerSurrenderEvent.IsBribeFeasible && !SurrenderEvent.PlayerSurrenderEvent.IsSurrenderFeasible && MobileParty.ConversationParty != null && MobileParty.ConversationParty.IsMilitia;
        }

        private bool conversation_settlement_surrender_on_condition() => SurrenderEvent.PlayerSurrenderEvent.IsSurrenderFeasible && MobileParty.ConversationParty != null && MobileParty.ConversationParty.IsMilitia;

        private void conversation_settlement_bribe_on_consequence()
        {
            // Transfer the bribe amount from the settlement to the player.
            GiveGoldAction.ApplyForSettlementToCharacter(PlayerSiege.BesiegedSettlement, Hero.MainHero, SurrenderHelper.GetBribeAmount(MobileParty.ConversationParty, PlayerSiege.BesiegedSettlement), false);

            // Add a bribe cooldown to the settlement.
            _bribeCooldown.Add(PlayerSiege.BesiegedSettlement, SurrenderTweaksSettings.Instance.SettlementBribeCooldownDays);

            // Break the siege.
            AccessTools.Method(typeof(SiegeEventCampaignBehavior), "LeaveSiege").Invoke(null, null);
        }

        private void conversation_settlement_surrender_on_consequence()
        {
            Settlement settlement = PlayerSiege.BesiegedSettlement;
            Dictionary<PartyBase, ItemRoster> dictionary = new Dictionary<PartyBase, ItemRoster>();
            ItemRoster value = new ItemRoster();
            TroopRoster troopRoster = TroopRoster.CreateDummyTroopRoster();

            foreach (PartyBase defender in settlement.GetInvolvedPartiesForEventType(MapEvent.BattleTypes.Siege).ToList())
            {
                if (defender != settlement.Party)
                {
                    // Capture the trade items which do not belong to the settlement.
                    value.Add(defender.ItemRoster);
                    defender.ItemRoster.Clear();
                    SurrenderHelper.AddPrisonersAsCasualties(MobileParty.MainParty, defender.MobileParty);
                }

                foreach (TroopRosterElement troopRosterElement in defender.MemberRoster.GetTroopRoster())
                {
                    if (!troopRosterElement.Character.IsHero)
                    {
                        // Capture the troops.
                        troopRoster.AddToCounts(troopRosterElement.Character, troopRosterElement.Number, false, 0, 0, true, -1);
                    }
                    else
                    {
                        // Capture the lords.
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
            PartyScreenManager.OpenScreenAsLoot(TroopRoster.CreateDummyTroopRoster(), troopRoster, settlement.Party.Name, troopRoster.TotalManCount, null);
            // Capture the settlement.
            PlayerEncounter.Init();
            PlayerEncounter.Current.SetupFields(PartyBase.MainParty, settlement.Party);
            PlayerEncounter.StartBattle();
            PlayerEncounter.Update();
        }

        // When the settlement requests a parley with the player, display a popup message.
        private void RequestParley() => InformationManager.ShowInquiry(new InquiryData(new TextObject("{=SurrenderTweaks14}Defenders request to parley").ToString(), new TextObject("{=SurrenderTweaks15}The defenders sound a horn and open the gates. A messenger rides out towards your camp and requests to parley.").ToString(), true, false, new TextObject("{=SurrenderTweaks16}OK", null).ToString(), "", new Action(AcceptParley), null, ""), true);

        private void AcceptParley()
        {
            Campaign.Current.CurrentConversationContext = ConversationContext.Default;
            // When the player accepts a parley, start a conversation with the settlement defenders.
            CampaignMapConversation.OpenConversation(new ConversationCharacterData(CharacterObject.PlayerCharacter, null, true, true, false, false), new ConversationCharacterData(PlayerSiege.BesiegedSettlement.Culture.MeleeEliteMilitiaTroop, PlayerSiege.BesiegedSettlement.MilitiaPartyComponent.Party, false, true, false, false));
        }
    }
}
