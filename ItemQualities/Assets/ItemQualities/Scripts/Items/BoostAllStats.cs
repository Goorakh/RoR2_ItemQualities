using R2API;
using RoR2;
using UnityEngine;

namespace ItemQualities.Items
{
    static class BoostAllStats
    {
        [SystemInitializer]
        static void Init()
        {
            RecalculateStatsAPI.GetStatCoefficients += getStatCoefficients;
        }

        static void getStatCoefficients(CharacterBody sender, RecalculateStatsAPI.StatHookEventArgs args)
        {
            ItemQualityCounts boostAllStats = ItemQualitiesContent.ItemQualityGroups.BoostAllStats.GetItemCountsEffective(sender.inventory);
            if (boostAllStats.TotalQualityCount > 0 && ItemQualitiesContent.BuffQualityGroups.BoostAllStatsBuff.HasQualityBuff(sender))
            {
                float cooldownMultiplier = Mathf.Pow(1f - 0.10f, boostAllStats.UncommonCount) *
                                           Mathf.Pow(1f - 0.20f, boostAllStats.RareCount) *
                                           Mathf.Pow(1f - 0.35f, boostAllStats.EpicCount) *
                                           Mathf.Pow(1f - 0.50f, boostAllStats.LegendaryCount);

                args.allSkills.cooldownMultiplier *= cooldownMultiplier;
            }
        }
    }
}
