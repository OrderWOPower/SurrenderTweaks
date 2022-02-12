using HarmonyLib;
using SandBox.View.Map;
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core.ViewModelCollection;
using TaleWorlds.MountAndBlade.View.Screen;

namespace SurrenderTweaks
{
    [HarmonyPatch(typeof(PowerLevelComparer), MethodType.Constructor, new Type[] { typeof(double), typeof(double) })]
    [GameStateScreen(typeof(MapState))]
    public class SurrenderTweaksScreen : MapScreen
    {
        public static void Postfix(PowerLevelComparer __instance) => _powerLevelComparer = __instance;
        public SurrenderTweaksScreen(MapState mapState) : base(mapState) { }
        protected override void OnFrameTick(float dt)
        {
            base.OnFrameTick(dt);
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
