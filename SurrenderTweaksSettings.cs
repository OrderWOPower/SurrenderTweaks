using MCM.Abstractions.Attributes;
using MCM.Abstractions.Attributes.v2;
using MCM.Abstractions.Settings.Base.Global;

namespace SurrenderTweaks
{
    public class SurrenderTweaksSettings : AttributeGlobalSettings<SurrenderTweaksSettings>
    {
        public override string Id => "SurrenderTweaks";
        public override string DisplayName => "Surrender Tweaks";
        public override string FolderName => "SurrenderTweaks";
        public override string FormatType => "json2";
        [SettingPropertyFloatingInteger("{=SurrenderTweaks18}Chance of Bribe", 0.0f, 10.0f, "0.0", Order = 0, RequireRestart = false, HintText = "{=SurrenderTweaks19}Multiplier for chance of bribe. Default is 1.0.")]
        [SettingPropertyGroup("{=SurrenderTweaks17}Multipliers")]
        public float BribeChanceMultiplier { get; set; } = 1.0f;
        [SettingPropertyFloatingInteger("{=SurrenderTweaks20}Chance of Surrender", 0.0f, 10.0f, "0.0", Order = 1, RequireRestart = false, HintText = "{=SurrenderTweaks21}Multiplier for chance of surrender. Default is 1.0.")]
        [SettingPropertyGroup("{=SurrenderTweaks17}Multipliers")]
        public float SurrenderChanceMultiplier { get; set; } = 1.0f;
        [SettingPropertyFloatingInteger("{=SurrenderTweaks22}Settlement Nutrition Bonus", 0.0f, 10.0f, "0.0", Order = 2, RequireRestart = false, HintText = "{=SurrenderTweaks23}Multiplier for a besieged settlement's nutrition bonus. Default is 1.0.")]
        [SettingPropertyGroup("{=SurrenderTweaks17}Multipliers")]
        public float NutritionBonusMultiplier { get; set; } = 1.0f;
        [SettingPropertyFloatingInteger("{=SurrenderTweaks24}Settlement Starvation Penalty", 0.0f, 10.0f, "0.0", Order = 3, RequireRestart = false, HintText = "{=SurrenderTweaks25}Multiplier for a besieged settlement's starvation penalty. Default is 1.0.")]
        [SettingPropertyGroup("{=SurrenderTweaks17}Multipliers")]
        public float StarvationPenaltyMultiplier { get; set; } = 1.0f;
        [SettingPropertyFloatingInteger("{=SurrenderTweaks26}Bribe Amount", 0.0f, 10.0f, "0.0", Order = 4, RequireRestart = false, HintText = "{=SurrenderTweaks27}Multiplier for the bribe amount. Default is 1.0.")]
        [SettingPropertyGroup("{=SurrenderTweaks17}Multipliers")]
        public float BribeAmountMultiplier { get; set; } = 1.0f;
    }
}
