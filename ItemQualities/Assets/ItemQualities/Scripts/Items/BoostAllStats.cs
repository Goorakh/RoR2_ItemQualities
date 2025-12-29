using ItemQualities.Utilities.Extensions;
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
            if (!sender.inventory)
                return;

            ItemQualityCounts boostAllStats = sender.inventory.GetItemCountsEffective(ItemQualitiesContent.ItemQualityGroups.BoostAllStats);
            if (boostAllStats.TotalQualityCount > 0 && ItemQualitiesContent.BuffQualityGroups.BoostAllStatsBuff.HasQualityBuff(sender))
            {
                float cooldownMultiplier = Mathf.Pow(1f - 0.30f, boostAllStats.UncommonCount) *
                                           Mathf.Pow(1f - 0.40f, boostAllStats.RareCount) *
                                           Mathf.Pow(1f - 0.50f, boostAllStats.EpicCount) *
                                           Mathf.Pow(1f - 0.60f, boostAllStats.LegendaryCount);

                args.allSkills.cooldownMultiplier *= cooldownMultiplier;
            }
        }
    }
}
