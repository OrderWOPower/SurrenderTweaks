using System;
using System.Collections.Generic;
using Helpers;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Library;

namespace SurrenderTweaks
{
    public static class SurrenderTweaksHelper
    {
        public static void SetBribeOrSurrender(MobileParty defender, MobileParty attacker)
        {
            IsBribeFeasible = false;
            IsSurrenderFeasible = false;
            if (defender != null && attacker != null)
            {
                IsBribeFeasible = IsBribeOrSurrenderFeasible(defender, attacker, false);
                IsSurrenderFeasible = IsBribeOrSurrenderFeasible(defender, attacker, true);
                if (defender.LeaderHero?.GetTraitLevel(DefaultTraits.Valor) < 0)
                {
                    IsSurrenderFeasible = IsBribeFeasible;
                }
            }
        }
        // Calculate the chance of bribe or surrender for bandit parties, caravan parties, lord parties, militia parties and villager parties.
        private static bool IsBribeOrSurrenderFeasible(MobileParty defender, MobileParty attacker, bool shouldSurrender)
        {
            float num = 0f;
            float num2 = 0f;
            if (defender.IsBandit)
            {
                num = !shouldSurrender ? 0.06f : 0.04f;
                num2 = !shouldSurrender ? 0.09f : 0.06f;
            }
            else if (defender.IsCaravan || defender.IsLordParty || defender.IsMilitia)
            {
                num = !shouldSurrender ? 0.3f : 0.1f;
                num2 = !shouldSurrender ? 0.6f : 0.2f;
            }
            else if (defender.IsVillager)
            {
                num = !shouldSurrender ? 0.2f : 0.05f;
                num2 = !shouldSurrender ? 0.4f : 0.1f;
            }
            int num3 = (!defender.IsMilitia ? PartyBaseHelper.DoesSurrenderIsLogicalForParty(defender, attacker, num) : DoesSurrenderIsLogicalForSettlement(defender, attacker, num)) ? 33 : 67;
            if (Hero.MainHero.GetPerkValue(DefaultPerks.Roguery.Scarface))
            {
                num3 = MathF.Round(num3 * (1f + DefaultPerks.Roguery.Scarface.PrimaryBonus * 0.01f));
            }
            return 50 <= 100 - num3 && (!defender.IsMilitia ? PartyBaseHelper.DoesSurrenderIsLogicalForParty(defender, attacker, num2) : DoesSurrenderIsLogicalForSettlement(defender, attacker, num2));
        }
        // Compare the defenders' and attackers' relative strengths. Give the defenders a bonus for every day of food that they have. Give the defenders a penalty if they have no food.
        public static bool DoesSurrenderIsLogicalForSettlement(MobileParty defender, MobileParty attacker, float acceptablePowerRatio = 0.1f)
        {
            double num = defender.Party.TotalStrength;
            double num2 = attacker.Party.TotalStrength;
            foreach (PartyBase party in defender.CurrentSettlement.SiegeParties)
            {
                if (party != defender.Party)
                {
                    num += party.TotalStrength;
                }
            }
            foreach (PartyBase party in attacker.BesiegerCamp.SiegeParties)
            {
                if (party != attacker.Party)
                {
                    num2 += party.TotalStrength;
                }
            }
            double num3 = ((double)(num2 * acceptablePowerRatio) * (0.5f + 0.5f * (defender.Party.Random.GetValue(0) / 100f))) - (Food * 96) + StarvationPenalty;
            return num < num3;
        }
        // For lord parties, calculate the bribe amount based on the total barter value of the lord and the troops in the party.
        // For settlements, calculate the bribe amount based on the total barter value of the lords and the troops in the settlement, as well as the properity of the settlement.
        public static void BribeAmount(MobileParty conversationParty, Settlement defenderSettlement, out int gold)
        {
            int num = 0;
            int num2 = 0;
            if (!conversationParty.IsMilitia)
            {
                if (conversationParty.LeaderHero != null)
                {
                    num += (int)(0.1f * Campaign.Current.Models.ValuationModel.GetValueOfHero(conversationParty.LeaderHero));
                    num += (int)(0.1f * Campaign.Current.Models.ValuationModel.GetMilitaryValueOfParty(conversationParty));
                    num2 = Math.Min(num, conversationParty.LeaderHero.Gold);
                }
            }
            else
            {
                foreach (PartyBase defenderParty in defenderSettlement.SiegeParties)
                {
                    if (defenderParty.LeaderHero != null)
                    {
                        num += (int)(0.1f * Campaign.Current.Models.ValuationModel.GetValueOfHero(defenderParty.LeaderHero));
                    }
                    if (defenderParty.MobileParty != null)
                    {
                        num += (int)(0.1f * Campaign.Current.Models.ValuationModel.GetMilitaryValueOfParty(defenderParty.MobileParty));
                    }
                }
                num += (int)defenderSettlement.Prosperity * 3;
                num2 = Math.Min(num, defenderSettlement.Town.Gold);
            }
            gold = num2;
        }
        // If a settlement has no food, increase its starvation penalty.
        public static int GetStarvationPenalty(Settlement settlement)
        {
            for (int i = 0; i < StarvationPenalties.Item1.Count; i++)
            {
                if (StarvationPenalties.Item1[i] == settlement)
                {
                    StarvationPenalty = StarvationPenalties.Item2[i];
                }
            }
            if (Food > 0)
            {
                StarvationPenalty = 0;
            }
            else
            {
                StarvationPenalty += 8;
            }
            return StarvationPenalty;
        }
        public static int GetBribeCooldown(Settlement settlement)
        {
            for (int i = 0; i < BribeCooldowns.Item1.Count; i++)
            {
                if (BribeCooldowns.Item1[i] == settlement)
                {
                    return BribeCooldowns.Item2[i];
                }
            }
            return 0;
        }
        public static void SetStarvationPenalties(Tuple<List<Settlement>, List<int>> tuple) => StarvationPenalties = tuple;
        public static void SetBribeCooldowns(Tuple<List<Settlement>, List<int>> tuple) => BribeCooldowns = tuple;
        public static bool IsBribeFeasible { get; set; }
        public static bool IsSurrenderFeasible { get; set; }
        public static Settlement DefenderSettlement
        {
            get
            {
                if (PlayerSiege.PlayerSiegeEvent?.BesiegerCamp.BesiegerParty == MobileParty.MainParty)
                {
                    return PlayerSiege.BesiegedSettlement;
                }
                return null;
            }
        }
        public static double Food => Math.Ceiling(DefenderSettlement.Town.FoodStocks / -DefenderSettlement.Town.FoodChange);
        public static int StarvationPenalty { get; set; }
        public static Tuple<List<Settlement>, List<int>> StarvationPenalties { get; set; }
        public static Tuple<List<Settlement>, List<int>> BribeCooldowns { get; set; }
    }
}
