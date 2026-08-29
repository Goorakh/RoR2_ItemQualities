using HG;
using ItemQualities.Utilities.Extensions;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using RoR2;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace ItemQualities
{
    internal static class QualityItemOrderPatch
    {
        private static readonly Comparer<ItemIndex> _itemQualityComparer = Comparer<ItemIndex>.Create((a, b) =>
        {
            QualityTier qualityTierA = QualityCatalog.GetQualityTier(a);
            QualityTier qualityTierB = QualityCatalog.GetQualityTier(b);

            return qualityTierA - qualityTierB;
        });

        [SystemInitializer]
        private static void Init()
        {
            IL.RoR2.Inventory.SetItemAcquiredServer += Inventory_SetItemAcquiredServer;
        }

        private static bool sortQualityItem(Inventory inventory, ItemQualityGroupIndex itemGroupIndex)
        {
            using var _1 = ListPool<ItemIndex>.RentCollection(out List<ItemIndex> tempItemAcquisitionOrder);
            ListUtils.CloneTo(inventory.itemAcquisitionOrder, tempItemAcquisitionOrder);

            bool itemOrderChanged = false;

            try
            {
                using var _2 = ListPool<ItemIndex>.RentCollection(out List<ItemIndex> sortedItemsInGroup);
                ListUtils.EnsureCapacity(sortedItemsInGroup, (int)QualityTier.Count + 1);

                extractAndSortAllItemsInGroup(itemGroupIndex, 0, tempItemAcquisitionOrder, sortedItemsInGroup, out int groupStartIndex);
                if (sortedItemsInGroup.Count > 0)
                {
                    tempItemAcquisitionOrder.InsertRange(groupStartIndex, sortedItemsInGroup);

                    itemOrderChanged = true;
                }
            }
            catch (Exception e)
            {
                Log.Error_NoCallerPrefix(e.ToString());
                return false;
            }

            if (itemOrderChanged)
            {
                ListUtils.CloneTo(tempItemAcquisitionOrder, inventory.itemAcquisitionOrder);
            }

            return itemOrderChanged;
        }

        private static void extractAndSortAllItemsInGroup(ItemQualityGroupIndex itemGroupIndex, int startSearchIndex, List<ItemIndex> itemsList, List<ItemIndex> extractedGroup, out int firstFoundIndex)
        {
            void recordItemInGroup(ItemIndex itemIndex)
            {
                int indexInGroup = extractedGroup.BinarySearch(itemIndex, _itemQualityComparer);
                if (indexInGroup < 0)
                    indexInGroup = ~indexInGroup;

                extractedGroup.Insert(indexInGroup, itemIndex);
            }

            firstFoundIndex = -1;

            for (int i = startSearchIndex; i < itemsList.Count; i++)
            {
                ItemIndex itemIndex = itemsList[i];
                if (QualityCatalog.FindItemQualityGroupIndex(itemIndex) != itemGroupIndex)
                    continue;

                if (firstFoundIndex == -1)
                {
                    firstFoundIndex = i;
                }

                recordItemInGroup(itemIndex);
                itemsList.RemoveAt(i);
                i--;

                if (extractedGroup.Count >= (int)QualityTier.Count + 1)
                {
                    break;
                }
            }
        }

        private static void Inventory_SetItemAcquiredServer(ILContext il)
        {
            MethodInfo itemIndexListAddMethod = typeof(List<ItemIndex>).GetMethod(nameof(List<ItemIndex>.Add));
            if (itemIndexListAddMethod == null)
            {
                Log.Error("Failed to find List<ItemIndex> index getter method");
                return;
            }

            ILCursor c = new ILCursor(il);

            int itemIndexParameterIndex = -1;
            if (!c.TryGotoNext(MoveType.After,
                               x => x.MatchLdarg(0),
                               x => x.MatchLdfld<Inventory>(nameof(Inventory.itemAcquisitionOrder)),
                               x => x.MatchLdarg(out itemIndexParameterIndex),
                               x => x.MatchCallOrCallvirt(itemIndexListAddMethod)))
            {
                Log.Error("Failed to find patch location");
                return;
            }

            c.Emit(OpCodes.Ldarg_0);
            c.Emit(OpCodes.Ldarg, itemIndexParameterIndex);
            c.EmitDelegate<Action<Inventory, ItemIndex>>(tryCustomInsertIndex);

            static void tryCustomInsertIndex(Inventory inventory, ItemIndex itemIndex)
            {
                ItemQualityGroupIndex itemGroupIndex = QualityCatalog.FindItemQualityGroupIndex(itemIndex);
                if (itemGroupIndex != ItemQualityGroupIndex.Invalid)
                {
                    sortQualityItem(inventory, itemGroupIndex);
                }
            }
        }
    }
}
