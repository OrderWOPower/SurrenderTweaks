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
    [HarmonyPatch(typeof(LordConversationsCampaignBehavior), "AddOtherConversations")]
    public class LordBribeAndSurrenderBehavior : CampaignBehaviorBase
    {
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) => instructions.MethodReplacer(AccessTools.Method(typeof(CampaignGameStarter), "AddPlayerLine"), AccessTools.Method(typeof(LordBribeAndSurrenderBehavior), "AddPlayerLine"));
        public override void RegisterEvents()
        {
            CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, new Action(OnDailyTick));
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, new Action<CampaignGameStarter>(OnSessionLaunched));
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
        public void OnSessionLaunched(CampaignGameStarter campaignGameStarter) => AddDialogs(campaignGameStarter);
        // Add dialog lines for a lord offering a bribe or surrender.
        protected void AddDialogs(CampaignGameStarter starter)
        {
            starter.AddDialogLine("", "party_encounter_lord_hostile_attacker_3", "lord_do_bribe", "I can pay you for my safe passage. Here is {MONEY}{GOLD_ICON}. Just take it and let me and my troops go.[if:idle_angry][ib:nervous]", new ConversationSentence.OnConditionDelegate(conversation_lord_bribe_on_condition), null, 100, null);
            starter.AddPlayerLine("", "lord_do_bribe", "close_window", "A wise choice for you and your troops. You are free to go.", null, new ConversationSentence.OnConsequenceDelegate(conversation_lord_bribe_on_consequence), 100, null, null);
            starter.AddPlayerLine("", "lord_do_bribe", "player_wants_prisoners", "None of you are going anywhere. I want prisoners.", null, null, 100, null, null);
            starter.AddDialogLine("", "player_wants_prisoners", "close_window", "I can't fight you. I yield. I am at your mercy.", new ConversationSentence.OnConditionDelegate(conversation_lord_surrender_on_condition), delegate
            {
                Campaign.Current.ConversationManager.ConversationEndOneShot += conversation_lord_surrender_on_consequence;
            }, 100, null);
            starter.AddDialogLine("", "player_wants_prisoners", "close_window", "I would rather fight than be taken prisoner.[if:idle_angry][ib:warrior]", null, null, 100, null);
        }
        // Add a clickable condition to the dialog line for attacking a lord.
        public static ConversationSentence AddPlayerLine(CampaignGameStarter instance, string id, string inputToken, string outputToken, string text, ConversationSentence.OnConditionDelegate conditionDelegate, ConversationSentence.OnConsequenceDelegate consequenceDelegate, int priority = 100, ConversationSentence.OnClickableConditionDelegate clickableConditionDelegate = null, ConversationSentence.OnPersuasionOptionDelegate persuasionOptionDelegate = null)
        {
            if (text == "{=ddhr2Xa3}I don't care. Yield or fight!")
            {
                return instance.AddPlayerLine(id, inputToken, outputToken, text, conditionDelegate, consequenceDelegate, priority, new ConversationSentence.OnClickableConditionDelegate(conversation_player_threats_lord_verify_on_clickable_condition), persuasionOptionDelegate);
            }
            return instance.AddPlayerLine(id, inputToken, outputToken, text, conditionDelegate, consequenceDelegate, priority, clickableConditionDelegate, persuasionOptionDelegate);
        }
        // If a party has a bribe cooldown, disable the option for attacking the party. Display the bribe cooldown's number of days in the option's tooltip.
        private static bool conversation_player_threats_lord_verify_on_clickable_condition(out TextObject explanation)
        {
            MobileParty conversationParty = MobileParty.ConversationParty;
            if (_bribeCooldown.ContainsKey(conversationParty))
            {
                MBTextManager.SetTextVariable("LORD_BRIBE_COOLDOWN", _bribeCooldown[conversationParty]);
                MBTextManager.SetTextVariable("PLURAL", (_bribeCooldown[conversationParty] > 1) ? 1 : 0);
                explanation = new TextObject("You cannot attack this party for {LORD_BRIBE_COOLDOWN} {?PLURAL}days{?}day{\\?}.", null);
                return false;
            }
            explanation = TextObject.Empty;
            return true;
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
                if (troopRosterElement.Character.HeroObject == null)
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
