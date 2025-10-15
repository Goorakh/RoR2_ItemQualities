using BepInEx.Configuration;
using RiskOfOptions;

namespace ItemQualities
{
    public static partial class Configs
    {
        const string ModGuid = ItemQualitiesPlugin.PluginGUID;
        const string ModName = ItemQualitiesPlugin.PluginName;

        internal static void Init(ConfigFile configFile)
        {
#if DEBUG
            Debug.Init(configFile);
#endif
        }

        internal static void InitRiskOfOptions()
        {
            ModSettingsManager.SetModDescription("Settings for ItemQualities", ModGuid, ModName);

            // TODO
            //ModSettingsManager.SetModIcon()

#if DEBUG
            Debug.InitRiskOfOptions();
#endif
        }
    }
}
