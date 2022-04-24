using Bannerlord.UIExtenderEx;
using HarmonyLib;
using SandBox.View.Map;
using SurrenderTweaks.Behaviors;
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using TaleWorlds.ScreenSystem;

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
                ScreenManager.OnPushScreen += OnPushScreen;
            }
        }

        public void OnPushScreen(ScreenBase pushedScreen)
        {
            if (pushedScreen is MapScreen mapScreen)
            {
                mapScreen.AddMapView<SurrenderTweaksView>(Array.Empty<object>());
            }
        }
    }
}
