using HarmonyLib;
using Helpers;
using System;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace SurrenderTweaks
{
    public static class SurrenderHelper
    {
        public static bool IsBribeOrSurrenderFeasible(MobileParty defender, MobileParty attacker, int daysUntilNoFood, int starvationPenalty, bool shouldSurrender)
        {
            if (defender != null && attacker != null && defender.Army == null && defender.DefaultBehavior != AiBehavior.EngageParty && defender.ShortTermBehavior != AiBehavior.EngageParty && daysUntilNoFood >= 0)
            {
                float num = 0f, num2 = 0f;
                int num3;
                SurrenderTweaksSettings settings = SurrenderTweaksSettings.Instance;

                // Calculate the chance of bribe or surrender for bandit parties, caravan parties, lord parties, militia parties and villager parties.
                if (defender.IsBandit)
                {
                    num = !shouldSurrender ? 0.06f : 0.04f;
                    num2 = !shouldSurrender ? 0.09f : 0.06f;
                }
                else if (defender.IsCaravan || defender.IsLordParty || defender.IsMilitia)
                {
                    Hero defenderLeader = defender.LeaderHero, attackerLeader = attacker.LeaderHero;
                    int relation = 0, defenderTraitLevels = 0, attackerTraitLevels = 0;

                    if (defenderLeader != null && attackerLeader != null)
                    {
                        relation = CharacterRelationManager.GetHeroRelation(defenderLeader, attackerLeader);
                        defenderTraitLevels = defenderLeader.GetTraitLevel(DefaultTraits.Mercy) + defenderLeader.GetTraitLevel(DefaultTraits.Valor) + defenderLeader.GetTraitLevel(DefaultTraits.Honor) + defenderLeader.GetTraitLevel(DefaultTraits.Generosity) + defenderLeader.GetTraitLevel(DefaultTraits.Calculating);
                        attackerTraitLevels = attackerLeader.GetTraitLevel(DefaultTraits.Mercy) + attackerLeader.GetTraitLevel(DefaultTraits.Valor) + attackerLeader.GetTraitLevel(DefaultTraits.Honor) + attackerLeader.GetTraitLevel(DefaultTraits.Generosity) + attackerLeader.GetTraitLevel(DefaultTraits.Calculating);
                    }

                    if (!shouldSurrender || relation > 25 || defenderTraitLevels + attackerTraitLevels > 0)
                    {
                        num = 0.4f;
                        num2 = 0.6f;
                    }
                    else if (shouldSurrender || relation < -25 || defenderTraitLevels + attackerTraitLevels < 0)
                    {
                        num = 0.1f;
                        num2 = 0.15f;
                    }
                }
                else if (defender.IsVillager)
                {
                    num = !shouldSurrender ? 0.2f : 0.05f;
                    num2 = !shouldSurrender ? 0.4f : 0.1f;
                }

                num *= !shouldSurrender ? settings.BribeChanceMultiplier : settings.SurrenderChanceMultiplier;
                num2 *= !shouldSurrender ? settings.BribeChanceMultiplier : settings.SurrenderChanceMultiplier;
                num3 = (!defender.IsMilitia ? PartyBaseHelper.DoesSurrenderIsLogicalForParty(defender, attacker, num) : DoesSurrenderIsLogicalForSettlement(defender, attacker, daysUntilNoFood, starvationPenalty, num)) ? 33 : 67;

                if (attacker.IsMainParty && Hero.MainHero.GetPerkValue(DefaultPerks.Roguery.Scarface))
                {
                    num3 = MathF.Round(num3 * (1f + DefaultPerks.Roguery.Scarface.PrimaryBonus));
                }

                return 50 <= 100 - num3 && (!defender.IsMilitia ? PartyBaseHelper.DoesSurrenderIsLogicalForParty(defender, attacker, num2) : DoesSurrenderIsLogicalForSettlement(defender, attacker, daysUntilNoFood, starvationPenalty, num2));
            }

            return false;
        }

        public static bool DoesSurrenderIsLogicalForSettlement(MobileParty defender, MobileParty attacker, int daysUntilNoFood, int starvationPenalty, float acceptablePowerRatio = 0.1f)
        {
            if (defender.CurrentSettlement != null && attacker.BesiegerCamp != null)
            {
                float num = defender.Party.TotalStrength, num2 = attacker.Party.TotalStrength, num3;
                SurrenderTweaksSettings settings = SurrenderTweaksSettings.Instance;

                foreach (PartyBase party in defender.CurrentSettlement.GetInvolvedPartiesForEventType(MapEvent.BattleTypes.Siege).Where(p => p != defender.Party))
                {
                    num += party.TotalStrength;
                }

                foreach (PartyBase party in attacker.BesiegerCamp.GetInvolvedPartiesForEventType(MapEvent.BattleTypes.Siege).Where(p => p != attacker.Party))
                {
                    num2 += party.TotalStrength;
                }

                // Compare the defenders' and attackers' relative strengths. Give the defenders a bonus for every day of food that they have. Give the defenders a penalty if they have no food.
                num3 = (num2 * acceptablePowerRatio) - (daysUntilNoFood * 96 * settings.NutritionBonusMultiplier) + (starvationPenalty * settings.StarvationPenaltyMultiplier);

                return num < num3;
            }

            return false;
        }

        public static int GetBribeAmount(MobileParty conversationParty, Settlement defenderSettlement)
        {
            int num = 0, num2 = 0;
            ValuationModel valuationModel = Campaign.Current.Models.ValuationModel;
            SurrenderTweaksSettings settings = SurrenderTweaksSettings.Instance;

            if (conversationParty != null)
            {
                if (!conversationParty.IsMilitia)
                {
                    if (conversationParty.LeaderHero != null)
                    {
                        num += (int)(2f * valuationModel.GetMilitaryValueOfParty(conversationParty));

                        foreach (TroopRosterElement troopRosterElement in conversationParty.MemberRoster.GetTroopRoster().Where(e => e.Character.IsHero))
                        {
                            num += (int)(0.2f * valuationModel.GetValueOfHero(troopRosterElement.Character.HeroObject));
                        }

                        // For lord parties, calculate the bribe amount based on the total barter value of the lords and the troops in the party.
                        num2 = MathF.Min((int)(num * settings.BribeAmountMultiplier), conversationParty.LeaderHero.Gold);
                    }
                }
                else
                {
                    if (defenderSettlement != null)
                    {
                        foreach (PartyBase defenderParty in defenderSettlement.GetInvolvedPartiesForEventType(MapEvent.BattleTypes.Siege).Where(p => p.MobileParty != null))
                        {
                            num += (int)(2f * valuationModel.GetMilitaryValueOfParty(defenderParty.MobileParty));

                            foreach (TroopRosterElement troopRosterElement in defenderParty.MemberRoster.GetTroopRoster().Where(e => e.Character.IsHero))
                            {
                                num += (int)(0.2f * valuationModel.GetValueOfHero(troopRosterElement.Character.HeroObject));
                            }
                        }

                        num += (int)defenderSettlement.Prosperity * 6;
                        // For settlements, calculate the bribe amount based on the total barter value of the lords and the troops in the settlement, as well as the prosperity of the settlement.
                        num2 = MathF.Min((int)(num * settings.BribeAmountMultiplier), defenderSettlement.Town.Gold);
                    }
                }
            }

            return num2;
        }

        public static void AddPrisonersAsCasualties(MobileParty attacker, MobileParty defender)
        {
            Type warExhaustionManager = AccessTools.TypeByName("WarExhaustionManager");
            object instance = AccessTools.Property(warExhaustionManager, "Instance")?.GetValue(null);
            int prisonerCount = defender.MemberRoster.TotalManCount;

            attacker.MapFaction.GetStanceWith(defender.MapFaction).Casualties1 += prisonerCount;

            // Check whether Diplomacy is loaded.
            if (instance != null && attacker.MapFaction.IsKingdomFaction && defender.MapFaction.IsKingdomFaction)
            {
                AccessTools.Method(warExhaustionManager, "AddCasualtyWarExhaustion", new Type[] { typeof(Kingdom), typeof(Kingdom), typeof(int), typeof(int), typeof(TextObject), typeof(TextObject) }).Invoke(instance, new object[] { (Kingdom)attacker.MapFaction, (Kingdom)defender.MapFaction, 0, prisonerCount, attacker.Name, defender.Name });
            }
        }
    }
}
