using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
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
        private static Dictionary<MobileParty, CampaignTime> _bribeCooldowns;

        private static void Postfix(ref bool __result, ref TextObject hint)
        {
            MobileParty conversationParty = MobileParty.ConversationParty;

            if (_bribeCooldowns.ContainsKey(conversationParty) && conversationParty.BesiegedSettlement?.OwnerClan != Clan.PlayerClan)
            {
                int bribeCooldown = SurrenderTweaksSettings.Instance.LordBribeCooldownDays - (int)(CampaignTime.Now - _bribeCooldowns[conversationParty]).ToDays;

                if (bribeCooldown > 0)
                {
                    MBTextManager.SetTextVariable("LORD_BRIBE_COOLDOWN", bribeCooldown);
                    MBTextManager.SetTextVariable("PLURAL", bribeCooldown > 1 ? 1 : 0);
                    // Display the bribe cooldown's number of days in the option's tooltip.
                    hint = new TextObject("{=SurrenderTweaks03}You cannot attack this party for {LORD_BRIBE_COOLDOWN} {?PLURAL}days{?}day{\\?}.", null);

                    // Disable the option for attacking the party.
                    __result = false;
                }
            }
        }

        public LordSurrenderCampaignBehavior() => _bribeCooldowns = new Dictionary<MobileParty, CampaignTime>();

        public override void RegisterEvents()
        {
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, new Action<CampaignGameStarter>(OnSessionLaunched));
            CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, new Action(OnDailyTick));
        }

        public override void SyncData(IDataStore dataStore)
        {
            try
            {
                dataStore.SyncData("_lordBribeCooldowns", ref _bribeCooldowns);
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

        private void OnDailyTick()
        {
            foreach (KeyValuePair<MobileParty, CampaignTime> keyValuePair in _bribeCooldowns.ToList().Where(p => (CampaignTime.Now - p.Value).ToDays >= SurrenderTweaksSettings.Instance.LordBribeCooldownDays))
            {
                _bribeCooldowns.Remove(keyValuePair.Key);
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
        }

        private bool conversation_lord_bribe_on_condition()
        {
            MBTextManager.SetTextVariable("MONEY", SurrenderHelper.GetBribeAmount(MobileParty.ConversationParty, null));

            return SurrenderEvent.PlayerSurrenderEvent.IsBribeFeasible && MobileParty.ConversationParty.MapEvent == null && MobileParty.ConversationParty.SiegeEvent == null;
        }

        private bool conversation_lord_surrender_on_condition() => SurrenderEvent.PlayerSurrenderEvent.IsSurrenderFeasible;

        private void conversation_lord_bribe_on_consequence()
        {
            // Transfer the bribe amount from the lord to the player.
            GiveGoldAction.ApplyBetweenCharacters(MobileParty.ConversationParty.LeaderHero, Hero.MainHero, SurrenderHelper.GetBribeAmount(MobileParty.ConversationParty, null), false);

            // Add a bribe cooldown to the party.
            _bribeCooldowns.Add(MobileParty.ConversationParty, CampaignTime.Now);

            PlayerEncounter.LeaveEncounter = true;
        }

        private void conversation_lord_surrender_on_consequence()
        {
            PartyBase defender = PlayerEncounter.EncounteredParty;
            Dictionary<PartyBase, ItemRoster> dictionary = new Dictionary<PartyBase, ItemRoster>();
            // Capture the trade items.
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
            dictionary.Add(PartyBase.MainParty, value);
            InventoryManager.OpenScreenAsLoot(dictionary);
            PartyScreenManager.OpenScreenAsLoot(TroopRoster.CreateDummyTroopRoster(), troopRoster, defender.Name, troopRoster.TotalManCount, null);
            PlayerEncounter.LeaveEncounter = true;
        }
    }
}
