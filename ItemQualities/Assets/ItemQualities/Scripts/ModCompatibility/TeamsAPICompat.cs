using RoR2;

namespace ItemQualities.ModCompatibility
{
    static class TeamsAPICompat
    {
        public static int TeamsCount => TeamCatalog.teamDefs.Length;
    }
}
