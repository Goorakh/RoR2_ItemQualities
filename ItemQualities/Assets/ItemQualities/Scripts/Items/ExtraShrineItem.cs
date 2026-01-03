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
            if (!interactorTeamComponent)
                return;
            ItemQualityCounts extraShrineItem = ItemQualityUtils.GetTeamItemCounts(ItemQualitiesContent.ItemQualityGroups.ExtraShrineItem, interactorTeamComponent.teamIndex, true);

            if ((extraShrineItem.HighestQuality >= QualityTier.Rare && self.successfulPurchaseCount >= 1) ||
            (extraShrineItem.HighestQuality >= QualityTier.Uncommon && self.successfulPurchaseCount >= 2))
            {
                self.costMultiplierPerPurchase = 1;
            }

            orig(self, activator);
        }
    }
}
