using Bannerlord.UIExtenderEx;
using HarmonyLib;
using SurrenderTweaks.Behaviors;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace SurrenderTweaks
{
    // This mod enables surrender for lord parties and settlements. It also displays an enemy party or settlement's chance of surrender.
    public class SurrenderTweaksSubModule : MBSubModuleBase
    {
        protected override void OnSubModuleLoad()
        {
            new Harmony("mod.bannerlord.surrendertweaks").PatchAll();
            UIExtender uiExtender = new UIExtender("SurrenderTweaks");
            uiExtender.Register(typeof(SurrenderTweaksSubModule).Assembly);
            uiExtender.Enable();
        }

        protected override void OnGameStart(Game game, IGameStarter gameStarter)
        {
            if (game.GameType is Campaign)
            {
                CampaignGameStarter campaignStarter = (CampaignGameStarter)gameStarter;
                campaignStarter.AddBehavior(new BribeAndSurrenderBehavior());
                campaignStarter.AddBehavior(new LordBribeAndSurrenderBehavior());
                campaignStarter.AddBehavior(new SettlementBribeAndSurrenderBehavior());
            }
        }
    }
}
