using BepInEx.Bootstrap;

namespace ItemQualities.ModCompatibility
{
    internal static class ProperSaveCompat
    {
        public static bool Enabled => Chainloader.PluginInfos.ContainsKey(ProperSave.ProperSavePlugin.GUID);
    }
}
