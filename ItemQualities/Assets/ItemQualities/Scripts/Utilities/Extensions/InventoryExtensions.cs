using RoR2;

namespace ItemQualities.Utilities.Extensions
{
    public static class InventoryExtensions
    {
        public static bool HasAtLeastXTotalQualityItemsOfTier(this Inventory inventory, ItemTier itemTier, int x)
        {
            if (x <= 0)
                return true;

            int totalCount = 0;
            foreach (ItemIndex itemIndex in inventory.itemAcquisitionOrder)
            {
                ItemDef itemDef = ItemCatalog.GetItemDef(itemIndex);
                if (itemDef && itemDef.tier == itemTier && QualityCatalog.GetQualityTier(itemIndex) > QualityTier.None)
                {
                    totalCount += inventory.GetItemCount(itemIndex);
                    if (totalCount >= x)
                        return true;
                }
            }

            return false;
        }

        public static bool HasAtLeastXTotalNonQualityItemsOfTier(this Inventory inventory, ItemTier itemTier, int x)
        {
            if (x <= 0)
                return true;

            int totalCount = 0;
            foreach (ItemIndex itemIndex in inventory.itemAcquisitionOrder)
            {
                ItemDef itemDef = ItemCatalog.GetItemDef(itemIndex);
                if (itemDef && itemDef.tier == itemTier && QualityCatalog.GetQualityTier(itemIndex) == QualityTier.None)
                {
                    totalCount += inventory.GetItemCount(itemIndex);
                    if (totalCount >= x)
                        return true;
                }
            }

            return false;
        }
    }
}
