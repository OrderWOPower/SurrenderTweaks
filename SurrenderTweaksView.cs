using SandBox.View.Map;

namespace SurrenderTweaks
{
    public class SurrenderTweaksView : MapView
    {
        protected override void OnMapScreenUpdate(float dt)
        {
            if (SurrenderTweaksMixin.MixinWeakReference != null && SurrenderTweaksMixin.MixinWeakReference.TryGetTarget(out SurrenderTweaksMixin mixin))
            {
                mixin.SetSurrenderChance();
            }
        }
    }
}
