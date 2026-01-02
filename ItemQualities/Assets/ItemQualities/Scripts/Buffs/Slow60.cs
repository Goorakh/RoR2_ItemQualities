using ItemQualities.Utilities.Extensions;
using R2API;
using RoR2;

namespace ItemQualities.Buffs
{
    static class Slow60
    {
        [SystemInitializer]
        static void Init()
        {
            RecalculateStatsAPI.GetStatCoefficients += RecalculateStatsAPI_GetStatCoefficients;
        }

        static void RecalculateStatsAPI_GetStatCoefficients(CharacterBody sender, RecalculateStatsAPI.StatHookEventArgs args)
        {
            BuffQualityCounts slow60 = sender.GetBuffCounts(ItemQualitiesContent.BuffQualityGroups.Slow60);

            args.moveSpeedReductionMultAdd += (1 * slow60.UncommonCount) +
                                              (2 * slow60.RareCount) +
                                              (3 * slow60.EpicCount) +
                                              (5 * slow60.LegendaryCount);
        }
    }
}
