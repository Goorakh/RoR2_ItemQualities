using R2API;
using RoR2;

namespace ItemQualities.Buffs
{
    internal static class LifeStealSpeed
    {
        [SystemInitializer]
        private static void Init()
        {
            RecalculateStatsAPI.GetStatCoefficients += getStatCoefficients;
        }

        private static void getStatCoefficients(CharacterBody sender, RecalculateStatsAPI.StatHookEventArgs args)
        {
            int lifestealSpeedCount = sender.GetBuffCount(ItemQualitiesContent.Buffs.LifeStealSpeed);
            if (lifestealSpeedCount > 0)
            {
                args.moveSpeedMultAdd += 0.01f * lifestealSpeedCount;
            }
        }
    }
}
