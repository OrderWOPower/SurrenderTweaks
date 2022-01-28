using Helpers;
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Library;

namespace SurrenderTweaks
{
    public static class SurrenderTweaksHelper
    {
        public static void SetBribeOrSurrender(MobileParty defender, MobileParty attacker, int daysUntilNoFood = 0, int starvationPenalty = 0)
        {
            IsBribeFeasible = IsBribeOrSurrenderFeasible(defender, attacker, daysUntilNoFood, starvationPenalty, false);
            IsSurrenderFeasible = IsBribeOrSurrenderFeasible(defender, attacker, daysUntilNoFood, starvationPenalty, true);
        }
        // Calculate the chance of bribe or surrender for bandit parties, caravan parties, lord parties, militia parties and villager parties.
        public static bool IsBribeOrSurrenderFeasible(MobileParty defender, MobileParty attacker, int daysUntilNoFood, int starvationPenalty, bool shouldSurrender)
        {
            float num = 0f;
            float num2 = 0f;
            if (defender != null && attacker != null && daysUntilNoFood >= 0)
            {
                if (defender.IsBandit)
                {
                    num = !shouldSurrender ? 0.06f : 0.04f;
                    num2 = !shouldSurrender ? 0.09f : 0.06f;
                }
                else if (defender.IsCaravan || defender.IsLordParty || defender.IsMilitia)
                {
                    num = !shouldSurrender || defender.LeaderHero?.GetTraitLevel(DefaultTraits.Valor) < 0 ? 0.4f : 0.1f;
                    num2 = !shouldSurrender || defender.LeaderHero?.GetTraitLevel(DefaultTraits.Valor) < 0 ? 0.6f : 0.15f;
                }
                else if (defender.IsVillager)
                {
                    num = !shouldSurrender ? 0.2f : 0.05f;
                    num2 = !shouldSurrender ? 0.4f : 0.1f;
                }
                num *= !shouldSurrender ? Settings.BribeChanceMultiplier : Settings.SurrenderChanceMultiplier;
                num2 *= !shouldSurrender ? Settings.BribeChanceMultiplier : Settings.SurrenderChanceMultiplier;
                int num3 = (!defender.IsMilitia ? PartyBaseHelper.DoesSurrenderIsLogicalForParty(defender, attacker, num) : DoesSurrenderIsLogicalForSettlement(defender, attacker, daysUntilNoFood, starvationPenalty, num)) ? 33 : 67;
                if (attacker.IsMainParty && Hero.MainHero.GetPerkValue(DefaultPerks.Roguery.Scarface))
                {
                    num3 = MathF.Round(num3 * (1f + DefaultPerks.Roguery.Scarface.PrimaryBonus * 0.01f));
                }
                return 50 <= 100 - num3 && (!defender.IsMilitia ? PartyBaseHelper.DoesSurrenderIsLogicalForParty(defender, attacker, num2) : DoesSurrenderIsLogicalForSettlement(defender, attacker, daysUntilNoFood, starvationPenalty, num2));
            }
            else
            {
                return false;
            }
        }
        // Compare the defenders' and attackers' relative strengths. Give the defenders a bonus for every day of food that they have. Give the defenders a penalty if they have no food.
        public static bool DoesSurrenderIsLogicalForSettlement(MobileParty defender, MobileParty attacker, int daysUntilNoFood, int starvationPenalty, float acceptablePowerRatio = 0.1f)
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
            double num3 = ((double)(num2 * acceptablePowerRatio) * (0.5f + 0.5f * (defender.Party.Random.GetValue(0) / 100f))) - (daysUntilNoFood * 96 * Settings.NutritionBonusMultiplier) + (starvationPenalty * Settings.StarvationPenaltyMultiplier);
            return num < num3;
        }
        // For lord parties, calculate the bribe amount based on the total barter value of the lord and the troops in the party.
        // For settlements, calculate the bribe amount based on the total barter value of the lords and the troops in the settlement, as well as the prosperity of the settlement.
        public static void BribeAmount(MobileParty conversationParty, Settlement defenderSettlement, out int gold)
        {
            int num = 0;
            int num2 = 0;
            if (conversationParty != null)
            {
                if (!conversationParty.IsMilitia)
                {
                    if (conversationParty.LeaderHero != null)
                    {
                        num += (int)(2f * Campaign.Current.Models.ValuationModel.GetMilitaryValueOfParty(conversationParty));
                        foreach (TroopRosterElement troopRosterElement in conversationParty.MemberRoster.GetTroopRoster())
                        {
                            if (troopRosterElement.Character.IsHero)
                            {
                                num += (int)(0.2f * Campaign.Current.Models.ValuationModel.GetValueOfHero(troopRosterElement.Character.HeroObject));
                            }
                        }
                        num2 = (int)Math.Min(num * Settings.BribeAmountMultiplier, conversationParty.LeaderHero.Gold);
                    }
                }
                else
                {
                    foreach (PartyBase defenderParty in defenderSettlement.SiegeParties)
                    {
                        if (defenderParty.MobileParty != null)
                        {
                            num += (int)(2f * Campaign.Current.Models.ValuationModel.GetMilitaryValueOfParty(defenderParty.MobileParty));
                            foreach (TroopRosterElement troopRosterElement in defenderParty.MemberRoster.GetTroopRoster())
                            {
                                if (troopRosterElement.Character.IsHero)
                                {
                                    num += (int)(0.2f * Campaign.Current.Models.ValuationModel.GetValueOfHero(troopRosterElement.Character.HeroObject));
                                }
                            }
                        }
                    }
                    num += (int)defenderSettlement.Prosperity * 6;
                    num2 = (int)Math.Min(num * Settings.BribeAmountMultiplier, defenderSettlement.Town.Gold);
                }
            }
            gold = num2;
        }
        public static bool IsBribeFeasible { get; set; }
        public static bool IsSurrenderFeasible { get; set; }
        public static SurrenderTweaksSettings Settings => SurrenderTweaksSettings.Instance;
    }
}
