using BepInEx.Bootstrap;

namespace ItemQualities.ModCompatibility
{
    internal static class UmbralCompat
    {
        public static bool Enabled => Chainloader.PluginInfos.ContainsKey("com.Nuxlar.UmbralMithrix");
    }
}
