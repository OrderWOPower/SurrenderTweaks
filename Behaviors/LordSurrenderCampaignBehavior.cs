﻿using HarmonyLib;
using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Conversation;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.Inventory;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace SurrenderTweaks.Behaviors
{
    [HarmonyPatch(typeof(LordConversationsCampaignBehavior), "conversation_player_can_attack_hero_on_clickable_condition")]
    public class LordSurrenderCampaignBehavior : CampaignBehaviorBase
    {
        private static Dictionary<MobileParty, CampaignTime> _bribeTimes;

        private static void Postfix(ref bool __result, ref TextObject hint)
        {
            if (_bribeTimes.TryGetValue(MobileParty.ConversationParty, out CampaignTime bribeTime) && MobileParty.ConversationParty.BesiegedSettlement?.OwnerClan != Clan.PlayerClan)
            {
                int cooldownDays = SurrenderTweaksSettings.Instance.LordBribeCooldownDays - (int)(CampaignTime.Now - bribeTime).ToDays;

                if (cooldownDays > 0)
                {
                    MBTextManager.SetTextVariable("LORD_BRIBE_COOLDOWN", cooldownDays);
                    MBTextManager.SetTextVariable("PLURAL", cooldownDays > 1 ? 1 : 0);
                    // Display the bribe cooldown's number of days in the option's tooltip.
                    hint = new TextObject("{=SurrenderTweaks03}You cannot attack this party for {LORD_BRIBE_COOLDOWN} {?PLURAL}days{?}day{\\?}.", null);

                    // Disable the option for attacking the party.
                    __result = false;
                }
            }
        }

        public LordSurrenderCampaignBehavior() => _bribeTimes = new Dictionary<MobileParty, CampaignTime>();

        public override void RegisterEvents()
        {
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, new Action<CampaignGameStarter>(OnSessionLaunched));
            CampaignEvents.DailyTickPartyEvent.AddNonSerializedListener(this, new Action<MobileParty>(OnDailyTickParty));
        }

        public override void SyncData(IDataStore dataStore)
        {
            try
            {
                dataStore.SyncData("_lordBribeTimes", ref _bribeTimes);
            }
            catch (Exception ex)
            {
                InformationManager.DisplayMessage(new InformationMessage(ex.ToString()));
            }
        }

        private void OnSessionLaunched(CampaignGameStarter campaignGameStarter) => AddDialogs(campaignGameStarter);

        private void OnDailyTickParty(MobileParty party)
        {
            if (_bribeTimes.TryGetValue(party, out CampaignTime bribeTime) && (CampaignTime.Now - bribeTime).ToDays >= SurrenderTweaksSettings.Instance.LordBribeCooldownDays)
            {
                _bribeTimes.Remove(party);
            }
        }

        private void AddDialogs(CampaignGameStarter starter)
        {
            // Add dialog lines for a lord offering a bribe or surrender.
            starter.AddDialogLine("", "party_encounter_lord_hostile_attacker_3", "lord_do_bribe", "{=SurrenderTweaks04}I can pay you for my safe passage. Here is {MONEY}{GOLD_ICON}. Just take it and let me and my troops go.[if:idle_angry][ib:nervous]", new ConversationSentence.OnConditionDelegate(conversation_lord_bribe_on_condition), null, 100, null);
            starter.AddDialogLine("", "party_encounter_lord_hostile_attacker_3", "close_window", "{=SurrenderTweaks07}I can't fight you. I yield. I am at your mercy.[if:idle_angry][ib:nervous]", new ConversationSentence.OnConditionDelegate(conversation_lord_surrender_on_condition), delegate
            {
                Campaign.Current.ConversationManager.ConversationEndOneShot += conversation_lord_surrender_on_consequence;
            }, 100, null);
            starter.AddPlayerLine("", "lord_do_bribe", "close_window", "{=SurrenderTweaks05}A wise choice for you and your troops. You are free to go.", null, new ConversationSentence.OnConsequenceDelegate(conversation_lord_bribe_on_consequence), 100, null, null);
            starter.AddPlayerLine("", "lord_do_bribe", "player_wants_prisoners", "{=SurrenderTweaks06}None of you are going anywhere. I want prisoners.", null, null, 100, null, null);
            starter.AddDialogLine("", "player_wants_prisoners", "close_window", "{=SurrenderTweaks07}I can't fight you. I yield. I am at your mercy.[if:idle_angry][ib:nervous]", new ConversationSentence.OnConditionDelegate(conversation_lord_surrender_on_condition), delegate
            {
                Campaign.Current.ConversationManager.ConversationEndOneShot += conversation_lord_surrender_on_consequence;
            }, 100, null);
            starter.AddDialogLine("", "player_wants_prisoners", "close_window", "{=SurrenderTweaks08}I would rather fight than be taken prisoner.[if:idle_angry][ib:warrior]", null, null, 100, null);
            starter.AddDialogLine("", "attack_disbanding_enemy_response", "close_window", "{=SurrenderTweaks07}I can't fight you. I yield. I am at your mercy.[if:idle_angry][ib:nervous]", new ConversationSentence.OnConditionDelegate(conversation_lord_surrender_on_condition), delegate
            {
                Campaign.Current.ConversationManager.ConversationEndOneShot += conversation_lord_surrender_on_consequence;
            }, 100, null);
        }

        private bool conversation_lord_bribe_on_condition()
        {
            MBTextManager.SetTextVariable("MONEY", SurrenderHelper.GetBribeAmount(MobileParty.ConversationParty, null));

            return SurrenderEvent.PlayerSurrenderEvent.IsBribeFeasible && MobileParty.ConversationParty.MapEvent == null && MobileParty.ConversationParty.SiegeEvent == null && MobileParty.ConversationParty.Army == null;
        }

        private bool conversation_lord_surrender_on_condition() => SurrenderEvent.PlayerSurrenderEvent.IsSurrenderFeasible;

        private void conversation_lord_bribe_on_consequence()
        {
            // Transfer the bribe amount from the lord to the player.
            GiveGoldAction.ApplyBetweenCharacters(MobileParty.ConversationParty.LeaderHero, Hero.MainHero, SurrenderHelper.GetBribeAmount(MobileParty.ConversationParty, null), false);

            if (!_bribeTimes.TryGetValue(MobileParty.ConversationParty, out _))
            {
                // Add a bribe cooldown to the party.
                _bribeTimes.Add(MobileParty.ConversationParty, CampaignTime.Now);
            }
            else
            {
                _bribeTimes[MobileParty.ConversationParty] = CampaignTime.Now;
            }

            PlayerEncounter.LeaveEncounter = true;
        }

        private void conversation_lord_surrender_on_consequence()
        {
            PartyBase defender = PlayerEncounter.EncounteredParty;
            Dictionary<PartyBase, ItemRoster> dictionary = new Dictionary<PartyBase, ItemRoster>();
            ItemRoster value = new ItemRoster(defender.ItemRoster);
            TroopRoster troopRoster = TroopRoster.CreateDummyTroopRoster();

            defender.ItemRoster.Clear();
            SurrenderHelper.AddPrisonersAsCasualties(MobileParty.MainParty, defender.MobileParty);

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

            DestroyPartyAction.Apply(PartyBase.MainParty, defender.MobileParty);
            // Capture the trade items.
            dictionary.Add(PartyBase.MainParty, value);
            InventoryManager.OpenScreenAsLoot(dictionary);
            PartyScreenManager.OpenScreenAsLoot(TroopRoster.CreateDummyTroopRoster(), troopRoster, defender.Name, troopRoster.TotalManCount, null);
            PlayerEncounter.LeaveEncounter = true;
        }
    }
}
