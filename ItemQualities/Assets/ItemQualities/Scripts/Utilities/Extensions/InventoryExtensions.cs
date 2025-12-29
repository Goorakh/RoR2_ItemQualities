using RoR2;
using System;

namespace ItemQualities.Utilities.Extensions
{
    public static class InventoryExtensions
    {
        public static ItemQualityCounts GetItemCountsEffective(this Inventory inventory, ItemQualityGroupIndex itemGroupIndex)
        {
            return inventory.GetItemCountsEffective(QualityCatalog.GetItemQualityGroup(itemGroupIndex));
        }

        public static ItemQualityCounts GetItemCountsEffective(this Inventory inventory, ItemQualityGroup itemGroup)
        {
            if (!inventory)
                throw new ArgumentNullException(nameof(inventory));

            if (!itemGroup)
                return default;

            int baseItemCount = inventory.CalculateEffectiveItemStacks(itemGroup.BaseItemIndex);
            int uncommonItemCount = inventory.GetItemCountEffective(itemGroup.UncommonItemIndex);
            int rareItemCount = inventory.GetItemCountEffective(itemGroup.RareItemIndex);
            int epicItemCount = inventory.GetItemCountEffective(itemGroup.EpicItemIndex);
            int legendaryItemCount = inventory.GetItemCountEffective(itemGroup.LegendaryItemIndex);

            return new ItemQualityCounts(baseItemCount, uncommonItemCount, rareItemCount, epicItemCount, legendaryItemCount);
        }

        public static ItemQualityCounts GetItemCountsPermanent(this Inventory inventory, ItemQualityGroupIndex itemGroupIndex)
        {
            return inventory.GetItemCountsPermanent(QualityCatalog.GetItemQualityGroup(itemGroupIndex));
        }

        public static ItemQualityCounts GetItemCountsPermanent(this Inventory inventory, ItemQualityGroup itemGroup)
        {
            if (!inventory)
                throw new ArgumentNullException(nameof(inventory));

            if (!itemGroup)
                return default;

            int baseItemCount = inventory.GetItemCountPermanent(itemGroup.BaseItemIndex);
            int uncommonItemCount = inventory.GetItemCountPermanent(itemGroup.UncommonItemIndex);
            int rareItemCount = inventory.GetItemCountPermanent(itemGroup.RareItemIndex);
            int epicItemCount = inventory.GetItemCountPermanent(itemGroup.EpicItemIndex);
            int legendaryItemCount = inventory.GetItemCountPermanent(itemGroup.LegendaryItemIndex);

            return new ItemQualityCounts(baseItemCount, uncommonItemCount, rareItemCount, epicItemCount, legendaryItemCount);
        }

        public static ItemQualityCounts GetItemCountsTemp(this Inventory inventory, ItemQualityGroupIndex itemGroupIndex)
        {
            return inventory.GetItemCountsTemp(QualityCatalog.GetItemQualityGroup(itemGroupIndex));
        }

        public static ItemQualityCounts GetItemCountsTemp(this Inventory inventory, ItemQualityGroup itemGroup)
        {
            if (!inventory)
                throw new ArgumentNullException(nameof(inventory));

            if (!itemGroup)
                return default;

            int baseItemCount = inventory.GetItemCountTemp(itemGroup.BaseItemIndex);
            int uncommonItemCount = inventory.GetItemCountTemp(itemGroup.UncommonItemIndex);
            int rareItemCount = inventory.GetItemCountTemp(itemGroup.RareItemIndex);
            int epicItemCount = inventory.GetItemCountTemp(itemGroup.EpicItemIndex);
            int legendaryItemCount = inventory.GetItemCountTemp(itemGroup.LegendaryItemIndex);

            return new ItemQualityCounts(baseItemCount, uncommonItemCount, rareItemCount, epicItemCount, legendaryItemCount);
        }

        public static TempItemQualityCounts GetTempItemsDecayValue(this Inventory inventory, ItemQualityGroupIndex itemGroupIndex)
        {
            return inventory.GetTempItemsDecayValue(QualityCatalog.GetItemQualityGroup(itemGroupIndex));
        }

        public static TempItemQualityCounts GetTempItemsDecayValue(this Inventory inventory, ItemQualityGroup itemGroup)
        {
            if (!inventory)
                throw new ArgumentNullException(nameof(inventory));

            if (!itemGroup)
                return default;

            float baseDecayValue = inventory.GetTempItemDecayValue(itemGroup.BaseItemIndex);
            float uncommonDecayValue = inventory.GetTempItemDecayValue(itemGroup.UncommonItemIndex);
            float rareDecayValue = inventory.GetTempItemDecayValue(itemGroup.RareItemIndex);
            float epicDecayValue = inventory.GetTempItemDecayValue(itemGroup.EpicItemIndex);
            float legendaryDecayValue = inventory.GetTempItemDecayValue(itemGroup.LegendaryItemIndex);

            return new TempItemQualityCounts(baseDecayValue, uncommonDecayValue, rareDecayValue, epicDecayValue, legendaryDecayValue);
        }

        public static TempItemQualityCounts GetTempItemsRawValue(this Inventory inventory, ItemQualityGroupIndex itemGroupIndex)
        {
            return inventory.GetTempItemsRawValue(QualityCatalog.GetItemQualityGroup(itemGroupIndex));
        }

        public static TempItemQualityCounts GetTempItemsRawValue(this Inventory inventory, ItemQualityGroup itemGroup)
        {
            if (!inventory)
                throw new ArgumentNullException(nameof(inventory));

            if (!itemGroup)
                return default;

            float baseRawValue = inventory.GetTempItemRawValue(itemGroup.BaseItemIndex);
            float uncommonRawValue = inventory.GetTempItemRawValue(itemGroup.UncommonItemIndex);
            float rareRawValue = inventory.GetTempItemRawValue(itemGroup.RareItemIndex);
            float epicRawValue = inventory.GetTempItemRawValue(itemGroup.EpicItemIndex);
            float legendaryRawValue = inventory.GetTempItemRawValue(itemGroup.LegendaryItemIndex);

            return new TempItemQualityCounts(baseRawValue, uncommonRawValue, rareRawValue, epicRawValue, legendaryRawValue);
        }

        public static ItemQualityCounts GetItemCountsChanneled(this Inventory inventory, ItemQualityGroupIndex itemGroupIndex)
        {
            return inventory.GetItemCountsChanneled(QualityCatalog.GetItemQualityGroup(itemGroupIndex));
        }

        public static ItemQualityCounts GetItemCountsChanneled(this Inventory inventory, ItemQualityGroup itemGroup)
        {
            if (!inventory)
                throw new ArgumentNullException(nameof(inventory));

            if (!itemGroup)
                return default;

            int baseItemCount = inventory.GetItemCountChanneled(itemGroup.BaseItemIndex);
            int uncommonItemCount = inventory.GetItemCountChanneled(itemGroup.UncommonItemIndex);
            int rareItemCount = inventory.GetItemCountChanneled(itemGroup.RareItemIndex);
            int epicItemCount = inventory.GetItemCountChanneled(itemGroup.EpicItemIndex);
            int legendaryItemCount = inventory.GetItemCountChanneled(itemGroup.LegendaryItemIndex);

            return new ItemQualityCounts(baseItemCount, uncommonItemCount, rareItemCount, epicItemCount, legendaryItemCount);
        }

        public static bool HasAtLeastXTotalQualityItemsOfTierForPurchase(this Inventory inventory, ItemTier itemTier, int x)
        {
            if (!inventory)
                throw new ArgumentNullException(nameof(inventory));

            if (inventory.inventoryDisabled)
                return false;

            if (x <= 0)
                return true;

            int totalCount = 0;
            foreach (ItemIndex itemIndex in inventory.itemAcquisitionOrder)
            {
                ItemDef itemDef = ItemCatalog.GetItemDef(itemIndex);
                if (itemDef && itemDef.canRemove && !itemDef.ContainsTag(ItemTag.ObjectiveRelated) && itemDef.tier == itemTier && QualityCatalog.GetQualityTier(itemIndex) > QualityTier.None)
                {
                    totalCount += inventory.GetItemCountPermanent(itemIndex);
                    if (totalCount >= x)
                        return true;
                }
            }

            return false;
        }

        public static bool HasAtLeastXTotalNonQualityItemsOfTierForPurchase(this Inventory inventory, ItemTier itemTier, int x)
        {
            if (!inventory)
                throw new ArgumentNullException(nameof(inventory));

            if (inventory.inventoryDisabled)
                return false;

            if (x <= 0)
                return true;

            int totalCount = 0;
            foreach (ItemIndex itemIndex in inventory.itemAcquisitionOrder)
            {
                if (QualityCatalog.GetQualityTier(itemIndex) == QualityTier.None ||
                    QualityCatalog.FindItemQualityGroupIndex(itemIndex) == ItemQualitiesContent.ItemQualityGroups.RegeneratingScrap.GroupIndex)
                {
                    ItemDef itemDef = ItemCatalog.GetItemDef(itemIndex);
                    if (itemDef && itemDef.canRemove && !itemDef.ContainsTag(ItemTag.ObjectiveRelated) && itemDef.tier == itemTier)
                    {
                        totalCount += inventory.GetItemCountPermanent(itemIndex);
                        if (totalCount >= x)
                            return true;
                    }
                }
            }

            return false;
        }
    }
}
