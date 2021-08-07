using System.Collections.Generic;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using SandBox;

namespace SurrenderTweaks.Behaviors
{
    [HarmonyPatch(typeof(LordConversationsCampaignBehavior), "AddHeroGeneralConversations")]
    public class LordBribeAndSurrenderBehavior
    {
        // Add dialog lines for a lord offering a bribe or surrender.
        private static void Prefix(CampaignGameStarter starter)
        {
            starter.AddDialogLine("", "party_encounter_lord_hostile_attacker_3", "lord_do_bribe", "I can pay you for my safe passage. Here is {MONEY}{GOLD_ICON}. Just take it and let me and my troops go. I will disband my party.[if:idle_angry][ib:nervous]", new ConversationSentence.OnConditionDelegate(conversation_lord_bribe_on_condition), null, 100, null);
            starter.AddPlayerLine("", "lord_do_bribe", "close_window", "A wise choice for you and your troops. You are free to go.", null, delegate
            {
                MobileParty party = MobileParty.ConversationParty;
                Campaign.Current.ConversationManager.ConversationEndOneShot += delegate
                {
                    conversation_lord_bribe_on_consequence(party);
                };
            }, 100, null, null);
            starter.AddPlayerLine("", "lord_do_bribe", "player_wants_prisoners", "None of you are going anywhere. I want prisoners.", null, null, 100, null, null);
            starter.AddDialogLine("", "player_wants_prisoners", "close_window", "I can't fight you. I yield. I am at your mercy.", new ConversationSentence.OnConditionDelegate(conversation_lord_surrender_on_condition), delegate
            {
                MobileParty party = MobileParty.ConversationParty;
                Campaign.Current.ConversationManager.ConversationEndOneShot += delegate
                {
                    conversation_lord_surrender_on_consequence(party);
                };
            }, 100, null);
            starter.AddDialogLine("", "player_wants_prisoners", "close_window", "I would rather fight than be taken prisoner.[if:idle_angry][ib:warrior]", null, null, 100, null);
        }
        private static bool conversation_lord_bribe_on_condition() => SurrenderTweaksHelper.IsBribeFeasible;
        private static bool conversation_lord_surrender_on_condition() => SurrenderTweaksHelper.IsSurrenderFeasible;
        // If the player accepts a lord's bribe, transfer the bribe amount from the lord to the player and disband the lord's party.
        private static void conversation_lord_bribe_on_consequence(MobileParty defender)
        {
            GiveGoldAction.ApplyBetweenCharacters(defender.LeaderHero, Hero.MainHero, SurrenderTweaksHelper.BribeAmount(), false);
            DisbandPartyAction.ApplyDisband(defender);
            PlayerEncounter.LeaveEncounter = true;
        }
        // If the player accepts a lord's surrender, capture the lord, capture all the troops in the party and capture all their trade items.
        private static void conversation_lord_surrender_on_consequence(MobileParty defender)
        {
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
            DestroyPartyAction.Apply(PartyBase.MainParty, defender);
            dictionary.Add(PartyBase.MainParty, value);
            InventoryManager.OpenScreenAsLoot(dictionary);
            PartyScreenManager.OpenScreenAsLoot(TroopRoster.CreateDummyTroopRoster(), troopRoster, defender.Name, troopRoster.TotalManCount, null);
            PlayerEncounter.LeaveEncounter = true;
        }
    }
}
