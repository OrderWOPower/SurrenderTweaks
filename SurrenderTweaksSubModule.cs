using Bannerlord.UIExtenderEx;
using HarmonyLib;
using SurrenderTweaks.Behaviors;
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Core.ViewModelCollection;
using TaleWorlds.MountAndBlade;

namespace SurrenderTweaks
{
    // This mod enables surrender for lord parties and settlements. It also displays an enemy party or settlement's chance of surrender.
    [HarmonyPatch(typeof(PowerLevelComparer), MethodType.Constructor, new Type[] { typeof(double), typeof(double) })]
    public class SurrenderTweaksSubModule : MBSubModuleBase
    {
        public static void Postfix(PowerLevelComparer __instance) => _powerLevelComparer = __instance;
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
        protected override void OnApplicationTick(float dt)
        {
            if (_powerLevelComparer != null)
            {
                _surrenderTweaksMixin = new SurrenderTweaksMixin(_powerLevelComparer);
                _powerLevelComparer = null;
            }
            _surrenderTweaksMixin?.SetSurrenderChance();
        }
        private static PowerLevelComparer _powerLevelComparer;
        private SurrenderTweaksMixin _surrenderTweaksMixin;
    }
}
