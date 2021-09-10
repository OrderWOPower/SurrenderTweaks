﻿using System;
using TaleWorlds.CampaignSystem;

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
        public void OnSetupPreConversation()
        {
            if (MobileParty.ConversationParty != null && !MobileParty.ConversationParty.IsMilitia)
            {
                SurrenderTweaksHelper.SetBribeOrSurrender(MobileParty.ConversationParty, MobileParty.MainParty);
            }
        }
        public void OnTick(float dt)
        {
            if (PlayerSiege.PlayerSiegeEvent == null)
            {
                SurrenderTweaksHelper.SetBribeOrSurrender(null, null);
            }
        }
    }
}
