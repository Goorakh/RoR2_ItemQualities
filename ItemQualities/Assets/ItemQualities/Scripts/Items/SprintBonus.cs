using R2API;
using RoR2;

namespace ItemQualities.Items
{
    static class SprintBonus
    {
        [SystemInitializer]
        static void Init()
        {
            RecalculateStatsAPI.GetStatCoefficients += RecalculateStatsAPI_GetStatCoefficients;
        }

        static void RecalculateStatsAPI_GetStatCoefficients(CharacterBody sender, RecalculateStatsAPI.StatHookEventArgs args)
        {
            ItemQualityCounts sprintBonus = ItemQualitiesContent.ItemQualityGroups.SprintBonus.GetItemCountsEffective(sender.inventory);

            if (sender.isSprinting && sprintBonus.TotalQualityCount > 0)
            {
                args.moveSpeedMultAdd += (((0.40f - 0.25f) * sprintBonus.UncommonCount) +
                                          ((0.70f - 0.25f) * sprintBonus.RareCount) +
                                          ((1.00f - 0.25f) * sprintBonus.EpicCount) +
                                          ((1.50f - 0.25f) * sprintBonus.LegendaryCount)) / sender.sprintingSpeedMultiplier;
            }
        }
    }
}
