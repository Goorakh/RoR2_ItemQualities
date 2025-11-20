using ItemQualities.Utilities.Extensions;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.Utils;
using RoR2;
using System;
using System.Collections;
using System.Diagnostics;
using System.Linq;
using UnityEngine.Networking;

namespace ItemQualities.Items
{
    static class ItemHooks
    {
        public delegate void ModifyDamageDelegate(ref float damageValue, DamageInfo damageInfo);
        public static event ModifyDamageDelegate TakeDamageModifier;

        [SystemInitializer]
        static void Init()
        {
            IL.RoR2.Inventory.UpdateEffectiveItemStacks += Inventory_UpdateEffectiveItemStacks;

            On.RoR2.CharacterMaster.HighlightNewItem += CharacterMaster_HighlightNewItem;

            On.RoR2.HealthComponent.GetBarrierDecayRate += HealthComponent_GetBarrierDecayRate;

            On.RoR2.CharacterBody.AddMultiKill += CharacterBody_AddMultiKill;

            IL.RoR2.HealthComponent.TakeDamageProcess += HealthComponent_TakeDamageProcess;
        }

        static void Inventory_UpdateEffectiveItemStacks(ILContext il)
        {
            if (!il.Method.TryFindParameter<ItemIndex>(out ParameterDefinition itemIndexParameter))
            {
                Log.Error("Failed to find ItemIndex parameter");
                return;
            }

            ILCursor c = new ILCursor(il);

            ILCursor[] foundCursors;
            if (!c.TryFindNext(out foundCursors,
                               x => x.MatchLdflda<Inventory>(nameof(Inventory.effectiveItemStacks)),
                               x => x.MatchCallOrCallvirt<ItemCollection>(nameof(ItemCollection.SetStackValue))))
            {
                Log.Error("Failed to find effectiveItemStacks.SetStackValue call");
                return;
            }

            c.Goto(foundCursors[1].Next, MoveType.After); // call ItemCollection.SetStackValue

            c.Emit(OpCodes.Ldarg_0);
            c.Emit(OpCodes.Ldarg, itemIndexParameter);
            c.EmitDelegate<Action<Inventory, ItemIndex>>(onSetEffectiveItemCount);

            static void onSetEffectiveItemCount(Inventory inventory, ItemIndex itemIndex)
            {
                if (QualityCatalog.GetQualityTier(itemIndex) > QualityTier.None)
                {
                    ItemIndex baseQualityItemIndex = QualityCatalog.GetItemIndexOfQuality(itemIndex, QualityTier.None);
                    if (baseQualityItemIndex != ItemIndex.None && baseQualityItemIndex != itemIndex)
                    {
                        inventory.UpdateEffectiveItemStacks(baseQualityItemIndex);
                    }
                }
            }

            int effectiveItemCountVarIndex = -1;
            if (!c.TryFindPrev(out foundCursors,
                               x => x.MatchLdloc(typeof(int), il, out effectiveItemCountVarIndex)))
            {
                Log.Error("Failed to find effectiveItemCount variable");
                return;
            }

            if (!c.TryFindPrev(out foundCursors,
                               x => x.MatchStloc(effectiveItemCountVarIndex),
                               x => x.MatchCall(typeof(Math), nameof(Math.Clamp)),
                               x => x.MatchCallOrCallvirt<Inventory>("get_" + nameof(Inventory.inventoryDisabled))))
            {
                Log.Error("Failed to find patch location");
                return;
            }

            c.Goto(foundCursors[0].Next, MoveType.After);

            c.Emit(OpCodes.Ldloc, effectiveItemCountVarIndex);
            c.Emit(OpCodes.Ldarg_0);
            c.Emit(OpCodes.Ldarg, itemIndexParameter);
            c.EmitDelegate<Func<Inventory, ItemIndex, int>>(getEffectiveItemCountFromQualities);
            c.Emit(OpCodes.Add);
            c.Emit(OpCodes.Stloc, effectiveItemCountVarIndex);

            static int getEffectiveItemCountFromQualities(Inventory inventory, ItemIndex itemIndex)
            {
                QualityTier qualityTier = QualityCatalog.GetQualityTier(itemIndex);
                if (qualityTier > QualityTier.None)
                    return 0;

                ItemQualityGroup qualityGroup = QualityCatalog.GetItemQualityGroup(QualityCatalog.FindItemQualityGroupIndex(itemIndex));
                if (!qualityGroup)
                    return 0;

                return qualityGroup.GetItemCountsEffective(inventory).TotalQualityCount;
            }
        }

        static void HealthComponent_TakeDamageProcess(ILContext il)
        {
            if (!il.Method.TryFindParameter<DamageInfo>(out ParameterDefinition damageInfoParameter))
            {
                Log.Error("Failed to find DamageInfo parameter");
                return;
            }

            ILCursor c = new ILCursor(il);

            int damageValueLocalIndex = -1;
            if (!c.TryGotoNext(x => x.MatchLdfld<TeamDef>(nameof(TeamDef.friendlyFireScaling))) ||
                !c.TryGotoPrev(x => x.MatchLdfld<DamageInfo>(nameof(DamageInfo.damage))) ||
                !c.TryGotoNext(MoveType.After,
                               x => x.MatchStloc(typeof(float), il, out damageValueLocalIndex)))
            {
                Log.Error("Failed to find patch location");
                return;
            }

            c.Emit(OpCodes.Ldloca, damageValueLocalIndex);
            c.Emit(OpCodes.Ldarg, damageInfoParameter);
            c.EmitDelegate<ModifyDamageDelegate>(invokeModifyDamage);

            static void invokeModifyDamage(ref float damageValue, DamageInfo damageInfo)
            {
                foreach (ModifyDamageDelegate takeDamageModifier in TakeDamageModifier.GetInvocationList().OfType<ModifyDamageDelegate>())
                {
                    try
                    {
                        takeDamageModifier?.Invoke(ref damageValue, damageInfo);
                    }
                    catch (Exception ex)
                    {
                        Log.Error_NoCallerPrefix($"Failed to invoke TakeDamageModifier: {ex}");
                    }
                }
            }
        }

        static void CharacterBody_AddMultiKill(On.RoR2.CharacterBody.orig_AddMultiKill orig, CharacterBody self, int kills)
        {
            if (NetworkServer.active && self && self.TryGetComponent(out CharacterBodyExtraStatsTracker bodyExtraStatsTracker))
            {
                bodyExtraStatsTracker.AddMultiKill(kills);
            }

            orig(self, kills);
        }

        static float HealthComponent_GetBarrierDecayRate(On.RoR2.HealthComponent.orig_GetBarrierDecayRate orig, HealthComponent self)
        {
            float barrierDecayRate = orig(self);
            
            if (self && self.TryGetComponent(out CharacterBodyExtraStatsTracker extraStatsTracker))
            {
                barrierDecayRate *= extraStatsTracker.BarrierDecayRateMultiplier;
            }

            return barrierDecayRate;
        }

        static IEnumerator CharacterMaster_HighlightNewItem(On.RoR2.CharacterMaster.orig_HighlightNewItem orig, CharacterMaster self, ItemIndex itemIndex)
        {
            return orig(self, QualityCatalog.GetItemIndexOfQuality(itemIndex, QualityTier.None));
        }

        [Obsolete]
        public static void CombineGroupedItemCountsPatch(ILContext il)
        {
            return;

            ILCursor c = new ILCursor(il);

            VariableDefinition inventoryTempVar = il.AddVariable<Inventory>();
            VariableDefinition itemIndexTempVar = il.AddVariable<ItemIndex>();
            VariableDefinition itemDefTempVar = il.AddVariable<ItemDef>();

            int patchCount = 0;

            while (c.TryGotoNext(MoveType.Before,
                                 x => x.MatchCallOrCallvirt<Inventory>(nameof(Inventory.GetItemCount))))
            {
                EmitSingleCombineGroupedItemCounts(c, inventoryTempVar, itemIndexTempVar, itemDefTempVar);

                patchCount++;
            }

            if (patchCount == 0)
            {
                Log.Error($"Failed to find patch location for {il.Method.FullName}");
            }
            else
            {
                Log.Debug($"Found {patchCount} patch location(s) for {il.Method.FullName}");
            }
        }

        [Obsolete]
        public static void EmitSingleCombineGroupedItemCounts(ILCursor c, VariableDefinition inventoryTempVar = null, VariableDefinition itemIndexTempVar = null, VariableDefinition itemDefTempVar = null)
        {
            if (!c.Next.MatchCallOrCallvirt<Inventory>(nameof(Inventory.GetItemCount)))
            {
                Log.Error($"Cursor must be placed before a GetItemCount call: {new StackTrace()}");
                return;
            }

            inventoryTempVar ??= c.Context.AddVariable<Inventory>();

            bool isItemIndex = ((MethodReference)c.Next.Operand).Parameters[0].ParameterType.Is(typeof(ItemIndex));

            VariableDefinition itemArgTempVar = isItemIndex ? itemIndexTempVar : itemDefTempVar;
            itemArgTempVar ??= c.Context.AddVariable(isItemIndex ? typeof(ItemIndex) : typeof(ItemDef));

            c.EmitStoreStack(inventoryTempVar, itemArgTempVar);

            c.Index++;

            c.Emit(OpCodes.Ldloc, inventoryTempVar);
            c.Emit(OpCodes.Ldloc, itemArgTempVar);

            static int tryGetCombinedItemCountShared(int baseItemCount, Inventory inventory, ItemIndex itemIndex)
            {
                if (inventory)
                {
                    ItemQualityGroup itemGroup = QualityCatalog.GetItemQualityGroup(QualityCatalog.FindItemQualityGroupIndex(itemIndex));
                    if (itemGroup)
                    {
                        baseItemCount = itemGroup.GetItemCounts(inventory).TotalCount;
                    }
                }

                return baseItemCount;
            }

            if (isItemIndex)
            {
                c.EmitDelegate<Func<int, Inventory, ItemIndex, int>>(tryGetCombinedItemCount);
                static int tryGetCombinedItemCount(int baseItemCount, Inventory inventory, ItemIndex itemIndex)
                {
                    return tryGetCombinedItemCountShared(baseItemCount, inventory, itemIndex);
                }
            }
            else
            {
                c.EmitDelegate<Func<int, Inventory, ItemDef, int>>(tryGetCombinedItemCount);
                static int tryGetCombinedItemCount(int baseItemCount, Inventory inventory, ItemDef itemDef)
                {
                    return tryGetCombinedItemCountShared(baseItemCount, inventory, itemDef ? itemDef.itemIndex : ItemIndex.None);
                }
            }
        }

        public static bool TryFindNextItemCountVariable(ILCursor c, Type itemDeclaringType, string itemName, out VariableDefinition itemCountVariable)
        {
            int itemCountVariableIndex = -1;
            if (c.TryFindNext(out ILCursor[] foundCursors,
                              x => x.MatchLdsfld(itemDeclaringType, itemName),
                              x => x.MatchCallOrCallvirt<Inventory>(nameof(Inventory.GetItemCount)),
                              x => x.MatchStloc(out itemCountVariableIndex) && c.Context.Method.Body.Variables[itemCountVariableIndex].VariableType.Is(typeof(int))))
            {
                itemCountVariable = c.Context.Method.Body.Variables[itemCountVariableIndex];
                return true;
            }

            itemCountVariable = null;
            return false;
        }

        public static bool EmitCombinedQualityItemTransformationPatch(ILCursor c)
        {
            return EmitCombinedQualityItemTransformationPatch(c, out _);
        }

        public static bool EmitCombinedQualityItemTransformationPatch(ILCursor c, out VariableDefinition qualityItemTransformResultVar)
        {
            qualityItemTransformResultVar = null;

            if (!c.Prev.MatchCallOrCallvirt<Inventory.ItemTransformation>(nameof(Inventory.ItemTransformation.TryTransform)))
            {
                Log.Error($"Cursor must be located directly after an Inventory.ItemTransformation.TryTransform call. {c.Context.Method.FullName} at {c.Prev.SafeToString()}");
                return false;
            }

            c.Index--;

            VariableDefinition itemTransformationVar = c.Context.AddVariable(typeof(Inventory.ItemTransformation).MakeByRefType());
            VariableDefinition inventoryVar = c.Context.AddVariable<Inventory>();
            VariableDefinition resultByRefVar = c.Context.AddVariable(typeof(Inventory.ItemTransformation.TryTransformResult).MakeByRefType());
            qualityItemTransformResultVar = c.Context.AddVariable<QualityItemTransformResult>();

            c.EmitStoreStack(itemTransformationVar, inventoryVar, resultByRefVar);

            c.Index++;

            c.Emit(OpCodes.Ldloc, itemTransformationVar);
            c.Emit(OpCodes.Ldloc, inventoryVar);
            c.Emit(OpCodes.Ldloc, resultByRefVar);
            c.Emit(OpCodes.Ldloca, qualityItemTransformResultVar);
            c.EmitDelegate<TryTransformCombinedQualitiesDelegate>(tryTransformCombinedQualities);

            static bool tryTransformCombinedQualities(bool result, in Inventory.ItemTransformation itemTransformation, Inventory inventory, ref Inventory.ItemTransformation.TryTransformResult transformResult, out QualityItemTransformResult qualityItemTransformResult)
            {
                qualityItemTransformResult = QualityItemTransformResult.Create();

                if (result)
                {
                    qualityItemTransformResult.TakenItems.ItemIndex = transformResult.takenItem.itemIndex;
                    qualityItemTransformResult.GivenItems.ItemIndex = transformResult.givenItem.itemIndex;

                    qualityItemTransformResult.TakenItems.StackValues.AddItemCountsFrom(transformResult.takenItem.stackValues, QualityTier.None);
                }
                else
                {
                    qualityItemTransformResult.TakenItems.ItemIndex = itemTransformation.originalItemIndex;
                    qualityItemTransformResult.GivenItems.ItemIndex = itemTransformation.newItemIndex;
                }

                int totalTransformed = result ? transformResult.totalTransformed : 0;

                if (totalTransformed < itemTransformation.maxToTransform)
                {
                    for (QualityTier qualityTier = 0; qualityTier < QualityTier.Count; qualityTier++)
                    {
                        ItemIndex qualityOriginalItemIndex = QualityCatalog.GetItemIndexOfQuality(itemTransformation.originalItemIndex, qualityTier);
                        ItemIndex qualityNewItemIndex = QualityCatalog.GetItemIndexOfQuality(itemTransformation.newItemIndex, qualityTier);

                        if (qualityOriginalItemIndex != itemTransformation.originalItemIndex &&
                            ((itemTransformation.newItemIndex == ItemIndex.None) || qualityNewItemIndex != itemTransformation.newItemIndex))
                        {
                            Inventory.ItemTransformation qualityItemTransformation = itemTransformation;
                            qualityItemTransformation.originalItemIndex = qualityOriginalItemIndex;
                            qualityItemTransformation.newItemIndex = qualityNewItemIndex;
                            qualityItemTransformation.maxToTransform = itemTransformation.maxToTransform - totalTransformed;

                            if (qualityItemTransformation.TryTransform(inventory, out Inventory.ItemTransformation.TryTransformResult qualityTransformResult))
                            {
                                result |= true;

                                static void addStackValues(ref Inventory.ItemStackValues a, in Inventory.ItemStackValues b)
                                {
                                    a.permanentStacks += b.permanentStacks;
                                    a.temporaryStacksValue += b.temporaryStacksValue;
                                    a.totalStacks += b.totalStacks;
                                }

                                addStackValues(ref transformResult.takenItem.stackValues, qualityTransformResult.takenItem.stackValues);
                                addStackValues(ref transformResult.givenItem.stackValues, qualityTransformResult.givenItem.stackValues);

                                qualityItemTransformResult.TakenItems.StackValues.AddItemCountsFrom(qualityTransformResult.takenItem.stackValues, qualityTier);
                                qualityItemTransformResult.GivenItems.StackValues.AddItemCountsFrom(qualityTransformResult.givenItem.stackValues, qualityTier);

                                totalTransformed += qualityTransformResult.totalTransformed;
                                if (totalTransformed >= itemTransformation.maxToTransform)
                                    break;
                            }
                        }
                    }
                }

                return result;
            }

            return true;
        }

        delegate bool TryTransformCombinedQualitiesDelegate(bool result, in Inventory.ItemTransformation itemTransformation, Inventory inventory, ref Inventory.ItemTransformation.TryTransformResult transformResult, out QualityItemTransformResult qualityItemTransformResult);
    }
}
