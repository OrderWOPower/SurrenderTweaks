using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.Prefabs2;

namespace SurrenderTweaks
{
    // Add the "Chance of Surrender" text to the Power Level bar.
    [PrefabExtension("PowerLevelComparer", "descendant::PowerLevelComparerWidget/Children/HintWidget")]
    public class SurrenderTweaksPrefab : PrefabExtensionInsertPatch
    {
        public override InsertType Type => InsertType.Append;

        [PrefabExtensionText]
        public string Text => "<TextWidget WidthSizePolicy=\"StretchToParent\" VerticalAlignment=\"Center\" Brush.FontSize=\"15\" Text=\"@SurrenderChance\"/>";
    }
}
