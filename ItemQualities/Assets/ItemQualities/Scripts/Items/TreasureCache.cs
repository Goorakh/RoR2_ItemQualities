using ItemQualities.Utilities.Extensions;
using RoR2;

namespace ItemQualities.Items
{
    static class TreasureCache
    {
        [SystemInitializer]
        static void Init()
        {
            SpecificItemCostTransformationHooks.ModifyItemCostTransformation += modifyItemCostTransformation;
        }

        static void modifyItemCostTransformation(ref Inventory.ItemTransformation itemTransformation, Interactor activator, int cost)
        {
            if (itemTransformation.originalItemIndex != RoR2Content.Items.TreasureCache.itemIndex)
                return;

            CharacterBody activatorBody = activator ? activator.GetComponent<CharacterBody>() : null;
            Inventory activatorInventory = activatorBody ? activatorBody.inventory : null;
            if (!activatorInventory)
                return;

            ItemQualityCounts treasureCache = activatorInventory.GetItemCountsPermanent(ItemQualitiesContent.ItemQualityGroups.TreasureCache);
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
