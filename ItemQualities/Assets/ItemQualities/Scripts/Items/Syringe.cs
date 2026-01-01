using ItemQualities.Utilities.Extensions;
using R2API;
using RoR2;

namespace ItemQualities.Items
{
    static class Syringe
    {
        [SystemInitializer]
        static void Init()
        {
            RecalculateStatsAPI.GetStatCoefficients += RecalculateStatsAPI_GetStatCoefficients;
        }

        static void RecalculateStatsAPI_GetStatCoefficients(CharacterBody sender, RecalculateStatsAPI.StatHookEventArgs args)
        {
            if (!sender.inventory)
                return;

            ItemQualityCounts syringe = sender.inventory.GetItemCountsEffective(ItemQualitiesContent.ItemQualityGroups.Syringe);

            if (syringe.TotalQualityCount > 0)
            {
                args.attackSpeedMultAdd += ((0.30f - 0.15f) * syringe.UncommonCount) +
                                           ((0.60f - 0.15f) * syringe.RareCount) +
                                           ((1.05f - 0.15f) * syringe.EpicCount) +
                                           ((1.50f - 0.15f) * syringe.LegendaryCount);
            }
        }
    }
}
