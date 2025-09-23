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
            ItemQualityCounts hoof = default;
            if (sender.inventory)
            {
                hoof = ItemQualitiesContent.ItemQualityGroups.Hoof.GetItemCounts(sender.inventory);
            }

            args.moveSpeedMultAdd += ((0.28f - 0.14f) * hoof.UncommonCount)
                                   + ((0.49f - 0.14f) * hoof.RareCount)
                                   + ((0.70f - 0.14f) * hoof.EpicCount)
                                   + ((0.98f - 0.14f) * hoof.LegendaryCount);
        }
    }
}
