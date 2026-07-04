using BepInEx.Configuration;
using ItemQualities.Config;
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
            private const string SectionName = "General";

            public static ConfigEntry<float> DifficultyCoefficientMultiplier { get; private set; }

            public static ConfigEntry<float> GlobalQualityChance { get; private set; }

            internal static void Init(ConfigFile configFile)
            {
                DifficultyCoefficientMultiplier = configFile.Bind(new ConfigDefinition(SectionName, "Difficulty Multiplier"), 1.25f, new ConfigDescription("Multiplier to difficulty scaling.", new AcceptableValueMin<float>(1f)));

                GlobalQualityChance = configFile.Bind(new ConfigDefinition(SectionName, "Global Quality Chance"), 4f, new ConfigDescription("The % chance for an item not from a quality chest to be of quality", new AcceptableValueRange<float>(0f, 100f)));
            }

            [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
            internal static void InitRiskOfOptions()
            {
                addOption(new SliderOption(DifficultyCoefficientMultiplier, new SliderConfig
                {
                    min = 1f,
                    max = 5f,
                    FormatString = "{0:0.##}x"
                }));

                addOption(new SliderOption(GlobalQualityChance, new SliderConfig
                {
                    min = 0f,
                    max = 100f,
                    FormatString = "{0:0.#}%"
                }));
            }
        }
    }
}
