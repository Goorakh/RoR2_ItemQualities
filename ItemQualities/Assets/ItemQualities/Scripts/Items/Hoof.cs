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

            args.moveSpeedMultAdd += hoofUncommonCount * (0.14f * 2);
            args.moveSpeedMultAdd += hoofRareCount * (0.14f * 3);
            args.moveSpeedMultAdd += hoofEpicCount * (0.14f * 4);
            args.moveSpeedMultAdd += hoofLegendaryCount * (0.14f * 5);
        }
    }
}
