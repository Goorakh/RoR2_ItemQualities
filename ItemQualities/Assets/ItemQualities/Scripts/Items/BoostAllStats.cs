using RoR2;
using System;
using UnityEngine;

namespace ItemQualities.Items
{
    static class BoostAllStats
    {
        [SystemInitializer]
        static void Init()
        {
            On.RoR2.CharacterBody.RecalculateStats += CharacterBody_RecalculateStats;
        }

        static void CharacterBody_RecalculateStats(On.RoR2.CharacterBody.orig_RecalculateStats orig, CharacterBody self)
        {
            orig(self);

            // RecalculateStatsAPI does not have a way to do cooldown multipliers, so we have to do it ourselves

            try
            {
                ItemQualityCounts boostAllStats = ItemQualitiesContent.ItemQualityGroups.BoostAllStats.GetItemCountsEffective(self.inventory);
                if (boostAllStats.TotalQualityCount > 0 && ItemQualitiesContent.BuffQualityGroups.BoostAllStatsBuff.HasQualityBuff(self))
                {
                    float cooldownMultiplier = Mathf.Pow(1f - 0.10f, boostAllStats.UncommonCount) *
                                               Mathf.Pow(1f - 0.20f, boostAllStats.RareCount) *
                                               Mathf.Pow(1f - 0.35f, boostAllStats.EpicCount) *
                                               Mathf.Pow(1f - 0.50f, boostAllStats.LegendaryCount);

                    if (self.skillLocator.primary)
                    {
                        self.skillLocator.primary.cooldownScale *= cooldownMultiplier;
                    }

                    if (self.skillLocator.secondaryBonusStockSkill)
                    {
                        self.skillLocator.secondaryBonusStockSkill.cooldownScale *= cooldownMultiplier;
                    }

                    if (self.skillLocator.utilityBonusStockSkill)
                    {
                        self.skillLocator.utilityBonusStockSkill.cooldownScale *= cooldownMultiplier;
                    }

                    if (self.skillLocator.specialBonusStockSkill)
                    {
                        self.skillLocator.specialBonusStockSkill.cooldownScale *= cooldownMultiplier;
                    }
                }
            }
            catch (Exception e)
            {
                Log.Error_NoCallerPrefix(e);
            }
        }
    }
}
