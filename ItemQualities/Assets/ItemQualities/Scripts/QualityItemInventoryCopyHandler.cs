using ItemQualities.Utilities.Extensions;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using RoR2;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace ItemQualities
{
    internal static class QualityItemInventoryCopyHandler
    {
        [SystemInitializer]
        private static void Init()
        {
            On.RoR2.Inventory.AddItemsFrom_Int32Array_Func2 += Inventory_AddItemsFrom_Int32Array_Func2;
            IL.RoR2.Inventory.CloneInventory += Inventory_CloneInventory;
        }

        private static bool TryFindHighestQualityItemPassingFilter(ItemQualityGroupIndex itemGroupIndex, Func<ItemIndex, bool> filter, QualityTier startingTier, out ItemIndex highestTierItemIndex)
        {
            ItemQualityGroup itemGroup = QualityCatalog.GetItemQualityGroup(itemGroupIndex);

            for (QualityTier qualityTier = startingTier; qualityTier >= QualityTier.None; qualityTier--)
            {
                ItemIndex itemIndex = itemGroup.GetItemIndex(qualityTier);
                if (itemIndex != ItemIndex.None && filter(itemIndex))
                {
                    highestTierItemIndex = itemIndex;
                    return true;
                }
            }

            highestTierItemIndex = ItemIndex.None;
            return false;
        }

        private static bool TryFindRedirectItemIndex(ItemIndex itemIndex, Func<ItemIndex, bool> filter, out ItemIndex redirectItemIndex)
        {
            ItemQualityGroupIndex itemGroupIndex = QualityCatalog.FindItemQualityGroupIndex(itemIndex);
            if (itemGroupIndex == ItemQualityGroupIndex.Invalid)
            {
                redirectItemIndex = ItemIndex.None;
                return false;
            }

            QualityTier qualityTier = QualityCatalog.GetQualityTier(itemIndex);
            if (qualityTier == QualityTier.None)
            {
                // This item is not a quality item, it's copied count cannot be redirected
                redirectItemIndex = ItemIndex.None;
                return false;
            }

            if (filter(itemIndex))
            {
                // This item already passes the filter, no redirect needs to happen
                redirectItemIndex = ItemIndex.None;
                return false;
            }

            // Find the highest quality item (below this one) that passes the filter, redirect item counts to it.
            return TryFindHighestQualityItemPassingFilter(itemGroupIndex, filter, qualityTier - 1, out redirectItemIndex);
        }

        private static ItemCollection GetBonusItemsFromDisallowedQualities(in ItemCollection itemCollection, Func<ItemIndex, bool> filter)
        {
            ItemCollection bonusItemCollection = ItemCollection.Create();

            ReadOnlySpan<ItemIndex> nonZeroItemIndices = itemCollection.GetNonZeroIndicesSpan();

            foreach (ItemIndex itemIndex in nonZeroItemIndices)
            {
                if (TryFindRedirectItemIndex(itemIndex, filter, out ItemIndex redirectItemIndex))
                {
                    bonusItemCollection.Add(redirectItemIndex, itemCollection.GetStackValue(itemIndex));
                }
            }

            return bonusItemCollection;
        }

        private static Inventory.TempItemsStorage GetBonusItemsFromDisallowedQualities(in Inventory.TempItemsStorage itemCollection, Func<ItemIndex, bool> filter)
        {
            Inventory.TempItemsStorage bonusItemCollection = new Inventory.TempItemsStorage(itemCollection.inventory);

            ReadOnlySpan<ItemIndex> nonZeroItemIndices = itemCollection.GetNonZeroIndicesSpan();

            foreach (ItemIndex itemIndex in nonZeroItemIndices)
            {
                if (TryFindRedirectItemIndex(itemIndex, filter, out ItemIndex redirectItemIndex))
                {
                    bonusItemCollection.GiveItemTemp(redirectItemIndex, itemCollection.GetItemRawValue(itemIndex));
                }
            }

            return bonusItemCollection;
        }

        private static void Inventory_AddItemsFrom_Int32Array_Func2(On.RoR2.Inventory.orig_AddItemsFrom_Int32Array_Func2 orig, Inventory self, int[] otherItemStacks, Func<ItemIndex, bool> filter)
        {
            orig(self, otherItemStacks, filter);

            ItemCollection bonusItemCollection = GetBonusItemsFromDisallowedQualities(self.permanentItemStacks, filter);
            try
            {
                ReadOnlySpan<ItemIndex> nonZeroItemIndices = bonusItemCollection.GetNonZeroIndicesSpan();
                foreach (ItemIndex itemIndex in nonZeroItemIndices)
                {
                    self.ChangeItemStacksCount(new Inventory.GiveItemPermanentImpl
                    {
                        inventory = self,
                    }, itemIndex, bonusItemCollection.GetStackValue(itemIndex));
                }
            }
            finally
            {
                bonusItemCollection.Dispose();
            }
        }

        private static void Inventory_CloneInventory(ILContext il)
        {
            if (!il.Method.TryFindParameter<Inventory.CloneInventoryArgs>(out ParameterDefinition argsParameter))
            {
                Log.PatchError(il, "Failed to find args parameter");
                return;
            }

            static void addBonusItemsFromSpan(ReadOnlySpan<ItemIndex> itemIndices, List<ItemIndex> dest)
            {
                int startCount = dest.Count;

                foreach (ItemIndex itemIndex in itemIndices)
                {
                    if (dest.IndexOf(itemIndex, 0, startCount) == -1)
                    {
                        dest.Add(itemIndex);
                    }
                }
            }

            // regular ItemCollections
            {
                static void emitDisposeItemCollection(ILCursor c, VariableDefinition itemCollectionVar)
                {
                    c.Emit(OpCodes.Ldloc, itemCollectionVar);
                    c.EmitDelegate<Action<ItemCollection>>(disposeItemCollection);

                    [MethodImpl(MethodImplOptions.AggressiveInlining)]
                    static void disposeItemCollection(ItemCollection itemCollection)
                    {
                        itemCollection.Dispose();
                    }
                }

                static void emitAddItemsFromItemCollection(ILCursor c, VariableDefinition itemCollectionVar, VariableDefinition itemIndexVar)
                {
                    c.Emit(OpCodes.Ldloc, itemCollectionVar);
                    c.Emit(OpCodes.Ldloc, itemIndexVar);
                    c.EmitDelegate<Func<ItemCollection, ItemIndex, int>>(getItemCountFromCollection);
                    c.Emit(OpCodes.Add);

                    [MethodImpl(MethodImplOptions.AggressiveInlining)]
                    static int getItemCountFromCollection(ItemCollection itemCollection, ItemIndex itemIndex)
                    {
                        return itemCollection.GetStackValue(itemIndex);
                    }
                }

                static void emitAddBonusItemsToItemList(ILCursor c, VariableDefinition bonusItemCollectionVar, VariableDefinition itemIndicesListVar)
                {
                    c.Emit(OpCodes.Ldloc, bonusItemCollectionVar);
                    c.Emit(OpCodes.Ldloc, itemIndicesListVar);
                    c.EmitDelegate<Action<ItemCollection, List<ItemIndex>>>(addBonusItemsToItemList);

                    [MethodImpl(MethodImplOptions.AggressiveInlining)]
                    static void addBonusItemsToItemList(ItemCollection bonusItemCollection, List<ItemIndex> dest)
                    {
                        addBonusItemsFromSpan(bonusItemCollection.GetNonZeroIndicesSpan(), dest);
                    }
                }

                // permanent
                {
                    ILCursor c = new ILCursor(il);

                    /*
                     *  // if (args.clonePermanentItems)
                     *  IL_0050: ldarg.1
                     *  IL_0051: ldfld     bool RoR2.Inventory/CloneInventoryArgs::clonePermanentItems
                     *  IL_0056: brfalse.s IL_00C6
                     */

                    ILLabel afterClonePermanentItemsLabel = null;
                    if (c.TryFindNext(out ILCursor[] foundCursors,
                                      x => x.MatchLdfld<Inventory.CloneInventoryArgs>(nameof(Inventory.CloneInventoryArgs.clonePermanentItems)),
                                      x => x.MatchBrfalse(out afterClonePermanentItemsLabel)))
                    {
                        c.Goto(foundCursors[1].Next, MoveType.After);

                        /*
                         *  // src.permanentItemStacks.GetNonZeroIndices(list2);
                         *  IL_0058: ldloc.0
                         *  IL_0059: ldflda    valuetype RoR2.ItemCollection RoR2.Inventory::permanentItemStacks
                         *  IL_005E: ldloc.s   V_5
                         *  IL_0060: call      instance void RoR2.ItemCollection::GetNonZeroIndices(class [netstandard]System.Collections.Generic.List`1<valuetype RoR2.ItemIndex>)
                         */

                        VariableDefinition itemIndicesToCopyListVar = null;
                        if (c.TryGotoNext(MoveType.After,
                                          x => x.MatchLdflda<Inventory>(nameof(Inventory.permanentItemStacks)),
                                          x => x.MatchLdloc<List<ItemIndex>>(il, out itemIndicesToCopyListVar),
                                          x => x.MatchCallOrCallvirt<ItemCollection>(nameof(ItemCollection.GetNonZeroIndices)))
                            && c.IsBefore(afterClonePermanentItemsLabel.Target))
                        {
                            VariableDefinition bonusPermanentItemCollectionVar = il.AddVariable<ItemCollection>();

                            c.Emit(OpCodes.Ldarg_0);
                            c.Emit(OpCodes.Ldarg, argsParameter);
                            c.EmitDelegate<Func<Inventory, Inventory.CloneInventoryArgs, ItemCollection>>(getPermanentBonusItemsFromDisallowedQualities);
                            c.Emit(OpCodes.Stloc, bonusPermanentItemCollectionVar);

                            [MethodImpl(MethodImplOptions.AggressiveInlining)]
                            static ItemCollection getPermanentBonusItemsFromDisallowedQualities(Inventory inventory, Inventory.CloneInventoryArgs args)
                            {
                                return GetBonusItemsFromDisallowedQualities(inventory.permanentItemStacks, args.canCloneFilter);
                            }

                            emitAddBonusItemsToItemList(c, bonusPermanentItemCollectionVar, itemIndicesToCopyListVar);

                            /*
                             *  // this.ChangeItemStacksCount<Inventory.GiveItemPermanentImpl>(new Inventory.GiveItemPermanentImpl
                             *  // {
                             *  //     inventory = this
                             *  // }, itemIndex, src.permanentItemStacks.GetStackValue(itemIndex));
                             *  
                             *  IL_008E: ldarg.0
                             *  IL_008F: ldloca.s  V_10
                             *  IL_0091: initobj   RoR2.Inventory/GiveItemPermanentImpl
                             *  IL_0097: ldloca.s  V_10
                             *  IL_0099: ldarg.0
                             *  IL_009A: stfld     class RoR2.Inventory RoR2.Inventory/GiveItemPermanentImpl::inventory
                             *  IL_009F: ldloc.s   V_10
                             *  IL_00A1: ldloc.s   V_9
                             *  IL_00A3: ldloc.0
                             *  IL_00A4: ldflda    valuetype RoR2.ItemCollection RoR2.Inventory::permanentItemStacks
                             *  IL_00A9: ldloc.s   V_9
                             *  IL_00AB: call      instance int32 RoR2.ItemCollection::GetStackValue(valuetype RoR2.ItemIndex)
                             *  IL_00B0: call      instance void RoR2.Inventory::ChangeItemStacksCount<valuetype RoR2.Inventory/GiveItemPermanentImpl>(!!0, valuetype RoR2.ItemIndex, int32)
                             */

                            VariableDefinition itemIndexVar = null;
                            if (c.TryGotoNext(MoveType.After,
                                              x => x.MatchLdflda<Inventory>(nameof(Inventory.permanentItemStacks)),
                                              x => x.MatchLdloc<ItemIndex>(il, out itemIndexVar),
                                              x => x.MatchCallOrCallvirt<ItemCollection>(nameof(ItemCollection.GetStackValue)))
                                && c.IsBefore(afterClonePermanentItemsLabel.Target))
                            {
                                emitAddItemsFromItemCollection(c, bonusPermanentItemCollectionVar, itemIndexVar);
                            }
                            else
                            {
                                Log.PatchError(il, "Failed to find permanent item change stack count location");
                            }

                            c.Goto(afterClonePermanentItemsLabel.Target, MoveType.Before);

                            emitDisposeItemCollection(c, bonusPermanentItemCollectionVar);
                        }
                        else
                        {
                            Log.PatchError(il, "Failed to find permanent item GetNonZeroIndices location");
                        }
                    }
                    else
                    {
                        Log.PatchError(il, "Failed to find args.clonePermanentItems check location");
                    }
                }

                // rental
                {
                    ILCursor c = new ILCursor(il);

                    /*
                     *  // if (args.cloneRentalItems)
                     *  IL_00CD: ldarg.1
                     *  IL_00CE: ldfld     bool RoR2.Inventory/CloneInventoryArgs::cloneRentalItems
                     *  IL_00D3: brfalse.s IL_0143
                     */

                    ILLabel afterCloneRentalItemsLabel = null;
                    if (c.TryFindNext(out ILCursor[] foundCursors,
                                      x => x.MatchLdfld<Inventory.CloneInventoryArgs>(nameof(Inventory.CloneInventoryArgs.cloneRentalItems)),
                                      x => x.MatchBrfalse(out afterCloneRentalItemsLabel)))
                    {
                        c.Goto(foundCursors[1].Next, MoveType.After);

                        /*
                         *  // src.rentalItemStacks.GetNonZeroIndices(list2);
                         *  IL_00D5: ldloc.0
                         *  IL_00D6: ldflda    valuetype RoR2.ItemCollection RoR2.Inventory::rentalItemStacks
                         *  IL_00DB: ldloc.s   V_5
                         *  IL_00DD: call      instance void RoR2.ItemCollection::GetNonZeroIndices(class [netstandard]System.Collections.Generic.List`1<valuetype RoR2.ItemIndex>)
                         */

                        VariableDefinition itemIndicesToCopyListVar = null;
                        if (c.TryGotoNext(MoveType.After,
                                          x => x.MatchLdflda<Inventory>(nameof(Inventory.rentalItemStacks)),
                                          x => x.MatchLdloc<List<ItemIndex>>(il, out itemIndicesToCopyListVar),
                                          x => x.MatchCallOrCallvirt<ItemCollection>(nameof(ItemCollection.GetNonZeroIndices)))
                            && c.IsBefore(afterCloneRentalItemsLabel.Target))
                        {
                            VariableDefinition bonusRentalItemCollectionVar = il.AddVariable<ItemCollection>();

                            c.Emit(OpCodes.Ldarg_0);
                            c.Emit(OpCodes.Ldarg, argsParameter);
                            c.EmitDelegate<Func<Inventory, Inventory.CloneInventoryArgs, ItemCollection>>(getRentalBonusItemsFromDisallowedQualities);
                            c.Emit(OpCodes.Stloc, bonusRentalItemCollectionVar);

                            [MethodImpl(MethodImplOptions.AggressiveInlining)]
                            static ItemCollection getRentalBonusItemsFromDisallowedQualities(Inventory inventory, Inventory.CloneInventoryArgs args)
                            {
                                return GetBonusItemsFromDisallowedQualities(inventory.rentalItemStacks, args.canCloneFilter);
                            }

                            emitAddBonusItemsToItemList(c, bonusRentalItemCollectionVar, itemIndicesToCopyListVar);

                            /*
                             *  // this.ChangeItemStacksCount<Inventory.GiveItemRentedImpl>(new Inventory.GiveItemRentedImpl
                             *  // {
                             *  //     inventory = this
                             *  // }, itemIndex2, src.rentalItemStacks.GetStackValue(itemIndex2));
                             *  IL_010B: ldarg.0
                             *  IL_010C: ldloca.s  V_13
                             *  IL_010E: initobj   RoR2.Inventory/GiveItemRentedImpl
                             *  IL_0114: ldloca.s  V_13
                             *  IL_0116: ldarg.0
                             *  IL_0117: stfld     class RoR2.Inventory RoR2.Inventory/GiveItemRentedImpl::inventory
                             *  IL_011C: ldloc.s   V_13
                             *  IL_011E: ldloc.s   V_12
                             *  IL_0120: ldloc.0
                             *  IL_0121: ldflda    valuetype RoR2.ItemCollection RoR2.Inventory::rentalItemStacks
                             *  IL_0126: ldloc.s   V_12
                             *  IL_0128: call      instance int32 RoR2.ItemCollection::GetStackValue(valuetype RoR2.ItemIndex)
                             *  IL_012D: call      instance void RoR2.Inventory::ChangeItemStacksCount<valuetype RoR2.Inventory/GiveItemRentedImpl>(!!0, valuetype RoR2.ItemIndex, int32)
                             */

                            VariableDefinition itemIndexVar = null;
                            if (c.TryGotoNext(MoveType.After,
                                              x => x.MatchLdflda<Inventory>(nameof(Inventory.rentalItemStacks)),
                                              x => x.MatchLdloc<ItemIndex>(il, out itemIndexVar),
                                              x => x.MatchCallOrCallvirt<ItemCollection>(nameof(ItemCollection.GetStackValue)))
                                && c.IsBefore(afterCloneRentalItemsLabel.Target))
                            {
                                emitAddItemsFromItemCollection(c, bonusRentalItemCollectionVar, itemIndexVar);
                            }
                            else
                            {
                                Log.PatchError(il, "Failed to find rental ChangeItemStacksCount location");
                            }

                            c.Goto(afterCloneRentalItemsLabel.Target, MoveType.Before);

                            emitDisposeItemCollection(c, bonusRentalItemCollectionVar);
                        }
                        else
                        {
                            Log.PatchError(il, "Failed to find rental GetNonZeroIndices location");
                        }
                    }
                    else
                    {
                        Log.PatchError(il, "Failed to find args.cloneRentalItems check location");
                    }
                }
            }

            // temp items
            {
                static void emitDisposeTempItemsStorage(ILCursor c, VariableDefinition tempItemsStorageVar)
                {
                    c.Emit(OpCodes.Ldloc, tempItemsStorageVar);
                    c.EmitDelegate<Action<Inventory.TempItemsStorage>>(disposeTempItemsStorage);

                    [MethodImpl(MethodImplOptions.AggressiveInlining)]
                    static void disposeTempItemsStorage(Inventory.TempItemsStorage itemCollection)
                    {
                        itemCollection.Dispose();
                    }
                }

                static void emitAddItemsFromTempItemsStorage(ILCursor c, VariableDefinition tempItemsStorageVar, VariableDefinition itemIndexVar)
                {
                    c.Emit(OpCodes.Ldloc, tempItemsStorageVar);
                    c.Emit(OpCodes.Ldloc, itemIndexVar);
                    c.EmitDelegate<Func<Inventory.TempItemsStorage, ItemIndex, float>>(getItemRawValueFromStorage);
                    c.Emit(OpCodes.Add);

                    [MethodImpl(MethodImplOptions.AggressiveInlining)]
                    static float getItemRawValueFromStorage(Inventory.TempItemsStorage tempItemsStorage, ItemIndex itemIndex)
                    {
                        return tempItemsStorage.GetItemRawValue(itemIndex);
                    }
                }

                static void emitAddBonusItemsToItemList(ILCursor c, VariableDefinition bonusTempItemsStorageVar, VariableDefinition itemIndicesListVar)
                {
                    c.Emit(OpCodes.Ldloc, bonusTempItemsStorageVar);
                    c.Emit(OpCodes.Ldloc, itemIndicesListVar);
                    c.EmitDelegate<Action<Inventory.TempItemsStorage, List<ItemIndex>>>(addBonusItemsToItemList);

                    [MethodImpl(MethodImplOptions.AggressiveInlining)]
                    static void addBonusItemsToItemList(Inventory.TempItemsStorage bonusTempItemsStorage, List<ItemIndex> dest)
                    {
                        addBonusItemsFromSpan(bonusTempItemsStorage.GetNonZeroIndicesSpan(), dest);
                    }
                }

                ILCursor c = new ILCursor(il);

                /*
                 *  // if (args.cloneTemporaryItems)
                 *  IL_014A: ldarg.1
                 *  IL_014B: ldfld     bool RoR2.Inventory/CloneInventoryArgs::cloneTemporaryItems
                 *  IL_0150: brfalse.s IL_01AE
                 */

                ILLabel afterCloneTemporaryItemsLabel = null;
                if (c.TryFindNext(out ILCursor[] foundCursors,
                                  x => x.MatchLdfld<Inventory.CloneInventoryArgs>(nameof(Inventory.CloneInventoryArgs.cloneTemporaryItems)),
                                  x => x.MatchBrfalse(out afterCloneTemporaryItemsLabel)))
                {
                    c.Goto(foundCursors[1].Next, MoveType.After);

                    /*
                     *  // src.tempItemsStorage.GetNonZeroIndices(list2);
                     *  IL_0152: ldloc.0
                     *  IL_0153: ldflda    valuetype RoR2.Inventory/TempItemsStorage RoR2.Inventory::tempItemsStorage
                     *  IL_0158: ldloc.s   V_5
                     *  IL_015A: call      instance void RoR2.Inventory/TempItemsStorage::GetNonZeroIndices(class [netstandard]System.Collections.Generic.List`1<valuetype RoR2.ItemIndex>)
                     */

                    VariableDefinition itemIndicesToCopyListVar = null;
                    if (c.TryGotoNext(MoveType.After,
                                      x => x.MatchLdflda<Inventory>(nameof(Inventory.tempItemsStorage)),
                                      x => x.MatchLdloc<List<ItemIndex>>(il, out itemIndicesToCopyListVar),
                                      x => x.MatchCallOrCallvirt<Inventory.TempItemsStorage>(nameof(Inventory.TempItemsStorage.GetNonZeroIndices)))
                        && c.IsBefore(afterCloneTemporaryItemsLabel.Target))
                    {
                        VariableDefinition bonusTempItemStorageVar = il.AddVariable<Inventory.TempItemsStorage>();

                        c.Emit(OpCodes.Ldarg_0);
                        c.Emit(OpCodes.Ldarg, argsParameter);
                        c.EmitDelegate<Func<Inventory, Inventory.CloneInventoryArgs, Inventory.TempItemsStorage>>(getTempBonusItemsFromDisallowedQualities);
                        c.Emit(OpCodes.Stloc, bonusTempItemStorageVar);

                        [MethodImpl(MethodImplOptions.AggressiveInlining)]
                        static Inventory.TempItemsStorage getTempBonusItemsFromDisallowedQualities(Inventory inventory, Inventory.CloneInventoryArgs args)
                        {
                            return GetBonusItemsFromDisallowedQualities(inventory.tempItemsStorage, args.canCloneFilter);
                        }

                        emitAddBonusItemsToItemList(c, bonusTempItemStorageVar, itemIndicesToCopyListVar);

                        /*
                         *  // this.GiveItemTemp(itemIndex3, src.tempItemsStorage.GetItemRawValue(itemIndex3));
                         *  IL_0188: ldarg.0
                         *  IL_0189: ldloc.s   V_15
                         *  IL_018B: ldloc.0
                         *  IL_018C: ldflda    valuetype RoR2.Inventory/TempItemsStorage RoR2.Inventory::tempItemsStorage
                         *  IL_0191: ldloc.s   V_15
                         *  IL_0193: call      instance float32 RoR2.Inventory/TempItemsStorage::GetItemRawValue(valuetype RoR2.ItemIndex)
                         *  IL_0198: call      instance void RoR2.Inventory::GiveItemTemp(valuetype RoR2.ItemIndex, float32)
                         */

                        VariableDefinition itemIndexVar = null;
                        if (c.TryGotoNext(MoveType.After,
                                          x => x.MatchLdflda<Inventory>(nameof(Inventory.tempItemsStorage)),
                                          x => x.MatchLdloc<ItemIndex>(il, out itemIndexVar),
                                          x => x.MatchCallOrCallvirt<Inventory.TempItemsStorage>(nameof(Inventory.TempItemsStorage.GetItemRawValue)))
                            && c.IsBefore(afterCloneTemporaryItemsLabel.Target))
                        {
                            emitAddItemsFromTempItemsStorage(c, bonusTempItemStorageVar, itemIndexVar);
                        }
                        else
                        {
                            Log.PatchError(il, "Failed to find temp item change stack count location");
                        }

                        c.Goto(afterCloneTemporaryItemsLabel.Target, MoveType.Before);

                        emitDisposeTempItemsStorage(c, bonusTempItemStorageVar);
                    }
                    else
                    {
                        Log.PatchError(il, "Failed to find temp item GetNonZeroIndices location");
                    }
                }
                else
                {
                    Log.PatchError(il, "Failed to find args.cloneTemporaryItems check location");
                }
            }
        }
    }
}
