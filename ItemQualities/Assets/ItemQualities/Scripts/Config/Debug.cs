using BepInEx.Configuration;
using RiskOfOptions.Options;

namespace ItemQualities
{
    partial class Configs
    {
        public static class Debug
        {
#if DEBUG
            private const string SectionName = "Debug";

            private static ConfigEntry<bool> _logItemQualitiesConfig;
            public static bool LogItemQualities => _logItemQualitiesConfig?.Value ?? false;

            private static ConfigEntry<bool> _enableDebugDraw;
            public static bool EnableDebugDraw => _enableDebugDraw?.Value ?? false;

            internal static void Init(ConfigFile configFile)
            {
                _logItemQualitiesConfig = configFile.Bind(new ConfigDefinition(SectionName, "Log Item Qualities"), false, new ConfigDescription("If messages about rolled or missing qualities should be logged"));

                _enableDebugDraw = configFile.Bind(new ConfigDefinition(SectionName, "Enable Debug Drawing"), false, new ConfigDescription("If debug drawing should be enabled"));
            }

            internal static void InitRiskOfOptions()
            {
                addOption(new CheckBoxOption(_logItemQualitiesConfig));

                addOption(new CheckBoxOption(_enableDebugDraw));
            }
#else
            public const bool LogItemQualities = false;

            public const bool EnableDebugDraw = false;
#endif
        }
    }
}
