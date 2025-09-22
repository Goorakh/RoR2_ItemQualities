using R2API;
using RoR2;

namespace ItemQualities.Items
{
    class SprintBonus
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
                uncommonCount = sender.inventory.GetItemCount(ItemQualitiesContent.ItemQualityGroups.SprintBonus.UncommonItemIndex);
                rareCount = sender.inventory.GetItemCount(ItemQualitiesContent.ItemQualityGroups.SprintBonus.RareItemIndex);
                epicCount = sender.inventory.GetItemCount(ItemQualitiesContent.ItemQualityGroups.SprintBonus.EpicItemIndex);
                legendaryCount = sender.inventory.GetItemCount(ItemQualitiesContent.ItemQualityGroups.SprintBonus.LegendaryItemIndex);
            }

            if (sender.isSprinting)
            {
                args.moveSpeedMultAdd += (((0.1f * 1) * uncommonCount)
                                        + ((0.1f * 2) * rareCount)
                                        + ((0.1f * 3) * epicCount)
                                        + ((0.1f * 4) * legendaryCount)) / sender.sprintingSpeedMultiplier;
            }
        }
    }
}
