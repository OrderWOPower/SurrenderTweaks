using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.ViewModels;
using System;
using TaleWorlds.Core.ViewModelCollection;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace SurrenderTweaks
{
    [ViewModelMixin]
    public class SurrenderTweaksMixin : BaseViewModelMixin<PowerLevelComparer>
    {
        private string _surrenderChance;

        public static WeakReference<SurrenderTweaksMixin> MixinWeakReference { get; set; }

        [DataSourceProperty]
        public string SurrenderChance
        {
            get => _surrenderChance;

            set
            {
                if (value != _surrenderChance)
                {
                    _surrenderChance = value;

                    ViewModel?.OnPropertyChangedWithValue(value, "SurrenderChance");
                }
            }
        }

        public SurrenderTweaksMixin(PowerLevelComparer powerLevelComparer) : base(powerLevelComparer) => MixinWeakReference = new WeakReference<SurrenderTweaksMixin>(this);

        public void SetSurrenderChance()
        {
            SurrenderEvent surrenderEvent = SurrenderEvent.PlayerSurrenderEvent;

            SurrenderChance = null;

            if (surrenderEvent.IsBribeFeasible)
            {
                SurrenderChance = new TextObject("{=SurrenderTweaks01}Chance of Surrender: High").ToString();
            }

            if (surrenderEvent.IsSurrenderFeasible)
            {
                SurrenderChance = new TextObject("{=SurrenderTweaks02}Chance of Surrender: Very High").ToString();
            }
        }
    }
}
