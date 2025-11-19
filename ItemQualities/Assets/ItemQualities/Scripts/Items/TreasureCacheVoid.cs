using RoR2;

namespace ItemQualities.Items
{
    static class TreasureCacheVoid
    {
        [SystemInitializer]
        static void Init()
        {
            SpecificItemCostTransformationHooks.ModifyItemCostTransformation += modifyItemCostTransformation;
        }

        static void modifyItemCostTransformation(ref Inventory.ItemTransformation itemTransformation, Interactor activator, int cost)
        {
            if (itemTransformation.originalItemIndex != DLC1Content.Items.TreasureCacheVoid.itemIndex)
                return;

            CharacterBody activatorBody = activator ? activator.GetComponent<CharacterBody>() : null;
            Inventory activatorInventory = activatorBody ? activatorBody.inventory : null;
            if (!activatorInventory)
                return;

            ItemQualityCounts treasureCache = ItemQualitiesContent.ItemQualityGroups.TreasureCacheVoid.GetItemCountsPermanent(activatorInventory);
            for (QualityTier qualityTier = QualityTier.Count - 1; qualityTier > QualityTier.None; qualityTier--)
            {
                if (treasureCache[qualityTier] >= cost)
                {
                    itemTransformation.originalItemIndex = QualityCatalog.GetItemIndexOfQuality(itemTransformation.originalItemIndex, qualityTier);
                    itemTransformation.newItemIndex = QualityCatalog.GetItemIndexOfQuality(itemTransformation.newItemIndex, qualityTier);
                    break;
                }
            }
        }
    }
}
