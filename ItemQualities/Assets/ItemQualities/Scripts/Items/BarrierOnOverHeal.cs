using ItemQualities.Utilities.Extensions;
using R2API;
using RoR2;

namespace ItemQualities.Items
{
    static class BarrierOnOverHeal
    {
        [SystemInitializer]
        static void Init()
        {
            RecalculateStatsAPI.GetStatCoefficients += getStatCoefficients;
        }

        static void getStatCoefficients(CharacterBody sender, RecalculateStatsAPI.StatHookEventArgs args)
        {
            if (!sender.healthComponent || !sender.inventory)
                return;

            ItemQualityCounts barrierOnOverHeal = sender.inventory.GetItemCountsEffective(ItemQualitiesContent.ItemQualityGroups.BarrierOnOverHeal);
            if (barrierOnOverHeal.TotalQualityCount > 0 && sender.healthComponent.barrier > 0f)
            {
                args.armorAdd += (20 * barrierOnOverHeal.UncommonCount) +
                                 (50 * barrierOnOverHeal.RareCount) +
                                 (80 * barrierOnOverHeal.EpicCount) +
                                 (100 * barrierOnOverHeal.LegendaryCount);
            }
        }
    }
}
