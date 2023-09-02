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
            UIExtender uiExtender = new UIExtender("SurrenderTweaks");

            uiExtender.Register(typeof(SurrenderTweaksSubModule).Assembly);
            uiExtender.Enable();
            new Harmony("mod.bannerlord.surrendertweaks").PatchAll();
        }

        protected override void OnGameStart(Game game, IGameStarter gameStarterObject)
        {
            if (game.GameType is Campaign)
            {
                CampaignGameStarter campaignGameStarter = (CampaignGameStarter)gameStarterObject;

                campaignGameStarter.AddBehavior(new BribeAndSurrenderBehavior());
                campaignGameStarter.AddBehavior(new LordBribeAndSurrenderBehavior());
                campaignGameStarter.AddBehavior(new SettlementBribeAndSurrenderBehavior());
                ScreenManager.OnPushScreen += OnScreenManagerPushScreen;
            }
        }

        public void OnScreenManagerPushScreen(ScreenBase pushedScreen)
        {
            if (pushedScreen is MapScreen mapScreen)
            {
                mapScreen.AddMapView<SurrenderTweaksView>();
            }
        }
    }
}
