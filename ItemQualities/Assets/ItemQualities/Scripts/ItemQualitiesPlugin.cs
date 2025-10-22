using BepInEx;
using BepInEx.Bootstrap;
using HG.Reflection;
using R2API.Utils;
using System.Diagnostics;
using System.IO;

[assembly: NetworkCompatibility(CompatibilityLevel.EveryoneMustHaveMod, VersionStrictness.EveryoneNeedSameModVersion)]
[assembly: SearchableAttribute.OptIn]

namespace ItemQualities
{
    [BepInDependency(RoR2BepInExPack.RoR2BepInExPack.PluginGUID)]
    [BepInDependency(R2API.R2API.PluginGUID)]
    [BepInDependency(R2API.RecalculateStatsAPI.PluginGUID)]
    [BepInDependency(R2API.LanguageAPI.PluginGUID)]
    [BepInDependency(R2API.ColorsAPI.PluginGUID)]
    [BepInDependency(R2API.PrefabAPI.PluginGUID)]
    [BepInDependency(R2API.DamageAPI.PluginGUID)]
    [BepInDependency(R2API.DeployableAPI.PluginGUID)]
    [BepInDependency(ReviveAPI.ReviveAPI.ModGuid)]
    [BepInDependency(RiskOfOptions.PluginInfo.PLUGIN_GUID, BepInDependency.DependencyFlags.SoftDependency)]
    [BepInPlugin(PluginGUID, PluginName, PluginVersion)]
    public class ItemQualitiesPlugin : BaseUnityPlugin
    {
        public const string PluginName = "ItemQualities";
        public const string PluginAuthor = "Gorakh";
        public const string PluginGUID = "com." + PluginAuthor + "." + PluginName;
        public const string PluginVersion = "1.0.0";

        static ItemQualitiesPlugin _instance;
        public static ItemQualitiesPlugin Instance => _instance;

        void Awake()
        {
            Stopwatch stopwatch = Stopwatch.StartNew();

            SingletonHelper.Assign(ref _instance, this);

            Log.Init(Logger);

            Configs.Init(Config);

            if (Chainloader.PluginInfos.ContainsKey(RiskOfOptions.PluginInfo.PLUGIN_GUID))
            {
                Configs.InitRiskOfOptions();
            }

            ItemQualitiesContent contentProvider = new ItemQualitiesContent();
            contentProvider.Register();

            LanguageFolderHandler.Register(Path.GetDirectoryName(Info.Location));

            stopwatch.Stop();
            Log.Message_NoCallerPrefix($"Initialized in {stopwatch.Elapsed.TotalMilliseconds:F0}ms");
        }

        void OnDestroy()
        {
            SingletonHelper.Unassign(ref _instance, this);
        }
    }
}
