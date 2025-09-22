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
            int critGlassesUncommonCount = 0;
            int critGlassesRareCount = 0;
            int critGlassesEpicCount = 0;
            int critGlassesLegendaryCount = 0;
            if (sender.inventory)
            {
                critGlassesUncommonCount = sender.inventory.GetItemCount(ItemQualitiesContent.ItemQualityGroups.CritGlasses.UncommonItemIndex);
                critGlassesRareCount = sender.inventory.GetItemCount(ItemQualitiesContent.ItemQualityGroups.CritGlasses.RareItemIndex);
                critGlassesEpicCount = sender.inventory.GetItemCount(ItemQualitiesContent.ItemQualityGroups.CritGlasses.EpicItemIndex);
                critGlassesLegendaryCount = sender.inventory.GetItemCount(ItemQualitiesContent.ItemQualityGroups.CritGlasses.LegendaryItemIndex);
            }

            args.critDamageMultAdd += (0.2f * critGlassesUncommonCount)
                                    + (0.4f * critGlassesRareCount)
                                    + (1.0f * critGlassesEpicCount)
                                    + (1.5f * critGlassesLegendaryCount);
        }
    }
}
