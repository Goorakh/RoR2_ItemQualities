using ItemQualities.Utilities;
using RoR2;
using System;

namespace ItemQualities.Items
{
    static class ExtraShrineItem
    {
        [SystemInitializer]
        static void Init()
        {
            On.RoR2.ShrineChanceBehavior.AddShrineStack += ShrineChanceBehavior_AddShrineStack;
        }

        private static void ShrineChanceBehavior_AddShrineStack(On.RoR2.ShrineChanceBehavior.orig_AddShrineStack orig, ShrineChanceBehavior self, Interactor activator)
        {
            try
            {
                if (activator.TryGetComponent(out TeamComponent interactorTeamComponent))
                {
                    ItemQualityCounts extraShrineItem = ItemQualityUtils.GetTeamItemCounts(ItemQualitiesContent.ItemQualityGroups.ExtraShrineItem, interactorTeamComponent.teamIndex, true);
                    if (extraShrineItem.TotalQualityCount > 0)
                    {
                        int maxPurchaseCountForCostIncrease;
                        if (extraShrineItem.HighestQuality > QualityTier.Uncommon)
                        {
                            maxPurchaseCountForCostIncrease = 1;
                        }
                        else
                        {
                            maxPurchaseCountForCostIncrease = 2;
                        }

                        if (self.successfulPurchaseCount >= maxPurchaseCountForCostIncrease)
                        {
                            self.costMultiplierPerPurchase = 1;
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Log.Error_NoCallerPrefix(e);
            }

            orig(self, activator);
        }
    }
}
