﻿using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.ViewModels;
using TaleWorlds.Core.ViewModelCollection;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace SurrenderTweaks
{
    [ViewModelMixin]
    public class SurrenderTweaksMixin : BaseViewModelMixin<PowerLevelComparer>
    {
        private string _surrenderChance;

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

        public SurrenderTweaksMixin(PowerLevelComparer powerLevelComparer) : base(powerLevelComparer) { }

        // Set the "Chance of Surrender" text depending on whether a bribe or a surrender is feasible.
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
