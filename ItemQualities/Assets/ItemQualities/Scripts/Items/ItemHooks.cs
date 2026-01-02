using ItemQualities.Utilities.Extensions;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.Utils;
using RoR2;
using RoR2.UI;
using System;
using System.Collections;
using System.Linq;

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

            IL.RoR2.UI.ItemInventoryDisplay.OnInventoryChanged += ItemInventoryDisplay_OnInventoryChanged;

            IL.RoR2.CharacterModel.UpdateItemDisplay += CharacterModel_UpdateItemDisplay;

            On.RoR2.CharacterMaster.HighlightNewItem += CharacterMaster_HighlightNewItem;

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
                if (!inventory)
                    return 0;

                QualityTier qualityTier = QualityCatalog.GetQualityTier(itemIndex);
                if (qualityTier > QualityTier.None)
                    return 0;

                return inventory.GetItemCountsEffective(QualityCatalog.FindItemQualityGroupIndex(itemIndex)).TotalQualityCount;
            }
        }

        static void ItemInventoryDisplay_OnInventoryChanged(ILContext il)
        {
            ILCursor c = new ILCursor(il);

            if (!c.TryGotoNext(MoveType.After,
                               x => x.MatchCallOrCallvirt<Inventory>(nameof(Inventory.WriteItemStacks))))
            {
                Log.Error("Failed to find patch location");
                return;
            }

            c.Emit(OpCodes.Ldarg_0);
            c.EmitDelegate<Action<ItemInventoryDisplay>>(undoQualityEffectiveStacks);

            static void undoQualityEffectiveStacks(ItemInventoryDisplay itemInventoryDisplay)
            {
                if (!itemInventoryDisplay.inventory || itemInventoryDisplay.itemStacks == null || itemInventoryDisplay.itemStacks.Length != ItemCatalog.itemCount)
                    return;

                for (ItemQualityGroupIndex itemGroupIndex = 0; (int)itemGroupIndex < QualityCatalog.ItemQualityGroupCount; itemGroupIndex++)
                {
                    ItemQualityGroup itemGroup = QualityCatalog.GetItemQualityGroup(itemGroupIndex);
                    if (itemGroup.BaseItemIndex != ItemIndex.None)
                    {
                        ref int baseItemCount = ref itemInventoryDisplay.itemStacks[(int)itemGroup.BaseItemIndex];
                        if (baseItemCount > 0)
                        {
                            ItemQualityCounts itemCounts = itemInventoryDisplay.inventory.GetItemCountsEffective(itemGroup);
                            baseItemCount = Math.Max(0, baseItemCount - itemCounts.TotalQualityCount);
                        }
                    }
                }
            }
        }

        static void CharacterModel_UpdateItemDisplay(ILContext il)
        {
            if (!il.Method.TryFindParameter<Inventory>(out ParameterDefinition inventoryParameter))
            {
                Log.Error("Failed to find inventory parameter");
                return;
            }

            ILCursor c = new ILCursor(il);

            int itemIndexVarIndex = -1;
            if (!c.TryGotoNext(MoveType.After,
                               x => x.MatchLdarg(inventoryParameter.Sequence),
                               x => x.MatchLdloc(typeof(ItemIndex), il, out itemIndexVarIndex),
                               x => x.MatchCallOrCallvirt<Inventory>(nameof(Inventory.CalculateEffectiveItemStacks))))
            {
                Log.Error("Failed to find patch location");
                return;
            }

            c.Emit(OpCodes.Ldarg, inventoryParameter);
            c.Emit(OpCodes.Ldloc, itemIndexVarIndex);
            c.EmitDelegate<Func<int, Inventory, ItemIndex, int>>(getItemCountWithQualities);

            static int getItemCountWithQualities(int itemCount, Inventory inventory, ItemIndex itemIndex)
            {
                if (inventory && QualityCatalog.GetQualityTier(itemIndex) == QualityTier.None)
                {
                    ItemQualityGroupIndex itemGroupIndex = QualityCatalog.FindItemQualityGroupIndex(itemIndex);
                    if (itemGroupIndex != ItemQualityGroupIndex.Invalid)
                    {
                        itemCount += inventory.GetItemCountsEffective(itemGroupIndex).TotalQualityCount;
                    }
                }

                return itemCount;
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

        static IEnumerator CharacterMaster_HighlightNewItem(On.RoR2.CharacterMaster.orig_HighlightNewItem orig, CharacterMaster self, ItemIndex itemIndex)
        {
            return orig(self, QualityCatalog.GetItemIndexOfQuality(itemIndex, QualityTier.None));
        }

        public static bool MatchCallLocalCheckRoll(Instruction instruction)
        {
            if (instruction.MatchCallOrCallvirt(out MethodReference method) && !string.IsNullOrEmpty(method?.Name))
            {
                if (method.Name.Contains(">g__LocalCheckRoll|"))
                {
                    return true;
                }
            }

            return false;
        }

        public static bool TryFindNextItemCountVariable(ILCursor c, Type itemDeclaringType, string itemName, out VariableDefinition itemCountVariable)
        {
            int itemCountVariableIndex = -1;
            if (c.TryFindNext(out ILCursor[] foundCursors,
                              x => x.MatchLdsfld(itemDeclaringType, itemName),
                              x => x.MatchCallOrCallvirt<Inventory>(nameof(Inventory.GetItemCountEffective)),
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
