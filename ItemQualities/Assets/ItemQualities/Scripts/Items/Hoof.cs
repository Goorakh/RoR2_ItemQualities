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

            args.moveSpeedMultAdd += ((0.25f - 0.14f) * hoof.UncommonCount) +
                                     ((0.40f - 0.14f) * hoof.RareCount) +
                                     ((0.60f - 0.14f) * hoof.EpicCount) +
                                     ((0.75f - 0.14f) * hoof.LegendaryCount);
        }
    }
}
