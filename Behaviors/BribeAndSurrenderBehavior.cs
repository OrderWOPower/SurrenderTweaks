using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Localization;

namespace SurrenderTweaks.Behaviors
{
    public class BribeAndSurrenderBehavior : CampaignBehaviorBase
    {
        public override void RegisterEvents() => CampaignEvents.SetupPreConversationEvent.AddNonSerializedListener(this, new Action(OnSetupPreConversation));
        public override void SyncData(IDataStore dataStore) { }
        private void OnSetupPreConversation()
        {
            if (MobileParty.ConversationParty != null)
            {
                if (!MobileParty.ConversationParty.IsMilitia)
                {
                    SurrenderTweaksHelper.SetBribeOrSurrender(MobileParty.ConversationParty, MobileParty.MainParty);
                }
                MBTextManager.SetTextVariable("MONEY", SurrenderTweaksHelper.BribeAmount());
            }
        }
    }
}
