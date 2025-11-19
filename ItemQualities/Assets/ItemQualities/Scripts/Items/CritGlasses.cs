using R2API;
using RoR2;

namespace ItemQualities.Items
{
    static class CritGlasses
    {
        [SystemInitializer]
        static void Init()
        {
            RecalculateStatsAPI.GetStatCoefficients += RecalculateStatsAPI_GetStatCoefficients;
        }

        static void RecalculateStatsAPI_GetStatCoefficients(CharacterBody sender, RecalculateStatsAPI.StatHookEventArgs args)
        {
            ItemQualityCounts critGlasses = ItemQualitiesContent.ItemQualityGroups.CritGlasses.GetItemCountsEffective(sender.inventory);
            if (critGlasses.TotalQualityCount > 0)
            {
                args.critDamageMultAdd += (0.2f * critGlasses.UncommonCount) +
                                          (0.4f * critGlasses.RareCount) +
                                          (1.0f * critGlasses.EpicCount) +
                                          (1.5f * critGlasses.LegendaryCount);
            }
        }
    }
}
