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

            args.critDamageMultAdd += (critGlassesUncommonCount * (0.1f * 1))
                                    + (critGlassesRareCount * (0.1f * 2))
                                    + (critGlassesEpicCount * (0.1f * 3))
                                    + (critGlassesLegendaryCount * (0.1f * 4));
        }
    }
}
