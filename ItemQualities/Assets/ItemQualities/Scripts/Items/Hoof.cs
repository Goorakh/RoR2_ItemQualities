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
            int hoofUncommonCount = 0;
            int hoofRareCount = 0;
            int hoofEpicCount = 0;
            int hoofLegendaryCount = 0;
            if (sender.inventory)
            {
                hoofUncommonCount = sender.inventory.GetItemCount(ItemQualitiesContent.ItemQualityGroups.Hoof.UncommonItemIndex);
                hoofRareCount = sender.inventory.GetItemCount(ItemQualitiesContent.ItemQualityGroups.Hoof.RareItemIndex);
                hoofEpicCount = sender.inventory.GetItemCount(ItemQualitiesContent.ItemQualityGroups.Hoof.EpicItemIndex);
                hoofLegendaryCount = sender.inventory.GetItemCount(ItemQualitiesContent.ItemQualityGroups.Hoof.LegendaryItemIndex);
            }

            args.moveSpeedMultAdd += ((0.35f - 0.14f) * hoofUncommonCount)
                                   + ((0.56f - 0.14f) * hoofRareCount)
                                   + ((0.98f - 0.14f) * hoofEpicCount)
                                   + ((1.40f - 0.14f) * hoofLegendaryCount);
        }
    }
}
