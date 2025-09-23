using ItemQualities.Utilities.Extensions;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;
using MonoMod.Utils;
using RoR2;
using System;
using System.Collections;
using System.Reflection;

namespace ItemQualities.Items
{
    static class ItemHooks
    {
        [SystemInitializer]
        static void Init()
        {
            IL.RoR2.CharacterBody.RecalculateStats += CombineGroupedItemCountsPatch;
            IL.RoR2.GlobalEventManager.OnCharacterDeath += CombineGroupedItemCountsPatch;
            IL.RoR2.GlobalEventManager.OnInteractionBegin += CombineGroupedItemCountsPatch;
            IL.RoR2.HealthComponent.TakeDamageProcess += CombineGroupedItemCountsPatch;
            IL.RoR2.CharacterBody.RemoveBuff_BuffIndex += CombineGroupedItemCountsPatch;

            ConstructorInfo itemCountsCtor = typeof(HealthComponent.ItemCounts).GetConstructor(new Type[] { typeof(Inventory) });
            if (itemCountsCtor != null)
            {
                new ILHook(itemCountsCtor, CombineGroupedItemCountsPatch);
            }
            else
            {
                Log.Error("Failed to find Inventory.ItemCounts..ctor");
            }

            IL.RoR2.CharacterModel.UpdateItemDisplay += CombineGroupedItemCountsPatch;
            IL.RoR2.FootstepHandler.Footstep_string_GameObject += CombineGroupedItemCountsPatch;

            IL.RoR2.ShrineChanceBehavior.AddShrineStack += CombineGroupedItemCountsPatch;

            On.RoR2.CharacterMaster.HighlightNewItem += CharacterMaster_HighlightNewItem;

            On.RoR2.CharacterBody.RecalculateStats += CharacterBody_RecalculateStats;
        }

        static void CharacterBody_RecalculateStats(On.RoR2.CharacterBody.orig_RecalculateStats orig, CharacterBody self)
        {
            orig(self);

            if (self && self.TryGetComponent(out CharacterBodyExtraStatsTracker extraStatsTracker))
            {
                self.barrierDecayRate *= extraStatsTracker.BarrierDecayRateMultiplier;
            }
        }

        static IEnumerator CharacterMaster_HighlightNewItem(On.RoR2.CharacterMaster.orig_HighlightNewItem orig, CharacterMaster self, ItemIndex itemIndex)
        {
            ItemQualityGroup itemGroup = QualityCatalog.GetItemQualityGroup(QualityCatalog.FindItemQualityGroupIndex(itemIndex));
            if (itemGroup)
            {
                itemIndex = itemGroup.BaseItemIndex;
            }

            return orig(self, itemIndex);
        }

        static void CombineGroupedItemCountsPatch(ILContext il)
        {
            ILCursor c = new ILCursor(il);

            VariableDefinition inventoryTempVar = il.AddVariable<Inventory>();
            VariableDefinition itemIndexTempVar = il.AddVariable<ItemIndex>();
            VariableDefinition itemDefTempVar = il.AddVariable<ItemDef>();

            int patchCount = 0;

            while (c.TryGotoNext(MoveType.Before,
                                 x => x.MatchCallOrCallvirt<Inventory>(nameof(Inventory.GetItemCount))))
            {
                bool isItemIndex = ((MethodReference)c.Next.Operand).Parameters[0].ParameterType.Is(typeof(ItemIndex));

                VariableDefinition itemArgTempVar = isItemIndex ? itemIndexTempVar : itemDefTempVar;

                c.EmitStoreStack(inventoryTempVar, itemArgTempVar);

                c.Index++;

                c.Emit(OpCodes.Ldloc, inventoryTempVar);
                c.Emit(OpCodes.Ldloc, itemArgTempVar);

                static int tryGetCombinedItemCountShared(int baseItemCount, Inventory inventory, ItemIndex itemIndex)
                {
                    if (!inventory)
                        return baseItemCount;

                    ItemQualityGroup itemGroup = QualityCatalog.GetItemQualityGroup(QualityCatalog.FindItemQualityGroupIndex(itemIndex));
                    if (itemGroup)
                    {
                        for (QualityTier qualityTier = QualityTier.None; qualityTier < QualityTier.Count; qualityTier++)
                        {
                            ItemIndex itemIndexInGroup = itemGroup.GetItemIndex(qualityTier);
                            if (itemIndexInGroup != ItemIndex.None && itemIndex != itemIndexInGroup)
                            {
                                baseItemCount += inventory.GetItemCount(itemIndexInGroup);
                            }
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
    }
}
