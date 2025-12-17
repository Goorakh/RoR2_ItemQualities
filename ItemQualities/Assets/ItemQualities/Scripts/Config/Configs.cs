using BepInEx.Configuration;
using RiskOfOptions;
using RoR2;
using System.Runtime.CompilerServices;

namespace ItemQualities
{
    public static partial class Configs
    {
        const string ModGuid = ItemQualitiesPlugin.PluginGUID;
        const string ModName = ItemQualitiesPlugin.PluginName;

        internal static void Init(ConfigFile configFile)
        {
            General.Init(configFile);

#if DEBUG
            Debug.Init(configFile);
#endif
        }

        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        internal static void InitRiskOfOptions()
        {
            ModSettingsManager.SetModDescription("Settings for ItemQualities", ModGuid, ModName);

            RoR2Application.onLoad += () =>
            {
                if (ItemQualitiesContent.Sprites.ModIcon)
                {
                    ModSettingsManager.SetModIcon(ItemQualitiesContent.Sprites.ModIcon, ModGuid, ModName);
                }
            };

            General.InitRiskOfOptions();

#if DEBUG
            Debug.InitRiskOfOptions();
#endif
        }
    }
}
