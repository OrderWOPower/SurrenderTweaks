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
    public class SettlementSurrenderCampaignBehavior : CampaignBehaviorBase
    {
        private static Dictionary<Settlement, CampaignTime> _bribeTimes;

        private Dictionary<Settlement, int> _starvationPenalties, _parleyCounts;
        private List<Settlement> _besiegedSettlements;

        private static void Postfix(MenuCallbackArgs args)
        {
            if (_bribeTimes.TryGetValue(Settlement.CurrentSettlement, out CampaignTime bribeTime))
            {
                int cooldownDays = SurrenderTweaksSettings.Instance.SettlementBribeCooldownDays - (int)(CampaignTime.Now - bribeTime).ToDays;

                if (cooldownDays > 0)
                {
                    MBTextManager.SetTextVariable("SETTLEMENT_BRIBE_COOLDOWN", cooldownDays);
                    MBTextManager.SetTextVariable("PLURAL", cooldownDays > 1 ? 1 : 0);
                    // Display the bribe cooldown's number of days in the option's tooltip.
                    args.Tooltip = new TextObject("{=SurrenderTweaks09}You cannot attack this settlement for {SETTLEMENT_BRIBE_COOLDOWN} {?PLURAL}days{?}day{\\?}.", null);
                    // Disable the option for besieging the settlement.
                    args.IsEnabled = false;
                }
            }
        }

        public SettlementSurrenderCampaignBehavior()
        {
            _bribeTimes = new Dictionary<Settlement, CampaignTime>();
            _starvationPenalties = new Dictionary<Settlement, int>();
            _parleyCounts = new Dictionary<Settlement, int>();
            _besiegedSettlements = new List<Settlement>();
        }

        public override void RegisterEvents()
        {
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, new Action<CampaignGameStarter>(OnSessionLaunched));
            CampaignEvents.OnGameLoadedEvent.AddNonSerializedListener(this, new Action<CampaignGameStarter>(OnGameLoaded));
            CampaignEvents.OnSiegeEventStartedEvent.AddNonSerializedListener(this, new Action<SiegeEvent>(OnSiegeStarted));
            CampaignEvents.SiegeCompletedEvent.AddNonSerializedListener(this, new Action<Settlement, MobileParty, bool, MapEvent.BattleTypes>(OnSiegeCompleted));
            CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, new Action(OnDailyTick));
            CampaignEvents.HourlyTickEvent.AddNonSerializedListener(this, new Action(OnHourlyTick));
        }

        public override void SyncData(IDataStore dataStore)
        {
            try
            {
                dataStore.SyncData("_settlementBribeTimes", ref _bribeTimes);
                dataStore.SyncData("_starvationPenalties", ref _starvationPenalties);
                dataStore.SyncData("_parleyCounts", ref _parleyCounts);
            }
            catch (Exception)
            {
                if (dataStore.IsLoading)
                {
                    InformationManager.DisplayMessage(new InformationMessage(MethodBase.GetCurrentMethod().DeclaringType.FullName + "." + MethodBase.GetCurrentMethod().Name + ": Error loading save file!"));
                }
            }
        }

        private void OnSessionLaunched(CampaignGameStarter campaignGameStarter) => AddDialogs(campaignGameStarter);

        private void OnGameLoaded(CampaignGameStarter campaignGameStarter) => _besiegedSettlements = _starvationPenalties.Keys.ToList();

        private void OnSiegeStarted(SiegeEvent siegeEvent)
        {
            Settlement settlement = siegeEvent.BesiegedSettlement;

            if (!_starvationPenalties.TryGetValue(settlement, out _))
            {
                // Add a starvation penalty to the besieged settlement.
                _starvationPenalties.Add(settlement, 0);
                _parleyCounts.Add(settlement, 0);
                _besiegedSettlements.Add(settlement);
            }
        }

        private void OnSiegeCompleted(Settlement settlement, MobileParty capturerParty, bool isWin, MapEvent.BattleTypes battleType)
        {
            if (isWin)
            {
                _bribeTimes.Remove(settlement);
                _starvationPenalties.Remove(settlement);
                _parleyCounts.Remove(settlement);
                _besiegedSettlements.Remove(settlement);
            }
        }

        private void OnDailyTick()
        {
            foreach (KeyValuePair<Settlement, CampaignTime> keyValuePair in _bribeTimes.ToList().Where(p => (CampaignTime.Now - p.Value).ToDays >= SurrenderTweaksSettings.Instance.SettlementBribeCooldownDays))
            {
                _bribeTimes.Remove(keyValuePair.Key);
            }

            foreach (KeyValuePair<Settlement, int> keyValuePair in _starvationPenalties.ToList().Where(p => !p.Key.IsUnderSiege && p.Value <= 0))
            {
                // If a settlement is no longer under siege, remove its starvation penalty.
                _starvationPenalties.Remove(keyValuePair.Key);
                _parleyCounts.Remove(keyValuePair.Key);
                _besiegedSettlements.Remove(keyValuePair.Key);
            }
        }

        private void OnHourlyTick()
        {
            foreach (Settlement settlement in _besiegedSettlements)
            {
                if (settlement.IsUnderSiege)
                {
                    MobileParty attacker = settlement.SiegeEvent.BesiegerCamp.LeaderParty;
                    SurrenderEvent surrenderEvent = SurrenderEvent.PlayerSurrenderEvent;
                    ValueTuple<int, int> townFoodAndMarketStocks = TownHelpers.GetTownFoodAndMarketStocks(settlement.Town);
                    float totalFood = townFoodAndMarketStocks.Item1 + townFoodAndMarketStocks.Item2, foodChange = settlement.Town.FoodChangeWithoutMarketStocks;
                    int daysUntilNoFood = MathF.Ceiling(MathF.Abs(totalFood / foodChange));

                    if (SettlementHelper.IsGarrisonStarving(settlement))
                    {
                        // If a settlement has no food, increase its starvation penalty.
                        _starvationPenalties[settlement]++;
                    }

                    if (attacker.IsMainParty)
                    {
                        surrenderEvent.SetBribeOrSurrender(settlement.MilitiaPartyComponent?.MobileParty, attacker, daysUntilNoFood, _starvationPenalties[settlement]);

                        if (!InformationManager.IsAnyInquiryActive())
                        {
                            // If the settlement is willing to offer a bribe or surrender to the player, make them request a parley with the player.
                            if (surrenderEvent.IsBribeFeasible && !surrenderEvent.IsSurrenderFeasible && _parleyCounts[settlement] < 1)
                            {
                                RequestParley();
                                _parleyCounts[settlement] = 1;
                            }
                            else if (surrenderEvent.IsSurrenderFeasible && _parleyCounts[settlement] < 2)
                            {
                                RequestParley();
                                _parleyCounts[settlement] = 2;
                            }
                        }
                    }
                    else if (!settlement.SiegeEvent.IsPlayerSiegeEvent && settlement.Party.MapEvent == null && SurrenderHelper.IsBribeOrSurrenderFeasible(settlement.MilitiaPartyComponent?.MobileParty, attacker, daysUntilNoFood, _starvationPenalties[settlement], true))
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
                else
                {
                    if (!SettlementHelper.IsGarrisonStarving(settlement) && _starvationPenalties[settlement] > 0)
                    {
                        _starvationPenalties[settlement]--;
                    }
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
            _bribeTimes.Add(PlayerSiege.BesiegedSettlement, CampaignTime.Now);

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
