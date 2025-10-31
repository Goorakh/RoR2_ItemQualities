using BepInEx.Configuration;
using RiskOfOptions;
using RiskOfOptions.OptionConfigs;
using RiskOfOptions.Options;
using System.Runtime.CompilerServices;

namespace ItemQualities
{
    partial class Configs
    {
        public static class General
        {
            const string SectionName = "General";

            public static ConfigEntry<bool> EnableDifficultyModifications { get; private set; }

            public static ConfigEntry<float> GlobalQualityChance { get; private set; }

            internal static void Init(ConfigFile configFile)
            {
                EnableDifficultyModifications = configFile.Bind(new ConfigDefinition(SectionName, "Difficulty Changes"), true, new ConfigDescription("Enable balance difficulty changes"));

                GlobalQualityChance = configFile.Bind(new ConfigDefinition(SectionName, "Global Quality Chance"), 4f, new ConfigDescription("The % chance for an item not from a quality chest to be of quality", new AcceptableValueRange<float>(0f, 100f)));
            }

            [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
            internal static void InitRiskOfOptions()
            {
                ModSettingsManager.AddOption(new CheckBoxOption(EnableDifficultyModifications));

                ModSettingsManager.AddOption(new SliderOption(GlobalQualityChance, new SliderConfig
                {
                    min = 0f,
                    max = 100f,
                    FormatString = "{0:0.#}%"
                }), ModGuid, ModName);
            }
        }
    }
}
