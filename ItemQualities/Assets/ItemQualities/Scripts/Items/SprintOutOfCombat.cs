using R2API;
using RoR2;

namespace ItemQualities.Items
{
    static class SprintOutOfCombat
    {
        [SystemInitializer]
        static void Init()
        {
            RecalculateStatsAPI.GetStatCoefficients += getStatCoefficients;
        }

        static void getStatCoefficients(CharacterBody sender, RecalculateStatsAPI.StatHookEventArgs args)
        {
            if (!sender)
                return;

            ItemQualityCounts sprintOutOfCombat = ItemQualitiesContent.ItemQualityGroups.SprintOutOfCombat.GetItemCountsEffective(sender.inventory);
            BuffQualityCounts whipBoostBuff = ItemQualitiesContent.BuffQualityGroups.WhipBoost.GetBuffCounts(sender);

            if (whipBoostBuff.TotalQualityCount > 0)
            {
                args.moveSpeedMultAdd += (0.2f * sprintOutOfCombat.UncommonCount) +
                                         (0.4f * sprintOutOfCombat.RareCount) +
                                         (0.7f * sprintOutOfCombat.EpicCount) +
                                         (1.0f * sprintOutOfCombat.LegendaryCount);
            }
        }
    }
}
