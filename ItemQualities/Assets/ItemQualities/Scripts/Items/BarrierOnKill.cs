using ItemQualities.Utilities.Extensions;
using R2API;
using RoR2;
using UnityEngine;

namespace ItemQualities.Items
{
    internal static class BarrierOnKill
    {
        [SystemInitializer]
        private static void Init()
        {
            RecalculateStatsAPI.GetStatCoefficients += getStatCoefficients;
        }

        private static void getStatCoefficients(CharacterBody sender, RecalculateStatsAPI.StatHookEventArgs args)
        {
            if (!sender.inventory)
                return;

            ItemQualityCounts barrierOnKill = sender.inventory.GetItemCountsEffective(ItemQualitiesContent.ItemQualityGroups.BarrierOnKill);
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
