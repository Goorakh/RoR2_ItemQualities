using ItemQualities.Utilities.Extensions;
using R2API;
using RoR2;

namespace ItemQualities.Items
{
    static class Hoof
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

            ItemQualityCounts hoof = sender.inventory.GetItemCountsEffective(ItemQualitiesContent.ItemQualityGroups.Hoof);

            args.moveSpeedMultAdd += ((0.28f - 0.14f) * hoof.UncommonCount) +
                                     ((0.49f - 0.14f) * hoof.RareCount) +
                                     ((0.70f - 0.14f) * hoof.EpicCount) +
                                     ((0.98f - 0.14f) * hoof.LegendaryCount);
        }
    }
}
