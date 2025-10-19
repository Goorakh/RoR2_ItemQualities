using ItemQualities.Utilities.Extensions;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;
using MonoMod.Utils;
using RoR2;
using System;
using System.Collections;
using System.Diagnostics;
using System.Reflection;
using UnityEngine.Networking;

namespace ItemQualities.Items
{
    static class ItemHooks
    {
        [SystemInitializer]
        static void Init()
        {
            IL.EntityStates.Headstompers.HeadstompersCooldown.OnEnter += CombineGroupedItemCountsPatch;
            IL.EntityStates.Headstompers.HeadstompersFall.DoStompExplosionAuthority += CombineGroupedItemCountsPatch;
            IL.RoR2.CharacterBody.AddMultiKill += CombineGroupedItemCountsPatch;
            IL.RoR2.CharacterBody.AddTimedBuff_BuffDef_float += CombineGroupedItemCountsPatch;
            IL.RoR2.CharacterBody.GetMaxIncreasedDamageMultiKillBuffsForCharacter += CombineGroupedItemCountsPatch;
            IL.RoR2.CharacterBody.OnClientBuffsChanged += CombineGroupedItemCountsPatch;
            IL.RoR2.CharacterBody.OnInventoryChanged += CombineGroupedItemCountsPatch;
            IL.RoR2.CharacterBody.OnKilledOtherServer += CombineGroupedItemCountsPatch;
            IL.RoR2.CharacterBody.RecalculateStats += CombineGroupedItemCountsPatch;
            IL.RoR2.CharacterBody.RemoveBuff_BuffIndex += CombineGroupedItemCountsPatch;
            IL.RoR2.CharacterBody.TriggerEnemyDebuffs += CombineGroupedItemCountsPatch;
            IL.RoR2.CharacterBody.UpdateAllTemporaryVisualEffects += CombineGroupedItemCountsPatch;
            IL.RoR2.CharacterBody.UpdateMultiKill += CombineGroupedItemCountsPatch;
            IL.RoR2.CharacterMaster.GetDeployableSameSlotLimit += CombineGroupedItemCountsPatch;
            IL.RoR2.CharacterMaster.OnBodyStart += CombineGroupedItemCountsPatch;
            IL.RoR2.CharacterMaster.OnInventoryChanged += CombineGroupedItemCountsPatch;
            IL.RoR2.CharacterModel.UpdateItemDisplay += CombineGroupedItemCountsPatch;
            IL.RoR2.CharacterModel.UpdateOverlays += CombineGroupedItemCountsPatch;
            IL.RoR2.EquipmentSlot.OnEquipmentExecuted += CombineGroupedItemCountsPatch;
            IL.RoR2.FootstepHandler.Footstep_string_GameObject += CombineGroupedItemCountsPatch;
            IL.RoR2.GlobalEventManager.OnCharacterDeath += CombineGroupedItemCountsPatch;
            IL.RoR2.GlobalEventManager.OnCharacterHitGroundServer += CombineGroupedItemCountsPatch;
            IL.RoR2.GlobalEventManager.OnCrit += CombineGroupedItemCountsPatch;
            IL.RoR2.GlobalEventManager.OnInteractionBegin += CombineGroupedItemCountsPatch;
            IL.RoR2.GlobalEventManager.ProcDeathMark += CombineGroupedItemCountsPatch;
            IL.RoR2.GlobalEventManager.ProcessHitEnemy += CombineGroupedItemCountsPatch;
            IL.RoR2.HealthComponent.TakeDamageProcess += CombineGroupedItemCountsPatch;
            IL.RoR2.Inventory.CalculateEquipmentCooldownScale += CombineGroupedItemCountsPatch;
            IL.RoR2.Inventory.GetEquipmentSlotMaxCharges += CombineGroupedItemCountsPatch;
            IL.RoR2.Inventory.UpdateEquipment += CombineGroupedItemCountsPatch;
            IL.RoR2.Items.BaseItemBodyBehavior.UpdateBodyItemBehaviorStacks += CombineGroupedItemCountsPatch;
            IL.RoR2.Items.WardOnLevelManager.OnCharacterLevelUp += CombineGroupedItemCountsPatch;
            IL.RoR2.PurchaseInteraction.OnInteractionBegin += CombineGroupedItemCountsPatch;
            IL.RoR2.SceneDirector.PopulateScene += CombineGroupedItemCountsPatch;
            IL.RoR2.SetStateOnHurt.OnTakeDamageServer += CombineGroupedItemCountsPatch;
            IL.RoR2.ShrineChanceBehavior.AddShrineStack += CombineGroupedItemCountsPatch;
            IL.RoR2.TeleporterInteraction.ChargingState.OnEnter += CombineGroupedItemCountsPatch;
            IL.RoR2.Util.GetItemCountForTeam += CombineGroupedItemCountsPatch;
            IL.RoR2.Util.GetItemCountGlobal += CombineGroupedItemCountsPatch;

            ConstructorInfo itemCountsCtor = typeof(HealthComponent.ItemCounts).GetConstructor(new Type[] { typeof(Inventory) });
            if (itemCountsCtor != null)
            {
                new ILHook(itemCountsCtor, CombineGroupedItemCountsPatch);
            }
            else
            {
                Log.Error("Failed to find Inventory.ItemCounts..ctor");
            }

            On.RoR2.CharacterMaster.HighlightNewItem += CharacterMaster_HighlightNewItem;

            On.RoR2.CharacterBody.RecalculateStats += CharacterBody_RecalculateStats;

            On.RoR2.CharacterBody.AddMultiKill += CharacterBody_AddMultiKill;
        }

        static void CharacterBody_AddMultiKill(On.RoR2.CharacterBody.orig_AddMultiKill orig, CharacterBody self, int kills)
        {
            if (NetworkServer.active && self && self.TryGetComponent(out CharacterBodyExtraStatsTracker bodyExtraStatsTracker))
            {
                bodyExtraStatsTracker.AddMultiKill(kills);
            }

            orig(self, kills);
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

        public static void CombineGroupedItemCountsPatch(ILContext il)
        {
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
    }
}
