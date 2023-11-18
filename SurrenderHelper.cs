using HarmonyLib;
using Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Siege;
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
            Settlement defenderSettlement = defender.CurrentSettlement;
            BesiegerCamp attackerCamp = attacker.BesiegerCamp;

            if (defenderSettlement != null && attackerCamp != null)
            {
                // Compare the defenders' and attackers' relative strengths. Give the defenders a bonus for every day of food that they have. Give the defenders a penalty if they have no food.
                float num = defender.Party.TotalStrength + defenderSettlement.GetInvolvedPartiesForEventType(MapEvent.BattleTypes.Siege).Where(party => party != defender.Party).Sum(party => party.TotalStrength) + (defenderSettlement.SiegeEngines.DeployedSiegeEngines.Count * 24) + (defenderSettlement.SettlementTotalWallHitPoints / 100);
                float num2 = attacker.Party.TotalStrength + attackerCamp.GetInvolvedPartiesForEventType(MapEvent.BattleTypes.Siege).Where(party => party != attacker.Party).Sum(party => party.TotalStrength) + (attackerCamp.SiegeEngines.DeployedSiegeEngines.Count * 24);
                float num3 = (num2 * acceptablePowerRatio) - (daysUntilNoFood * 96 * SurrenderTweaksSettings.Instance.NutritionBonusMultiplier) + (starvationPenalty * SurrenderTweaksSettings.Instance.StarvationPenaltyMultiplier);

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
                        // For lord parties, calculate the bribe amount based on the total barter value of the lords and the troops in the party.
                        num = (int)(valuationModel.GetMilitaryValueOfParty(conversationParty) * 2f) + conversationParty.MemberRoster.GetTroopRoster().Where(troopRosterElement => troopRosterElement.Character.IsHero).Sum(troopRosterElement => (int)(valuationModel.GetValueOfHero(troopRosterElement.Character.HeroObject) * 0.2f));
                        num2 = MathF.Min((int)(num * settings.BribeAmountMultiplier), conversationParty.LeaderHero.Gold);
                    }
                }
                else
                {
                    if (defenderSettlement != null)
                    {
                        IEnumerable<PartyBase> defenderParties = defenderSettlement.GetInvolvedPartiesForEventType(MapEvent.BattleTypes.Siege).Where(defenderParty => defenderParty.MobileParty != null);

                        // For settlements, calculate the bribe amount based on the total barter value of the lords and the troops in the settlement, as well as the prosperity of the settlement.
                        num = defenderParties.Sum(defenderParty => (int)(valuationModel.GetMilitaryValueOfParty(defenderParty.MobileParty) * 2f)) + defenderParties.SelectMany(defenderParty => defenderParty.MemberRoster.GetTroopRoster().Where(troopRosterElement => troopRosterElement.Character.IsHero)).Sum(troopRosterElement => (int)(valuationModel.GetValueOfHero(troopRosterElement.Character.HeroObject) * 0.2f)) + ((int)defenderSettlement.Town.Prosperity * 6);
                        num2 = MathF.Min((int)(num * settings.BribeAmountMultiplier), defenderSettlement.Town.Gold);
                    }
                }
            }

            return num2;
        }

        public static void AddPrisonersAsCasualties(MobileParty attacker, MobileParty defender)
        {
            IFaction attackerFaction = attacker.MapFaction, defenderFaction = defender.MapFaction;
            int prisonerCount = defender.MemberRoster.TotalManCount;
            object instance = AccessTools.Property(AccessTools.TypeByName("WarExhaustionManager"), "Instance")?.GetValue(null);

            attackerFaction.GetStanceWith(defenderFaction).Casualties1 += prisonerCount;

            // Check whether Diplomacy is loaded.
            if (instance != null && attackerFaction.IsKingdomFaction && defenderFaction.IsKingdomFaction)
            {
                AccessTools.Method(AccessTools.TypeByName("WarExhaustionManager"), "AddCasualtyWarExhaustion", new Type[] { typeof(Kingdom), typeof(Kingdom), typeof(int), typeof(int), typeof(TextObject), typeof(TextObject) }).Invoke(instance, new object[] { (Kingdom)attackerFaction, (Kingdom)defenderFaction, 0, prisonerCount, attacker.Name, defender.Name });
            }
        }
    }
}
