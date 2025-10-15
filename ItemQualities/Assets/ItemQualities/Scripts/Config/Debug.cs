using BepInEx.Configuration;
using RiskOfOptions;
using RiskOfOptions.Options;

namespace ItemQualities
{
    partial class Configs
    {
        public static class Debug
        {
#if DEBUG
            public static bool LogItemQualities => _logItemQualitiesConfig?.Value ?? false;

            static ConfigEntry<bool> _logItemQualitiesConfig;

            internal static void Init(ConfigFile configFile)
            {
                _logItemQualitiesConfig = configFile.Bind(new ConfigDefinition("Debug", "Log Item Qualities"), false, new ConfigDescription("If messages about rolled or missing qualities should be logged"));
            }

            internal static void InitRiskOfOptions()
            {
                ModSettingsManager.AddOption(new CheckBoxOption(_logItemQualitiesConfig), ModGuid, ModName);
            }
#else
            public const bool LogItemQualities = false;
#endif
        }
    }
}
