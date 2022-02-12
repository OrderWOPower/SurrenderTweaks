using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.ViewModels;
using TaleWorlds.Core.ViewModelCollection;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace SurrenderTweaks
{
    [ViewModelMixin]
    public class SurrenderTweaksMixin : BaseViewModelMixin<PowerLevelComparer>
    {
        public SurrenderTweaksMixin(PowerLevelComparer powerLevelComparer) : base(powerLevelComparer) { }
        // Set the "Chance of Surrender" text depending on whether a bribe or a surrender is feasible.
        public void SetSurrenderChance()
        {
            SurrenderChance = null;
            if (SurrenderTweaksHelper.IsBribeFeasible)
            {
                SurrenderChance = new TextObject("{=SurrenderTweaks01}Chance of Surrender: High").ToString();
            }
            if (SurrenderTweaksHelper.IsSurrenderFeasible)
            {
                SurrenderChance = new TextObject("{=SurrenderTweaks02}Chance of Surrender: Very High").ToString();
            }
        }
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
        private string _surrenderChance;
    }
}
