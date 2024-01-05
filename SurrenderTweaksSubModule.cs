using Bannerlord.UIExtenderEx;
using HarmonyLib;
using SandBox.View.Map;
using SurrenderTweaks.Behaviors;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using TaleWorlds.ScreenSystem;

namespace SurrenderTweaks
{
    // This mod enables surrender for lord parties and settlements. It also displays an enemy party or settlement's chance of surrender.
    public class SurrenderTweaksSubModule : MBSubModuleBase
    {
        private Harmony _harmony;

        protected override void OnSubModuleLoad()
        {
            UIExtender uiExtender = new UIExtender("SurrenderTweaks");

            uiExtender.Register(typeof(SurrenderTweaksSubModule).Assembly);
            uiExtender.Enable();

            _harmony = new Harmony("mod.bannerlord.surrendertweaks");
            _harmony.PatchAll();
        }

        protected override void OnGameStart(Game game, IGameStarter gameStarterObject)
        {
            if (game.GameType is Campaign)
            {
                CampaignGameStarter campaignGameStarter = (CampaignGameStarter)gameStarterObject;

                campaignGameStarter.AddBehavior(new SurrenderCampaignBehavior());
                campaignGameStarter.AddBehavior(new LordSurrenderCampaignBehavior());
                campaignGameStarter.AddBehavior(new SettlementSurrenderCampaignBehavior());
                ScreenManager.OnPushScreen += OnScreenManagerPushScreen;

                _harmony.Patch(AccessTools.Method(typeof(EncounterGameMenuBehavior), "game_menu_town_town_besiege_on_condition"), postfix: new HarmonyMethod(AccessTools.Method(typeof(SettlementSurrenderCampaignBehavior), "Postfix")));
            }
        }

        public override void OnGameEnd(Game game) => _harmony.Unpatch(AccessTools.Method(typeof(EncounterGameMenuBehavior), "game_menu_town_town_besiege_on_condition"), AccessTools.Method(typeof(SettlementSurrenderCampaignBehavior), "Postfix"));

        public void OnScreenManagerPushScreen(ScreenBase pushedScreen)
        {
            if (pushedScreen is MapScreen mapScreen)
            {
                mapScreen.AddMapView<SurrenderTweaksView>();
            }
        }
    }
}
