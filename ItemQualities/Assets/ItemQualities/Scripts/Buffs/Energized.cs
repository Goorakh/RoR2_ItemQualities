using ItemQualities.Utilities.Extensions;
using R2API;
using RoR2;

namespace ItemQualities.Buffs
{
    static class Energized
    {
        [SystemInitializer]
        static void Init()
        {
            RecalculateStatsAPI.GetStatCoefficients += RecalculateStatsAPI_GetStatCoefficients;
        }

        static void RecalculateStatsAPI_GetStatCoefficients(CharacterBody sender, RecalculateStatsAPI.StatHookEventArgs args)
        {
            if (!sender)
                return;

            BuffQualityCounts energized = sender.GetBuffCounts(ItemQualitiesContent.BuffQualityGroups.Energized);
            if (energized.TotalQualityCount > 0)
            {
                float bonusAttackSpeed = (0.1f * energized.UncommonCount) +
                                         (0.3f * energized.RareCount) +
                                         (0.6f * energized.EpicCount) +
                                         (1.0f * energized.LegendaryCount);

                args.attackSpeedMultAdd += 0.7f + bonusAttackSpeed;
            }
        }
    }
}
