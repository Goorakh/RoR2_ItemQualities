using ItemQualities.Utilities.Extensions;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using RoR2;
using System;
using System.Collections.Generic;

namespace ItemQualities
{
    static class QualityItemInventoryCopyHandler
    {
        [SystemInitializer]
        static void Init()
        {
            IL.RoR2.Inventory.AddItemsFrom_Int32Array_Func2 += Inventory_AddItemsFrom_Int32Array_Func2;
            IL.RoR2.Inventory.AddItemsFrom_Inventory_Func2 += Inventory_AddItemsFrom_Inventory_Func2;
        }

        static int calculateBonusItemCountFromQualities(ItemIndex itemIndex, in ItemQualityCounts itemCounts, Func<ItemIndex, bool> filter)
        {
            QualityTier itemQualityTier = QualityCatalog.GetQualityTier(itemIndex);

            // Find any higher qualities that don't pass the filter, and add their total count to this one

            int bonusItemCount = 0;
            for (QualityTier qualityTier = itemQualityTier + 1; qualityTier < QualityTier.Count; qualityTier++)
            {
                ItemIndex qualityItemIndex = QualityCatalog.GetItemIndexOfQuality(itemIndex, qualityTier);
                if (qualityItemIndex == itemIndex)
                    continue;

                // If a higher quality item passes the filter, thats what will handle the bonus count, so don't do anything for this one
                if (filter(qualityItemIndex))
                {
                    return 0;
                }
                else
                {
                    bonusItemCount += itemCounts[qualityTier];
                }
            }

            return bonusItemCount;
        }

        static void Inventory_AddItemsFrom_Int32Array_Func2(ILContext il)
        {
            if (!il.Method.TryFindParameter<Func<ItemIndex, bool>>(out ParameterDefinition filterParameter))
            {
                Log.Error("Failed to find filter parameter");
                return;
            }

            ILCursor c = new ILCursor(il);

            /*
             *  // int num = otherItemStacks[(int)itemIndex];
             *  IL_002D: ldarg.1
             *  IL_002E: ldloc.2
             *  IL_002F: ldelem.i4
             *  IL_0030: stloc.3
             */

            ParameterDefinition otherItemStacksParameter = null;
            VariableDefinition itemIndexVar = null;
            if (!c.TryGotoNext(MoveType.After,
                               x => x.MatchLdarg<int[]>(il, out otherItemStacksParameter),
                               x => x.MatchLdloc<ItemIndex>(il, out itemIndexVar),
                               x => x.MatchLdelemI4()))
            {
                Log.Error("Failed to find patch location");
                return;
            }

            c.Emit(OpCodes.Ldarg, otherItemStacksParameter);
            c.Emit(OpCodes.Ldarg, filterParameter);
            c.Emit(OpCodes.Ldloc, itemIndexVar);
            c.EmitDelegate<Func<int[], Func<ItemIndex, bool>, ItemIndex, int>>(getBonusItemCount);
            c.Emit(OpCodes.Add);

            static int getBonusItemCount(int[] otherItemStacks, Func<ItemIndex, bool> filter, ItemIndex itemIndex)
            {
                ItemQualityGroupIndex itemGroupIndex = QualityCatalog.FindItemQualityGroupIndex(itemIndex);
                if (itemGroupIndex == ItemQualityGroupIndex.Invalid)
                    return 0;

                ItemQualityGroup itemGroup = QualityCatalog.GetItemQualityGroup(itemGroupIndex);

                ItemQualityCounts itemCounts = new ItemQualityCounts();
                for (QualityTier qualityTier = QualityTier.None; qualityTier < QualityTier.Count; qualityTier++)
                {
                    ItemIndex qualityItemIndex = itemGroup.GetItemIndex(qualityTier);
                    if (qualityItemIndex != ItemIndex.None)
                    {
                        itemCounts[qualityTier] = otherItemStacks[(int)qualityItemIndex];
                    }
                }

                int bonusCount = calculateBonusItemCountFromQualities(itemIndex, itemCounts, filter);

                if (bonusCount > 0)
                {
                    Log.Debug($"Adding {bonusCount} bonus item(s) from filtered quality item(s): {ItemCatalog.GetItemDef(itemIndex).name}");
                }

                return bonusCount;
            }
        }

        static void Inventory_AddItemsFrom_Inventory_Func2(ILContext il)
        {
            if (!il.Method.TryFindParameter<Inventory>("other", out ParameterDefinition otherInventoryParameter))
            {
                Log.Error("Failed to find other inventory parameter");
                return;
            }

            if (!il.Method.TryFindParameter<Func<ItemIndex, bool>>(out ParameterDefinition filterParameter))
            {
                Log.Error("Failed to find filter parameter");
                return;
            }

            ILCursor c = new ILCursor(il);

            /*
             *  // other.permanentItemStacks.GetNonZeroIndices(list);
             *  IL_0025: ldarg.1
             *  IL_0026: ldflda    valuetype RoR2.ItemCollection RoR2.Inventory::permanentItemStacks
             *  IL_002B: ldloc.2
             *  IL_002C: call      instance void RoR2.ItemCollection::GetNonZeroIndices(class [netstandard]System.Collections.Generic.List`1<valuetype RoR2.ItemIndex>)
             */

            VariableDefinition nonZeroIndicesVar = null;
            if (c.TryGotoNext(MoveType.After,
                              x => x.MatchLdarg(otherInventoryParameter),
                              x => x.MatchLdflda<Inventory>(nameof(Inventory.permanentItemStacks)),
                              x => x.MatchLdloc<List<ItemIndex>>(il, out nonZeroIndicesVar),
                              x => x.MatchCallOrCallvirt<ItemCollection>(nameof(ItemCollection.GetNonZeroIndices))))
            {
                c.Emit(OpCodes.Ldarg, otherInventoryParameter);
                c.Emit(OpCodes.Ldarg, filterParameter);
                c.Emit(OpCodes.Ldloc, nonZeroIndicesVar);
                c.EmitDelegate<Action<Inventory, Func<ItemIndex, bool>, List<ItemIndex>>>(fixNonZeroIndices);

                static void fixNonZeroIndices(Inventory otherInventory, Func<ItemIndex, bool> filter, List<ItemIndex> nonZeroItemIndices)
                {
                    // The base method only iterates through nonzero indices, so if the inventory has only quality of an item and quality does not pass the filter, none of the base item will be given.
                    // Not sure if this is a good solution really, but just pretend an item is nonzero if we know it'll have a bonus count from quality

                    for (int i = nonZeroItemIndices.Count - 1; i >= 0; i--)
                    {
                        ItemIndex itemIndex = nonZeroItemIndices[i];

                        ItemQualityGroupIndex itemGroupIndex = QualityCatalog.FindItemQualityGroupIndex(itemIndex);
                        if (itemGroupIndex == ItemQualityGroupIndex.Invalid)
                            continue;

                        if (filter(itemIndex))
                            continue;

                        ItemQualityGroup itemGroup = QualityCatalog.GetItemQualityGroup(itemGroupIndex);

                        QualityTier itemQualityTier = QualityCatalog.GetQualityTier(itemIndex);
                        if (itemQualityTier == QualityTier.None)
                            continue;

                        QualityTier highestAcceptedQualityTier = itemQualityTier - 1;
                        ItemIndex highestAcceptedQualityItemIndex = itemGroup.GetItemIndex(highestAcceptedQualityTier);

                        while (highestAcceptedQualityTier >= QualityTier.None &&
                               // We don't actually care if an item of X quality is not defined, we just want to find any valid item
                               (highestAcceptedQualityItemIndex == ItemIndex.None || !filter(highestAcceptedQualityItemIndex)))
                        {
                            if (highestAcceptedQualityTier == QualityTier.None)
                            {
                                highestAcceptedQualityItemIndex = ItemIndex.None;
                                break;
                            }

                            highestAcceptedQualityTier--;
                            highestAcceptedQualityItemIndex = itemGroup.GetItemIndex(highestAcceptedQualityTier);
                        }

                        if (highestAcceptedQualityItemIndex != ItemIndex.None)
                        {
                            if (!nonZeroItemIndices.Contains(highestAcceptedQualityItemIndex))
                            {
                                nonZeroItemIndices.Add(highestAcceptedQualityItemIndex);
                                Log.Debug($"Added item {ItemCatalog.GetItemDef(highestAcceptedQualityItemIndex).name} to nonzero indices to receive filtered quality item(s)");
                            }
                        }
                    }
                }
            }
            else
            {
                Log.Warning("Failed to find NonZeroIndices patch location");
            }

            /*
             *  // other.permanentItemStacks.GetStackValue(itemIndex)
             *  IL_0065: ldarg.1
             *  IL_0066: ldflda    valuetype RoR2.ItemCollection RoR2.Inventory::permanentItemStacks
             *  IL_006B: ldloc.s   V_4
             *  IL_006D: call      instance int32 RoR2.ItemCollection::GetStackValue(valuetype RoR2.ItemIndex)
             */

            VariableDefinition itemIndexVar = null;
            if (!c.TryGotoNext(MoveType.After,
                               x => x.MatchLdarg(otherInventoryParameter),
                               x => x.MatchLdflda<Inventory>(nameof(Inventory.permanentItemStacks)),
                               x => x.MatchLdloc<ItemIndex>(il, out itemIndexVar),
                               x => x.MatchCallOrCallvirt<ItemCollection>(nameof(ItemCollection.GetStackValue))))
            {
                Log.Error("Failed to find stack value patch location");
                return;
            }

            c.Emit(OpCodes.Ldarg, otherInventoryParameter);
            c.Emit(OpCodes.Ldarg, filterParameter);
            c.Emit(OpCodes.Ldloc, itemIndexVar);
            c.EmitDelegate<Func<Inventory, Func<ItemIndex, bool>, ItemIndex, int>>(getBonusItemCount);
            c.Emit(OpCodes.Add);

            static int getBonusItemCount(Inventory otherInventory, Func<ItemIndex, bool> filter, ItemIndex itemIndex)
            {
                ItemQualityGroupIndex itemGroupIndex = QualityCatalog.FindItemQualityGroupIndex(itemIndex);
                if (itemGroupIndex == ItemQualityGroupIndex.Invalid)
                    return 0;

                ItemQualityCounts itemCounts = otherInventory.GetItemCountsPermanent(itemGroupIndex);

                int bonusCount = calculateBonusItemCountFromQualities(itemIndex, itemCounts, filter);

                if (bonusCount > 0)
                {
                    Log.Debug($"Adding {bonusCount} bonus item(s) from filtered quality item(s): {ItemCatalog.GetItemDef(itemIndex).name}");
                }

                return bonusCount;
            }
        }
    }
}
