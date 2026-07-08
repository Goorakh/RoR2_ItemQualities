using BepInEx.Configuration;
using RiskOfOptions.Options;
using System.Runtime.CompilerServices;

namespace ItemQualities
{
    partial class Configs
    {
        public static class Interface
        {
            private const string SectionName = "UI";

            public static ConfigEntry<bool> EnableQualityItemSorting { get; private set; }

            internal static void Init(ConfigFile configFile)
            {
                EnableQualityItemSorting = configFile.Bind(new ConfigDefinition(SectionName, "Enable Quality Item Grouping"), true, new ConfigDescription("If enabled, all quality items (including non-quality item) will be sorted and grouped together in inventories."));
            }

            [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
            internal static void InitRiskOfOptions()
            {
                addOption(new CheckBoxOption(EnableQualityItemSorting));
            }
        }
    }
}
