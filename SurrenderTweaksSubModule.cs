using HarmonyLib;
using Bannerlord.UIExtenderEx;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using SurrenderTweaks.Behaviors;

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
                campaignStarter.AddBehavior(new SettlementBribeAndSurrenderBehavior());
            }
        }
        protected override void OnApplicationTick(float dt) => SurrenderTweaksMixin.SetSurrenderChance();
    }
}
