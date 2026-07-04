using BepInEx.Configuration;
using RiskOfOptions;
using RoR2;
using System.Runtime.CompilerServices;

namespace ItemQualities
{
    public static partial class Configs
    {
        private const string ModGuid = ItemQualitiesPlugin.PluginGUID;
        private const string ModName = "Quality";

        internal static void Init(ConfigFile configFile)
        {
            configFile.SaveOnConfigSet = false;

            General.Init(configFile);
            Interface.Init(configFile);

#if DEBUG
            Debug.Init(configFile);
#endif

            configFile.SaveOnConfigSet = true;
            configFile.Save();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void addOption<TOption>(TOption option)
            where TOption : RiskOfOptions.Options.BaseOption
        {
            ModSettingsManager.AddOption(option, ModGuid, ModName);
        }

        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        internal static void InitRiskOfOptions()
        {
            ModSettingsManager.SetModDescription("Settings for Quality", ModGuid, ModName);

            RoR2Application.onLoad += () =>
            {
                if (ItemQualitiesContent.Sprites.ModIcon)
                {
                    ModSettingsManager.SetModIcon(ItemQualitiesContent.Sprites.ModIcon, ModGuid, ModName);
                }
            };

            General.InitRiskOfOptions();
            Interface.InitRiskOfOptions();

#if DEBUG
            Debug.InitRiskOfOptions();
#endif
        }
    }
}
