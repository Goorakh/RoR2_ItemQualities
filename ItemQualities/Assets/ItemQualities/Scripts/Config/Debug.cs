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
            const string SectionName = "Debug";

            static ConfigEntry<bool> _logItemQualitiesConfig;
            public static bool LogItemQualities => _logItemQualitiesConfig?.Value ?? false;

            static ConfigEntry<bool> _enableDebugDraw;
            public static bool EnableDebugDraw => _enableDebugDraw?.Value ?? false;

            internal static void Init(ConfigFile configFile)
            {
                _logItemQualitiesConfig = configFile.Bind(new ConfigDefinition(SectionName, "Log Item Qualities"), false, new ConfigDescription("If messages about rolled or missing qualities should be logged"));

                _enableDebugDraw = configFile.Bind(new ConfigDefinition(SectionName, "Enable Debug Drawing"), false, new ConfigDescription("If debug drawing should be enabled"));
            }

            internal static void InitRiskOfOptions()
            {
                ModSettingsManager.AddOption(new CheckBoxOption(_logItemQualitiesConfig), ModGuid, ModName);

                ModSettingsManager.AddOption(new CheckBoxOption(_enableDebugDraw), ModGuid, ModName);
            }
#else
            public const bool LogItemQualities = false;

            public const bool EnableDebugDraw = false;
#endif
        }
    }
}
