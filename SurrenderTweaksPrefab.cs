using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.Prefabs2;

namespace SurrenderTweaks
{
    public class SurrenderTweaksPrefab
    {
        // Add the "Chance of Surrender" text to the Power Level bar in conversations.
        [PrefabExtension("PowerLevelComparer", "descendant::PowerLevelComparerWidget/Children/HintWidget", "MapConversation")]
        public class SurrenderTweaksConversationPrefab : PrefabExtensionInsertPatch
        {
            public override InsertType Type => InsertType.Append;

            [PrefabExtensionText]
            public string Text => "<TextWidget WidthSizePolicy=\"StretchToParent\" VerticalAlignment=\"Center\" Brush.FontSize=\"15\" Text=\"@SurrenderChance\"/>";
        }

        // Add the "Chance of Surrender" text to the Power Level bar in encounters.
        [PrefabExtension("PowerLevelComparer", "descendant::PowerLevelComparerWidget/Children/HintWidget", "EncounterOverlay")]
        public class SurrenderTweaksEncounterPrefab : PrefabExtensionInsertPatch
        {
            public override InsertType Type => InsertType.Append;

            [PrefabExtensionText]
            public string Text => "<TextWidget WidthSizePolicy=\"StretchToParent\" VerticalAlignment=\"Center\" Brush.FontSize=\"15\" Text=\"@SurrenderChance\"/>";
        }
    }
}
