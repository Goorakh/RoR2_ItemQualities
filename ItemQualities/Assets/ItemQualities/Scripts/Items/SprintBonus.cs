using ItemQualities.Utilities.Extensions;
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
            if (!sender.inventory)
                return;

            ItemQualityCounts sprintBonus = sender.inventory.GetItemCountsEffective(ItemQualitiesContent.ItemQualityGroups.SprintBonus);

            if (sender.isSprinting && sprintBonus.TotalQualityCount > 0)
            {
                args.moveSpeedMultAdd += (((0.40f - 0.25f) * sprintBonus.UncommonCount) +
                                          ((0.50f - 0.25f) * sprintBonus.RareCount) +
                                          ((0.65f - 0.25f) * sprintBonus.EpicCount) +
                                          ((0.80f - 0.25f) * sprintBonus.LegendaryCount)) / sender.sprintingSpeedMultiplier;
            }
        }
    }
}
