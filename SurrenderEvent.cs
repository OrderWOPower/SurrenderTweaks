﻿using TaleWorlds.CampaignSystem.Party;

namespace SurrenderTweaks
{
    public class SurrenderEvent
    {
        private static readonly SurrenderEvent _surrenderEvent = new SurrenderEvent();

        public static SurrenderEvent PlayerSurrenderEvent => _surrenderEvent;

        public bool IsBribeFeasible { get; set; }

        public bool IsSurrenderFeasible { get; set; }

        public void SetBribeOrSurrenderFeasible(bool isBribeFeasible, bool isSurrenderFeasible)
        {
            IsBribeFeasible = isBribeFeasible;
            IsSurrenderFeasible = isSurrenderFeasible;
        }

        public void SetBribeOrSurrenderFeasible(MobileParty defender, MobileParty attacker, int daysUntilNoFood = 0, int starvationPenalty = 0)
        {
            IsBribeFeasible = SurrenderHelper.IsBribeOrSurrenderFeasible(defender, attacker, daysUntilNoFood, starvationPenalty, false);
            IsSurrenderFeasible = SurrenderHelper.IsBribeOrSurrenderFeasible(defender, attacker, daysUntilNoFood, starvationPenalty, true);

            if (IsSurrenderFeasible)
            {
                IsBribeFeasible = true;
            }
        }
    }
}
