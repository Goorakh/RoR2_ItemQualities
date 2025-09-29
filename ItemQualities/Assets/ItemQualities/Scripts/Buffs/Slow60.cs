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
            BuffQualityCounts slow60 = ItemQualitiesContent.BuffQualityGroups.Slow60.GetBuffCounts(sender);

            args.moveSpeedReductionMultAdd += (0.9f * slow60.UncommonCount) +
                                              (1.2f * slow60.RareCount) +
                                              (1.5f * slow60.EpicCount) +
                                              (2.0f * slow60.LegendaryCount);
        }
    }
}
