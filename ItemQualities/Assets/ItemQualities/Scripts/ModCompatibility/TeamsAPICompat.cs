using RoR2;

namespace ItemQualities.ModCompatibility
{
    internal static class TeamsAPICompat
    {
        public static int TeamsCount => TeamCatalog.teamDefs.Length;
    }
}
