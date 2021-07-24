using System;
using Helpers;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace SurrenderTweaks.Behaviors
{
    public class BribeAndSurrenderBehavior : CampaignBehaviorBase
    {
        public override void RegisterEvents()
        {
            CampaignEvents.SetupPreConversationEvent.AddNonSerializedListener(this, new Action(OnSetupPreConversation));
            CampaignEvents.TickEvent.AddNonSerializedListener(this, new Action<float>(OnTick));
        }
        public override void SyncData(IDataStore dataStore) { }
        // For lord parties, calculate the bribe amount based on the total barter value of the lord and the troops in the party.
        // For settlements, calculate the bribe amount based on the total barter value of the lords and the troops in the settlement, as well as the properity of the settlement.
        private void OnSetupPreConversation()
        {
            MobileParty conversationParty = MobileParty.ConversationParty;
            Settlement defenderSettlement = SettlementBribeAndSurrenderBehavior.DefenderSettlement;
            if (conversationParty != null)
            {
                int num = 0;
                if (!conversationParty.IsMilitia)
                {
                    if (conversationParty.LeaderHero != null)
                    {
                        num += (int)(0.1f * Campaign.Current.Models.ValuationModel.GetValueOfHero(conversationParty.LeaderHero));
                        num += (int)(0.1f * Campaign.Current.Models.ValuationModel.GetMilitaryValueOfParty(conversationParty));
                        BribeAmount = Math.Min(num, conversationParty.LeaderHero.Gold);
                    }
                    SetBribeOrSurrender(conversationParty, MobileParty.MainParty);
                }
                else
                {
                    foreach (PartyBase party in defenderSettlement.SiegeParties)
                    {
                        if (party.LeaderHero != null)
                        {
                            num += (int)(0.1f * Campaign.Current.Models.ValuationModel.GetValueOfHero(party.LeaderHero));
                        }
                        if (party.MobileParty != null)
                        {
                            num += (int)(0.1f * Campaign.Current.Models.ValuationModel.GetMilitaryValueOfParty(party.MobileParty));
                        }
                    }
                    num += (int)defenderSettlement.Prosperity * 3;
                    BribeAmount = Math.Min(num, defenderSettlement.Town.Gold);
                }
                MBTextManager.SetTextVariable("MONEY", BribeAmount);
            }
        }
        public void OnTick(float dt)
        {
            IsBribeFeasible = false;
            IsSurrenderFeasible = false;
            if (PlayerSiege.PlayerSiegeEvent == null)
            {
                SettlementBribeAndSurrenderBehavior.DefenderSettlement = null;
            }
            else
            {
                SetBribeOrSurrender(SettlementBribeAndSurrenderBehavior.DefenderSettlement?.MilitiaPartyComponent?.MobileParty, MobileParty.MainParty);
            }
        }
        // Set the chance of bribe or surrender for bandit parties, caravan parties, lord parties, militia parties and villager parties.
        public void SetBribeOrSurrender(MobileParty defender, MobileParty attacker)
        {
            if (defender != null && attacker != null)
            {
                float num = 0.0f;
                float num2 = 0.0f;
                float num3 = 0.0f;
                float num4 = 0.0f;
                if (defender.IsBandit)
                {
                    num = 0.06f;
                    num2 = 0.09f;
                    num3 = 0.04f;
                    num4 = 0.06f;
                }
                else if (defender.IsCaravan || defender.IsLordParty || defender.IsMilitia)
                {
                    num = 0.3f;
                    num2 = 0.6f;
                    num3 = 0.1f;
                    num4 = 0.2f;
                }
                else if (defender.IsVillager)
                {
                    num = 0.2f;
                    num2 = 0.4f;
                    num3 = 0.05f;
                    num4 = 0.1f;
                }
                IsBribeFeasible = IsBribeOrSurrenderFeasible(defender, attacker, num, num2);
                IsSurrenderFeasible = IsBribeOrSurrenderFeasible(defender, attacker, num3, num4);
                if (defender.LeaderHero?.GetTraitLevel(DefaultTraits.Valor) < 0)
                {
                    IsSurrenderFeasible = IsBribeFeasible;
                }
            }
        }
        // Calculate the chance of bribe or surrender for parties and settlements.
        private bool IsBribeOrSurrenderFeasible(MobileParty defender, MobileParty attacker, float num2, float num3)
        {
            int num = (attacker.SiegeEvent == null ? PartyBaseHelper.DoesSurrenderIsLogicalForParty(defender, attacker, num2) : SettlementBribeAndSurrenderBehavior.DoesSurrenderIsLogicalForSettlement(defender, attacker, num2)) ? 33 : 67;
            if (Hero.MainHero.GetPerkValue(DefaultPerks.Roguery.Scarface))
            {
                num = MathF.Round(num * (1f + DefaultPerks.Roguery.Scarface.PrimaryBonus * 0.01f));
            }
            return 50 <= 100 - num && (attacker.SiegeEvent == null ? PartyBaseHelper.DoesSurrenderIsLogicalForParty(defender, attacker, num3) : SettlementBribeAndSurrenderBehavior.DoesSurrenderIsLogicalForSettlement(defender, attacker, num3));
        }
        public static int BribeAmount { get; set; }
        public static bool IsBribeFeasible { get; set; }
        public static bool IsSurrenderFeasible { get; set; }
    }
}
