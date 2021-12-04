using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.Core;
using TaleWorlds.Localization;
using SandBox;

namespace SurrenderTweaks.Behaviors
{
    [HarmonyPatch(typeof(LordConversationsCampaignBehavior), "conversation_player_can_attack_hero_on_clickable_condition")]
    public class LordBribeAndSurrenderBehavior : CampaignBehaviorBase
    {
        // If a party has a bribe cooldown, disable the option for attacking the party. Display the bribe cooldown's number of days in the option's tooltip.
        private static void Postfix(ref bool __result, ref TextObject hint)
        {
            MobileParty conversationParty = MobileParty.ConversationParty;
            if (_bribeCooldown.ContainsKey(conversationParty) && conversationParty.BesiegedSettlement?.OwnerClan != Clan.PlayerClan)
            {
                MBTextManager.SetTextVariable("LORD_BRIBE_COOLDOWN", _bribeCooldown[conversationParty]);
                MBTextManager.SetTextVariable("PLURAL", (_bribeCooldown[conversationParty] > 1) ? 1 : 0);
                hint = new TextObject("{=SurrenderTweaks03}You cannot attack this party for {LORD_BRIBE_COOLDOWN} {?PLURAL}days{?}day{\\?}.", null);
                __result = false;
            }
        }
        public override void RegisterEvents()
        {
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, new Action<CampaignGameStarter>(OnSessionLaunched));
            CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, new Action(OnDailyTick));
        }
        public override void SyncData(IDataStore dataStore)
        {
            try
            {
                dataStore.SyncData("_lordBribeCooldown", ref _bribeCooldown);
            }
            catch (Exception ex)
            {
                InformationManager.DisplayMessage(new InformationMessage("Exception at LordBribeAndSurrenderBehavior.SyncData(): " + ex.Message));
            }
        }
        public void OnSessionLaunched(CampaignGameStarter campaignGameStarter) => AddDialogs(campaignGameStarter);
        // If a party has a bribe cooldown, decrease the bribe cooldown by 1 day.
        // If a party's bribe cooldown is 0 days, remove its bribe cooldown.
        public void OnDailyTick()
        {
            foreach (MobileParty mobileParty in _bribeCooldown.Keys.ToList())
            {
                _bribeCooldown[mobileParty]--;
                if (_bribeCooldown[mobileParty] == 0)
                {
                    _bribeCooldown.Remove(mobileParty);
                }
            }
        }
        // Add dialog lines for a lord offering a bribe or surrender.
        protected void AddDialogs(CampaignGameStarter starter)
        {
            starter.AddDialogLine("", "party_encounter_lord_hostile_attacker_3", "lord_do_bribe", "{=SurrenderTweaks04}I can pay you for my safe passage. Here is {MONEY}{GOLD_ICON}. Just take it and let me and my troops go.[if:idle_angry][ib:nervous]", new ConversationSentence.OnConditionDelegate(conversation_lord_bribe_on_condition), null, 100, null);
            starter.AddPlayerLine("", "lord_do_bribe", "close_window", "{=SurrenderTweaks05}A wise choice for you and your troops. You are free to go.", null, new ConversationSentence.OnConsequenceDelegate(conversation_lord_bribe_on_consequence), 100, null, null);
            starter.AddPlayerLine("", "lord_do_bribe", "player_wants_prisoners", "{=SurrenderTweaks06}None of you are going anywhere. I want prisoners.", null, null, 100, null, null);
            starter.AddDialogLine("", "player_wants_prisoners", "close_window", "{=SurrenderTweaks07}I can't fight you. I yield. I am at your mercy.[if:idle_angry][ib:nervous]", new ConversationSentence.OnConditionDelegate(conversation_lord_surrender_on_condition), delegate
            {
                Campaign.Current.ConversationManager.ConversationEndOneShot += conversation_lord_surrender_on_consequence;
            }, 100, null);
            starter.AddDialogLine("", "player_wants_prisoners", "close_window", "{=SurrenderTweaks08}I would rather fight than be taken prisoner.[if:idle_angry][ib:warrior]", null, null, 100, null);
        }
        private bool conversation_lord_bribe_on_condition()
        {
            SurrenderTweaksHelper.BribeAmount(MobileParty.ConversationParty, null, out int num);
            MBTextManager.SetTextVariable("MONEY", num);
            return SurrenderTweaksHelper.IsBribeFeasible;
        }
        private bool conversation_lord_surrender_on_condition() => SurrenderTweaksHelper.IsSurrenderFeasible;
        // If the player accepts a lord's bribe, transfer the bribe amount from the lord to the player. Add a bribe cooldown to the party and set it to 10 days.
        private void conversation_lord_bribe_on_consequence()
        {
            SurrenderTweaksHelper.BribeAmount(MobileParty.ConversationParty, null, out int num);
            GiveGoldAction.ApplyBetweenCharacters(MobileParty.ConversationParty.LeaderHero, Hero.MainHero, num, false);
            _bribeCooldown.Add(MobileParty.ConversationParty, 10);
            PlayerEncounter.LeaveEncounter = true;
        }
        // If the player accepts a lord's surrender, capture the lord, capture all the troops in the party and capture all their trade items.
        private void conversation_lord_surrender_on_consequence()
        {
            PartyBase defender = PlayerEncounter.EncounteredParty;
            Dictionary<PartyBase, ItemRoster> dictionary = new Dictionary<PartyBase, ItemRoster>();
            ItemRoster value = new ItemRoster(defender.ItemRoster);
            TroopRoster troopRoster = TroopRoster.CreateDummyTroopRoster();
            defender.ItemRoster.Clear();
            foreach (TroopRosterElement troopRosterElement in defender.MemberRoster.GetTroopRoster())
            {
                if (!troopRosterElement.Character.IsHero)
                {
                    troopRoster.AddToCounts(troopRosterElement.Character, troopRosterElement.Number, false, 0, 0, true, -1);
                }
            }
            TakePrisonerAction.Apply(PartyBase.MainParty, defender.LeaderHero);
            DestroyPartyAction.Apply(PartyBase.MainParty, defender.MobileParty);
            dictionary.Add(PartyBase.MainParty, value);
            InventoryManager.OpenScreenAsLoot(dictionary);
            PartyScreenManager.OpenScreenAsLoot(TroopRoster.CreateDummyTroopRoster(), troopRoster, defender.Name, troopRoster.TotalManCount, null);
            PlayerEncounter.LeaveEncounter = true;
        }
        private static Dictionary<MobileParty, int> _bribeCooldown = new Dictionary<MobileParty, int>();
    }
}
