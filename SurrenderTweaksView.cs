using HarmonyLib;
using SandBox.View.Map;
using System;
using TaleWorlds.Core.ViewModelCollection;

namespace SurrenderTweaks
{
    [HarmonyPatch(typeof(PowerLevelComparer), MethodType.Constructor, new Type[] { typeof(double), typeof(double) })]
    public class SurrenderTweaksView : MapView
    {
        private static PowerLevelComparer _powerLevelComparer;
        private SurrenderTweaksMixin _surrenderTweaksMixin;

        public static void Postfix(PowerLevelComparer __instance) => _powerLevelComparer = __instance;

        protected override void OnMapScreenUpdate(float dt)
        {
            if (_powerLevelComparer != null)
            {
                _surrenderTweaksMixin = new SurrenderTweaksMixin(_powerLevelComparer);
                _powerLevelComparer = null;
            }
            _surrenderTweaksMixin?.SetSurrenderChance();
        }
    }
}
