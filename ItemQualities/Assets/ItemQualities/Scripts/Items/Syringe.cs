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
            int uncommonCount = 0;
            int rareCount = 0;
            int epicCount = 0;
            int legendaryCount = 0;
            if (sender.inventory)
            {
                uncommonCount = sender.inventory.GetItemCount(ItemQualitiesContent.ItemQualityGroups.Syringe.UncommonItemIndex);
                rareCount = sender.inventory.GetItemCount(ItemQualitiesContent.ItemQualityGroups.Syringe.RareItemIndex);
                epicCount = sender.inventory.GetItemCount(ItemQualitiesContent.ItemQualityGroups.Syringe.EpicItemIndex);
                legendaryCount = sender.inventory.GetItemCount(ItemQualitiesContent.ItemQualityGroups.Syringe.LegendaryItemIndex);
            }

            args.attackSpeedMultAdd += ((0.30f - 0.15f) * uncommonCount)
                                     + ((0.60f - 0.15f) * rareCount)
                                     + ((1.05f - 0.15f) * epicCount)
                                     + ((1.50f - 0.15f) * legendaryCount);
        }
    }
}
