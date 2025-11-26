using R2API;
using RoR2;
using UnityEngine;

namespace ItemQualities.Items
{
    static class BarrierOnKill
    {
        [SystemInitializer]
        static void Init()
        {
            RecalculateStatsAPI.GetStatCoefficients += getStatCoefficients;
        }

        static void getStatCoefficients(CharacterBody sender, RecalculateStatsAPI.StatHookEventArgs args)
        {
            ItemQualityCounts barrierOnKill = ItemQualitiesContent.ItemQualityGroups.BarrierOnKill.GetItemCountsEffective(sender.inventory);
            if (barrierOnKill.TotalQualityCount > 0)
            {
                args.barrierDecayMult *= Mathf.Pow(1f - 0.10f, barrierOnKill.UncommonCount) *
                                         Mathf.Pow(1f - 0.20f, barrierOnKill.RareCount) *
                                         Mathf.Pow(1f - 0.50f, barrierOnKill.EpicCount) *
                                         Mathf.Pow(1f - 0.75f, barrierOnKill.LegendaryCount);
            }
        }
    }
}
