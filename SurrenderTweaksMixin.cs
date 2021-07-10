using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.ViewModels;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core.ViewModelCollection;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using SurrenderTweaks.Behaviors;

namespace SurrenderTweaks
{
    [ViewModelMixin]
    public class SurrenderTweaksMixin : BaseViewModelMixin<PowerLevelComparer>
    {
        public SurrenderTweaksMixin(PowerLevelComparer powerLevelComparer) : base(powerLevelComparer) => _viewModel = powerLevelComparer;
        // Set the "Chance of Surrender" text depending on whether a bribe or a surrender is feasible.
        public static void SetSurrenderChance()
        {
            SurrenderChance = null;
            if (MapEvent.PlayerMapEvent == null || (MapEvent.PlayerMapEvent != null && Mission.Current == null))
            {
                if (BribeAndSurrenderBehavior.IsBribeFeasible)
                {
                    SurrenderChance = "Chance of Surrender: High";
                }
                if (BribeAndSurrenderBehavior.IsSurrenderFeasible)
                {
                    SurrenderChance = "Chance of Surrender: Very High";
                }
            }
        }
        [DataSourceProperty]
        public static string SurrenderChance
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
                    _viewModel?.OnPropertyChangedWithValue(value, "SurrenderChance");
                }
            }
        }
        private static ViewModel _viewModel;
        private static string _surrenderChance;
    }
}
