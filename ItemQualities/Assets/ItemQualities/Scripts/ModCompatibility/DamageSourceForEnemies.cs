using BepInEx.Bootstrap;

namespace ItemQualities.ModCompatibility
{
    internal static class DamageSourceForEnemies
    {
        public const string GUID = "LordVGames.DamageSourceForEnemies";

        public static bool Enabled => Chainloader.PluginInfos.ContainsKey(GUID);
    }
}
