using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.ViewModels;
using TaleWorlds.Core.ViewModelCollection;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

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
            if (Mission.Current == null)
            {
                if (SurrenderTweaksHelper.IsBribeFeasible)
                {
                    SurrenderChance = "Chance of Surrender: High";
                }
                if (SurrenderTweaksHelper.IsSurrenderFeasible)
                {
                    SurrenderChance = "Chance of Surrender: Very High";
                }
            }
        }
        [DataSourceProperty]
        public string SurrenderChance
        {
            get
            {
                return _surrenderChance;
            }
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
