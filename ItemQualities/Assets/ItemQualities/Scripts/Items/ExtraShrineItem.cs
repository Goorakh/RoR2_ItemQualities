using ItemQualities.Utilities;
using RoR2;

namespace ItemQualities.Items
{
    static class ExtraShrineItem
    {
        [SystemInitializer]
        static void Init()
        {
            On.RoR2.ShrineChanceBehavior.AddShrineStack += ShrineChanceBehavior_AddShrineStack;
        }

        private static void ShrineChanceBehavior_AddShrineStack(On.RoR2.ShrineChanceBehavior.orig_AddShrineStack orig, RoR2.ShrineChanceBehavior self, RoR2.Interactor activator)
        {
            TeamComponent interactorTeamComponent = activator.GetComponent<TeamComponent>();
            if (!interactorTeamComponent) {
                orig(self, activator);
                return;
            }
                
            ItemQualityCounts extraShrineItem = ItemQualityUtils.GetTeamItemCounts(ItemQualitiesContent.ItemQualityGroups.ExtraShrineItem, interactorTeamComponent.teamIndex, true);

            int purchasesUntilPriceincreaseStops;
            if (extraShrineItem.HighestQuality >= QualityTier.Rare) {
                purchasesUntilPriceincreaseStops = 2;
            } else if (extraShrineItem.HighestQuality == QualityTier.Uncommon) {
                purchasesUntilPriceincreaseStops = 1;
            } else {
                orig(self, activator);
                return;
            }

            if (self.successfulPurchaseCount >= purchasesUntilPriceincreaseStops) {
                self.costMultiplierPerPurchase = 1;
            }

            orig(self, activator);
        }
    }
}
