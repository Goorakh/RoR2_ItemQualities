using BepInEx.Bootstrap;

namespace ItemQualities.ModCompatibility
{
    internal static class FathomlessCompat
    {
        public static bool Enabled => Chainloader.PluginInfos.ContainsKey(FathomlessVoidling.Main.PluginGUID);
    }
}
