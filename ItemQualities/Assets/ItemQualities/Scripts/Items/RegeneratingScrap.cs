using ItemQualities.Utilities.Extensions;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;
using RoR2;
using System;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace ItemQualities.Items
{
    internal static class RegeneratingScrap
    {
        [SystemInitializer(typeof(CostTypeCatalog))]
        private static void Init()
        {
            On.RoR2.CharacterMaster.TryRegenerateScrap += CharacterMaster_TryRegenerateScrap;

            CostTypeDef greenItemCostDef = CostTypeCatalog.GetCostTypeDef(CostTypeIndex.GreenItem);
            MethodInfo greenItemPayCostMethod = greenItemCostDef?.payCost?.Method;
            if (greenItemPayCostMethod != null)
            {
                new ILHook(greenItemPayCostMethod, ItemPayCostManipulator);
            }
            else
            {
                Log.Error($"Failed to find item PayCost method");
            }
        }

        private static void CharacterMaster_TryRegenerateScrap(On.RoR2.CharacterMaster.orig_TryRegenerateScrap orig, CharacterMaster self)
        {
            orig(self);

            if (self && self.inventory)
            {
                for (QualityTier qualityTier = 0; qualityTier < QualityTier.Count; qualityTier++)
                {
                    ItemIndex qualityScrapIndex = ItemQualitiesContent.ItemQualityGroups.RegeneratingScrap.GetItemIndex(qualityTier);
                    ItemIndex qualityConsumedScrapIndex = ItemQualitiesContent.ItemQualityGroups.RegeneratingScrapConsumed.GetItemIndex(qualityTier);

                    if (qualityScrapIndex != ItemIndex.None && qualityConsumedScrapIndex != ItemIndex.None)
                    {
                        Inventory.ItemTransformation qualityRegenScrapTransformation = new Inventory.ItemTransformation
                        {
                            originalItemIndex = qualityConsumedScrapIndex,
                            newItemIndex = qualityScrapIndex,
                            maxToTransform = int.MaxValue,
                            transformationType = (ItemTransformationTypeIndex)CharacterMasterNotificationQueue.TransformationType.RegeneratingScrapRegen
                        };

                        qualityRegenScrapTransformation.TryTransform(self.inventory, out _);
                    }
                }
            }
        }

        private static void ItemPayCostManipulator(ILContext il)
        {
            if (!il.Method.TryFindParameter<CostTypeDef.PayCostContext>(out ParameterDefinition contextParameter))
            {
                Log.Error("Failed to find context parameter");
                return;
            }

            ILCursor c = new ILCursor(il);

            // ItemTransformation quality handling
            {
                c.Goto(0);

                /*
                 *  // if (itemTransformation.originalItemIndex == DLC1Content.Items.RegeneratingScrap.itemIndex)
                 *  IL_01B1: ldloca.s  V_15
                 *  IL_01B3: call      instance valuetype RoR2.ItemIndex RoR2.Inventory/ItemTransformation::get_originalItemIndex()
                 *  IL_01B8: ldsfld    class RoR2.ItemDef RoR2.DLC1Content/Items::RegeneratingScrap
                 *  IL_01BD: callvirt  instance valuetype RoR2.ItemIndex RoR2.ItemDef::get_itemIndex()
                 *  IL_01C2: bne.un.s  IL_01D8
                 */

                VariableDefinition itemTransformationVar = null;
                Instruction regeneratingScrapTransformationStartInstr = null;
                if (c.TryGotoNext(MoveType.Before,
                                   x => x.MatchLdloca<Inventory.ItemTransformation>(il, out itemTransformationVar),
                                   x => x.MatchCallOrCallvirt<Inventory.ItemTransformation>("get_" + nameof(Inventory.ItemTransformation.originalItemIndex)),
                                   x => x.MatchLdsfld(typeof(DLC1Content.Items), nameof(DLC1Content.Items.RegeneratingScrap)),
                                   x => x.MatchCallOrCallvirt<ItemDef>("get_" + nameof(ItemDef.itemIndex)),
                                   x => x.MatchBneUn(out _),
                                   x => x.MatchAny(out regeneratingScrapTransformationStartInstr)))
                {
                    ILLabel regeneratingScrapTransformationLabel = c.DefineLabel();

                    c.Emit(OpCodes.Ldloca, itemTransformationVar);
                    c.EmitDelegate<IsRegeneratingScrapDelegate>(isRegeneratingScrap);
                    c.Emit(OpCodes.Brtrue, regeneratingScrapTransformationLabel);

                    c.Goto(regeneratingScrapTransformationStartInstr).MarkLabel(regeneratingScrapTransformationLabel);

                    static bool isRegeneratingScrap(in Inventory.ItemTransformation itemTransformation)
                    {
                        ItemQualityGroupIndex itemGroupIndex = QualityCatalog.FindItemQualityGroupIndex(itemTransformation.originalItemIndex);
                        return itemGroupIndex != ItemQualityGroupIndex.Invalid && itemGroupIndex == ItemQualitiesContent.ItemQualityGroups.RegeneratingScrap.GroupIndex;
                    }
                }
                else
                {
                    Log.Error("[ItemTransformation Quality Patch] Failed to find item transformation patch location");
                }
            }

            // Quality regnerating scrap priority
            {
                // The priority we want is:
                // 1) Use ONE of the highest quality regen scrap available
                // 2) Use all priority scrap
                // 3) Use all scrap
                // 4) Use all remaining quality regen scrap (lowest to highest quality)
                // 5) Random items

                c.Goto(0);

                VariableDefinition qualityRegeneratingScrapSelectionsVar = il.AddVariable<WeightedSelection<ItemIndex>[]>();
                VariableDefinition itemSelectionTempVar = il.AddVariable<WeightedSelection<ItemIndex>>();

                // Create weighted selections
                {
                    c.EmitDelegate<Func<WeightedSelection<ItemIndex>[]>>(createQualityRegeneratingScrapSelections);
                    c.Emit(OpCodes.Stloc, qualityRegeneratingScrapSelectionsVar);

                    [MethodImpl(MethodImplOptions.AggressiveInlining)]
                    static WeightedSelection<ItemIndex>[] createQualityRegeneratingScrapSelections()
                    {
                        return new WeightedSelection<ItemIndex>[(int)QualityTier.Count];
                    }
                }

                // Add all quality regen scrap to our selection instead
                {
                    /*
                     *  // (itemDef.ContainsTag(ItemTag.PriorityScrap) ? weightedSelection3 : (itemDef.ContainsTag(ItemTag.Scrap) ? weightedSelection2 : weightedSelection)).AddChoice(itemIndex, (float)itemCountPermanent);
                     *  IL_00CE: ldloc.s   V_11
                     *  IL_00D0: ldc.i4.s  14
                     *  IL_00D2: callvirt  instance bool RoR2.ItemDef::ContainsTag(valuetype RoR2.ItemTag)
                     *  IL_00D7: brtrue.s  IL_00EB
                     */
                    VariableDefinition itemDefVar = null;
                    ILLabel pickPriorityScrapSelectionLabel = null;
                    if (c.TryGotoNext(MoveType.AfterLabel,
                                      x => x.MatchLdloc<ItemDef>(il, out itemDefVar),
                                      x => x.MatchLdcI4((int)ItemTag.PriorityScrap),
                                      x => x.MatchCallOrCallvirt<ItemDef>(nameof(ItemDef.ContainsTag)),
                                      x => x.MatchBrtrue(out pickPriorityScrapSelectionLabel)))
                    {
                        // pickPriorityScrapSelectionLabel points to the instruction loading the priorityScrap selection,
                        // we want to jump to the one directly after with our own selection
                        ILLabel addItemToSelectionLabel = c.Clone().Goto(pickPriorityScrapSelectionLabel.Target, MoveType.After).MarkLabel();

                        ILLabel nonQualityRegenScrapLabel = c.DefineLabel();

                        VariableDefinition targetQualityScrapSelectionVar = itemSelectionTempVar;

                        // if (isQualityRegeneratingScrap) { use targetQualityScrapSelection } else { default behavior }
                        c.Emit(OpCodes.Ldloc, itemDefVar);
                        c.Emit(OpCodes.Ldloc, qualityRegeneratingScrapSelectionsVar);
                        c.Emit(OpCodes.Ldloca, targetQualityScrapSelectionVar);
                        c.EmitDelegate<IsQualityRegeneratingScrapDelegate>(isQualityRegeneratingScrap);
                        c.Emit(OpCodes.Brfalse, nonQualityRegenScrapLabel);

                        c.Emit(OpCodes.Ldloc, targetQualityScrapSelectionVar);
                        c.Emit(OpCodes.Br, addItemToSelectionLabel);

                        c.MarkLabel(nonQualityRegenScrapLabel);

                        static bool isQualityRegeneratingScrap(ItemDef itemDef, WeightedSelection<ItemIndex>[] qualityScrapSelections, out WeightedSelection<ItemIndex> targetQualityScrapSelection)
                        {
                            if (itemDef)
                            {
                                QualityTier qualityTier = QualityCatalog.GetQualityTier(itemDef.itemIndex);
                                ItemQualityGroupIndex itemGroupIndex = QualityCatalog.FindItemQualityGroupIndex(itemDef.itemIndex);

                                if (itemGroupIndex == ItemQualitiesContent.ItemQualityGroups.RegeneratingScrap.GroupIndex && qualityTier != QualityTier.None)
                                {
                                    targetQualityScrapSelection = (qualityScrapSelections[(int)qualityTier] ??= new WeightedSelection<ItemIndex>());
                                    return true;
                                }
                            }

                            targetQualityScrapSelection = null;
                            return false;
                        }
                    }
                    else
                    {
                        Log.Error("[Regenerating Scrap Priority] Failed to find priority scrap selection patch location");
                    }
                }

                // Item take priority rules
                {
                    /*
                     *  // CostTypeCatalog.<Init>g__TakeItemsFromWeightedSelection|5_20(weightedSelection3, ref CS$<>8__locals1, ref CS$<>8__locals2);
                     *  IL_0113: ldloc.s   V_5
                     *  IL_0115: ldloca.s  V_0
                     *  IL_0117: ldloca.s  V_2
                     *  IL_0119: call      void RoR2.CostTypeCatalog::'<Init>g__TakeItemsFromWeightedSelection|5_20'(class WeightedSelection`1<valuetype RoR2.ItemIndex>, valuetype RoR2.CostTypeCatalog/'<>c__DisplayClass5_0'&, valuetype RoR2.CostTypeCatalog/'<>c__DisplayClass5_1'&)
                     */

                    static bool matchCallTakeItemsFromWeightedSelection(Instruction x, out MethodReference takeItemsFromWeightedSelectionMethodRef)
                    {
                        return x.MatchCall(out takeItemsFromWeightedSelectionMethodRef) &&
                               takeItemsFromWeightedSelectionMethodRef?.Name?.StartsWith("<Init>g__TakeItemsFromWeightedSelection|") == true;
                    }

                    VariableDefinition locals1Var = null;
                    VariableDefinition locals2Var = null;
                    MethodReference takeItemsFromWeightedSelectionMethodRef = null;
                    if (c.TryGotoNext(MoveType.AfterLabel,
                                      x => x.MatchLdloc<WeightedSelection<ItemIndex>>(il, out _),
                                      x => x.MatchLdloca(il, out locals1Var),
                                      x => x.MatchLdloca(il, out locals2Var),
                                      x => matchCallTakeItemsFromWeightedSelection(x, out takeItemsFromWeightedSelectionMethodRef)))
                    {
                        // Take highest
                        {
                            int startIndex = c.Index;

                            VariableDefinition highestQualityRegeneratingScrapSelectionVar = itemSelectionTempVar;

                            ILLabel startRegularTakeItemsLabel = c.DefineLabel();

                            // if (tryGetHighestQualityRegeneratingScrapSelection) { take from highestQualityRegeneratingScrapSelection }
                            c.Emit(OpCodes.Ldloc, qualityRegeneratingScrapSelectionsVar);
                            c.Emit(OpCodes.Ldloca, highestQualityRegeneratingScrapSelectionVar);
                            c.EmitDelegate<TryGetHighestQualityRegeneratingScrapSelectionDelegate>(tryGetHighestQualityRegeneratingScrapSelection);
                            c.Emit(OpCodes.Brfalse, startRegularTakeItemsLabel);
                            static bool tryGetHighestQualityRegeneratingScrapSelection(WeightedSelection<ItemIndex>[] qualityScrapSelections, out WeightedSelection<ItemIndex> highestQualitySelection)
                            {
                                for (QualityTier qualityTier = QualityTier.Count - 1; qualityTier >= 0; qualityTier--)
                                {
                                    highestQualitySelection = qualityScrapSelections[(int)qualityTier];
                                    if (highestQualitySelection != null)
                                    {
                                        return true;
                                    }
                                }

                                highestQualitySelection = null;
                                return false;
                            }

                            VariableDefinition highestQualityRegeneratingScrapSelectionSingleVar = il.AddVariable<WeightedSelection<ItemIndex>>();
                            // because we only want to take 1 regenerating scrap, we make a copy selection that contains a single entry we take from the original and use that to try to take an item
                            {
                                c.EmitDelegate<Func<WeightedSelection<ItemIndex>>>(createQualityRegeneratingScrapSelection);
                                c.Emit(OpCodes.Stloc, highestQualityRegeneratingScrapSelectionSingleVar);

                                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                                static WeightedSelection<ItemIndex> createQualityRegeneratingScrapSelection()
                                {
                                    return new WeightedSelection<ItemIndex>();
                                }

                                c.Emit(OpCodes.Ldloc, highestQualityRegeneratingScrapSelectionVar);
                                c.Emit(OpCodes.Ldloc, highestQualityRegeneratingScrapSelectionSingleVar);
                                c.Emit(OpCodes.Ldarg, contextParameter);
                                c.EmitDelegate<Action<WeightedSelection<ItemIndex>, WeightedSelection<ItemIndex>, CostTypeDef.PayCostContext>>(moveSingleRandom);
                                static void moveSingleRandom(WeightedSelection<ItemIndex> src, WeightedSelection<ItemIndex> dest, CostTypeDef.PayCostContext context)
                                {
                                    if (src.Count > 0)
                                    {
                                        int moveChoiceIndex = src.EvaluateToChoiceIndex(context.rng.nextNormalizedFloat);
                                        WeightedSelection<ItemIndex>.ChoiceInfo moveChoiceInfo = src.GetChoice(moveChoiceIndex);

                                        int itemCount = (int)moveChoiceInfo.weight;
                                        itemCount--;
                                        if (itemCount > 0)
                                        {
                                            src.ModifyChoiceWeight(moveChoiceIndex, itemCount);
                                        }
                                        else
                                        {
                                            src.RemoveChoice(moveChoiceIndex);
                                        }

                                        int destChoiceIndex = dest.FindChoiceIndex(moveChoiceInfo.value);
                                        if (destChoiceIndex != -1)
                                        {
                                            dest.ModifyChoiceWeight(destChoiceIndex, dest.GetChoice(destChoiceIndex).weight + 1);
                                        }
                                        else
                                        {
                                            dest.AddChoice(moveChoiceInfo.value, 1);
                                        }
                                    }
                                }
                            }

                            c.Emit(OpCodes.Ldloc, highestQualityRegeneratingScrapSelectionSingleVar);
                            c.Emit(OpCodes.Ldloca, locals1Var);
                            c.Emit(OpCodes.Ldloca, locals2Var);
                            c.Emit(OpCodes.Call, takeItemsFromWeightedSelectionMethodRef);

                            // in case nothing was taken, add the transferred item back into our selection
                            {
                                c.Emit(OpCodes.Ldloc, highestQualityRegeneratingScrapSelectionSingleVar);
                                c.Emit(OpCodes.Ldloc, highestQualityRegeneratingScrapSelectionVar);
                                c.EmitDelegate<Action<WeightedSelection<ItemIndex>, WeightedSelection<ItemIndex>>>(WeightedSelectionExtensions.AddTo);
                            }

                            c.MarkLabel(startRegularTakeItemsLabel);

                            // MoveType.AfterLabel does not modify exception handlers, so they must be retargeted manually
                            c.Goto(startIndex, MoveType.Before);
                            foreach (ExceptionHandler exceptionHandler in il.Method.Body.ExceptionHandlers)
                            {
                                if (exceptionHandler.HandlerEnd == startRegularTakeItemsLabel.Target)
                                {
                                    exceptionHandler.HandlerEnd = c.Next;
                                }

                                if (exceptionHandler.TryEnd == startRegularTakeItemsLabel.Target)
                                {
                                    exceptionHandler.TryEnd = c.Next;
                                }
                            }
                        }

                        // Take remainder
                        {
                            c.Goto(-1);

                            /*
                             *  // CostTypeCatalog.<Init>g__TakeItemsFromWeightedSelection|5_20(weightedSelection, ref CS$<>8__locals1, ref CS$<>8__locals2);
                             *  IL_0129: ldloc.3
                             *  IL_012A: ldloca.s  V_0
                             *  IL_012C: ldloca.s  V_2
                             *  IL_012E: call      void RoR2.CostTypeCatalog::'<Init>g__TakeItemsFromWeightedSelection|5_20'(class WeightedSelection`1<valuetype RoR2.ItemIndex>, valuetype RoR2.CostTypeCatalog/'<>c__DisplayClass5_0'&, valuetype RoR2.CostTypeCatalog/'<>c__DisplayClass5_1'&)
                             */

                            if (c.TryGotoPrev(MoveType.AfterLabel,
                                              x => x.MatchLdloc<WeightedSelection<ItemIndex>>(il, out _),
                                              x => x.MatchLdloca(locals1Var),
                                              x => x.MatchLdloca(locals2Var),
                                              x => matchCallTakeItemsFromWeightedSelection(x, out _)))
                            {
                                // Take remainder lowest to highest
                                for (QualityTier qualityTier = 0; qualityTier < QualityTier.Count; qualityTier++)
                                {
                                    VariableDefinition qualityRegeneratingScrapSelectionVar = itemSelectionTempVar;
                                    ILLabel skipNullSelectionLabel = c.DefineLabel();

                                    c.Emit(OpCodes.Ldloc, qualityRegeneratingScrapSelectionsVar);
                                    c.Emit(OpCodes.Ldc_I4, (int)qualityTier);
                                    c.Emit(OpCodes.Ldelem_Ref);
                                    c.Emit(OpCodes.Stloc, qualityRegeneratingScrapSelectionVar);

                                    c.Emit(OpCodes.Ldloc, qualityRegeneratingScrapSelectionVar);
                                    c.Emit(OpCodes.Brfalse, skipNullSelectionLabel);

                                    c.Emit(OpCodes.Ldloc, qualityRegeneratingScrapSelectionVar);
                                    c.Emit(OpCodes.Ldloca, locals1Var);
                                    c.Emit(OpCodes.Ldloca, locals2Var);
                                    c.Emit(OpCodes.Call, takeItemsFromWeightedSelectionMethodRef);

                                    c.MarkLabel(skipNullSelectionLabel);
                                }
                            }
                            else
                            {
                                Log.Error("[Regenerating Scrap Priority] Failed to find take remainder regen scrap patch location");
                            }
                        }
                    }
                    else
                    {
                        Log.Error("[Regenerating Scrap Priority] Failed to find take priority regen scrap patch location");
                    }
                }
            }

            // Remove duplicate notification
            {
                c.Goto(0);
                if (c.TryGotoNext(MoveType.Before,
                                  x => x.MatchCallOrCallvirt<CharacterMasterNotificationQueue>(nameof(CharacterMasterNotificationQueue.SendTransformNotification))))
                {
                    c.EmitSkipMethodCall();
                }
                else
                {
                    Log.Warning("[Duplicate Notification Fix] Failed to find duplicate CharacterMasterNotificationQueue.SendTransformNotification call, it was likely removed");
                }
            }
        }

        delegate bool IsQualityRegeneratingScrapDelegate(ItemDef itemDef, WeightedSelection<ItemIndex>[] qualityScrapSelections, out WeightedSelection<ItemIndex> targetQualityScrapSelection);

        delegate bool TryGetHighestQualityRegeneratingScrapSelectionDelegate(WeightedSelection<ItemIndex>[] qualityScrapSelections, out WeightedSelection<ItemIndex> highestQualitySelection);

        delegate bool IsRegeneratingScrapDelegate(in Inventory.ItemTransformation itemTransformation);
    }
}
