using R2API;
using RoR2;

namespace ItemQualities.Items
{
    static class GoldOnHurt
    {
        [SystemInitializer]
        static void Init()
        {
            RecalculateStatsAPI.GetStatCoefficients += RecalculateStatsAPI_GetStatCoefficients;
        }

        static void RecalculateStatsAPI_GetStatCoefficients(CharacterBody sender, RecalculateStatsAPI.StatHookEventArgs args)
        {
            Inventory inventory = sender ? sender.inventory : null;

            ItemQualityCounts goldOnHurt = ItemQualitiesContent.ItemQualityGroups.GoldOnHurt.GetItemCountsEffective(inventory);
            if (goldOnHurt.TotalQualityCount > 0)
            {
                if (ItemQualitiesContent.BuffQualityGroups.GoldArmorBuff.HasQualityBuff(sender))
                {
                    args.armorAdd += (30f * goldOnHurt.UncommonCount) +
                                     (50f * goldOnHurt.RareCount) +
                                     (75f * goldOnHurt.EpicCount) +
                                     (100f * goldOnHurt.LegendaryCount);
                }
            }
        }
    }
}
