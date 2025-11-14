using RoR2;

namespace ItemQualities.Utilities.Extensions
{
    public static class InventoryExtensions
    {
        public static bool HasAtLeastXTotalRemovableQualityItemsOfTier(this Inventory inventory, ItemTier itemTier, int x)
        {
            if (x <= 0)
                return true;

            int totalCount = 0;
            foreach (ItemIndex itemIndex in inventory.itemAcquisitionOrder)
            {
                ItemDef itemDef = ItemCatalog.GetItemDef(itemIndex);
                if (itemDef && itemDef.canRemove && itemDef.tier == itemTier && QualityCatalog.GetQualityTier(itemIndex) > QualityTier.None)
                {
                    totalCount += inventory.GetItemCount(itemIndex);
                    if (totalCount >= x)
                        return true;
                }
            }

            return false;
        }

        public static bool HasAtLeastXTotalNonQualityItemsOfTierForPurchase(this Inventory inventory, ItemTier itemTier, int x)
        {
            if (x <= 0)
                return true;

            int totalCount = 0;
            foreach (ItemIndex itemIndex in inventory.itemAcquisitionOrder)
            {
                if (QualityCatalog.GetQualityTier(itemIndex) == QualityTier.None ||
                    QualityCatalog.FindItemQualityGroupIndex(itemIndex) == ItemQualitiesContent.ItemQualityGroups.RegeneratingScrap.GroupIndex)
                {
                    ItemDef itemDef = ItemCatalog.GetItemDef(itemIndex);
                    if (itemDef && itemDef.canRemove && itemDef.tier == itemTier)
                    {
                        totalCount += inventory.GetItemCount(itemIndex);
                        if (totalCount >= x)
                            return true;
                    }
                }
            }

            return false;
        }
    }
}
