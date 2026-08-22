using ItemQualities.Utilities.Extensions;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.Utils;
using RoR2;
using RoR2.UI;
using System;
using System.Collections;

namespace ItemQualities.Items
{
    internal static class ItemHooks
    {
        [SystemInitializer]
        private static void Init()
        {
            IL.RoR2.Inventory.UpdateEffectiveItemStacks += Inventory_UpdateEffectiveItemStacks;

            IL.RoR2.UI.ItemInventoryDisplay.OnInventoryChanged += ItemInventoryDisplay_OnInventoryChanged;

            IL.RoR2.CharacterModel.UpdateItemDisplay += CharacterModel_UpdateItemDisplay;

            On.RoR2.CharacterMaster.HighlightNewItem += CharacterMaster_HighlightNewItem;

            IL.RoR2.HealthComponent.TakeDamageProcess += HealthComponent_TakeDamageProcess;
        }

        private static void Inventory_UpdateEffectiveItemStacks(ILContext il)
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

            VariableDefinition effectiveItemCountVar = null;
            if (!c.TryFindPrev(out foundCursors,
                               x => x.MatchLdloc(typeof(int), il, out effectiveItemCountVar)))
            {
                Log.Error("Failed to find effectiveItemCount variable");
                return;
            }

            if (!c.TryFindPrev(out foundCursors,
                               x => x.MatchStloc(effectiveItemCountVar),
                               x => x.MatchCall(typeof(Math), nameof(Math.Clamp)),
                               x => x.MatchCallOrCallvirt<Inventory>("get_" + nameof(Inventory.inventoryDisabled))))
            {
                Log.Error("Failed to find patch location");
                return;
            }

            c.Goto(foundCursors[0].Next, MoveType.After);

            c.Emit(OpCodes.Ldloc, effectiveItemCountVar);
            c.Emit(OpCodes.Ldarg_0);
            c.Emit(OpCodes.Ldarg, itemIndexParameter);
            c.EmitDelegate<Func<Inventory, ItemIndex, int>>(getEffectiveItemCountFromQualities);
            c.Emit(OpCodes.Add);
            c.Emit(OpCodes.Stloc, effectiveItemCountVar);

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

        private static void ItemInventoryDisplay_OnInventoryChanged(ILContext il)
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

        private static void CharacterModel_UpdateItemDisplay(ILContext il)
        {
            if (!il.Method.TryFindParameter<Inventory>(out ParameterDefinition inventoryParameter))
            {
                Log.Error("Failed to find inventory parameter");
                return;
            }

            ILCursor c = new ILCursor(il);

            VariableDefinition itemIndexVar = null;
            if (!c.TryGotoNext(MoveType.After,
                               x => x.MatchLdarg(inventoryParameter.Sequence),
                               x => x.MatchLdloc(typeof(ItemIndex), il, out itemIndexVar),
                               x => x.MatchCallOrCallvirt<Inventory>(nameof(Inventory.CalculateEffectiveItemStacks))))
            {
                Log.Error("Failed to find patch location");
                return;
            }

            c.Emit(OpCodes.Ldarg, inventoryParameter);
            c.Emit(OpCodes.Ldloc, itemIndexVar);
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

        private static void HealthComponent_TakeDamageProcess(ILContext il)
        {
            if (!il.Method.TryFindParameter<DamageInfo>(out ParameterDefinition damageInfoParameter))
            {
                Log.Error("Failed to find DamageInfo parameter");
                return;
            }

            ILCursor c = new ILCursor(il);

            VariableDefinition damageValueVar = null;
            if (!c.TryGotoNext(x => x.MatchLdfld<TeamDef>(nameof(TeamDef.friendlyFireScaling))) ||
                !c.TryGotoPrev(x => x.MatchLdfld<DamageInfo>(nameof(DamageInfo.damage))) ||
                !c.TryGotoNext(MoveType.After,
                               x => x.MatchStloc(typeof(float), il, out damageValueVar)))
            {
                Log.Error("Failed to find patch location");
                return;
            }

            c.Emit(OpCodes.Ldloca, damageValueVar);
            c.Emit(OpCodes.Ldarg_0);
            c.Emit(OpCodes.Ldarg, damageInfoParameter);
            c.EmitDelegate<ModifyDamageDelegate>(modifyDamage);

            static void modifyDamage(ref float damageValue, HealthComponent victim, DamageInfo damageInfo)
            {
                try
                {
                    EquipmentMagazineVoid.ModifyTakeDamage(ref damageValue, victim, damageInfo);
                    Tooth.TakeDamageModifier(ref damageValue, victim, damageInfo);
                    BearVoid.TakeDamageModifier(ref damageValue, victim, damageInfo);
                }
                catch (Exception ex)
                {
                    Log.Error_NoCallerPrefix($"Failed to invoke TakeDamageModifier: {ex}");
                }
            }
        }

        private delegate void ModifyDamageDelegate(ref float damageValue, HealthComponent victim, DamageInfo damageInfo);

        private static IEnumerator CharacterMaster_HighlightNewItem(On.RoR2.CharacterMaster.orig_HighlightNewItem orig, CharacterMaster self, ItemIndex itemIndex)
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

        public static bool TryGotoNextItemCountVariable(ILCursor c, Type itemDeclaringType, string itemName, out VariableDefinition itemCountVariable)
        {
            static bool matchCallGetItemCountMethod(Instruction x)
            {
                return x.MatchCallOrCallvirt<Inventory>(nameof(Inventory.GetItemCount)) ||
                       x.MatchCallOrCallvirt<Inventory>(nameof(Inventory.GetItemCountEffective)) ||
                       x.MatchCallOrCallvirt<Inventory>(nameof(Inventory.GetItemCountChanneled)) ||
                       x.MatchCallOrCallvirt<Inventory>(nameof(Inventory.GetItemCountPermanent)) ||
                       x.MatchCallOrCallvirt<Inventory>(nameof(Inventory.GetItemCountTemp));
            }

            VariableDefinition _itemCountVariable = null;
            if (c.TryGotoNext(MoveType.After,
                              x => x.MatchLdsfld(itemDeclaringType, itemName),
                              matchCallGetItemCountMethod,
                              x => x.MatchStloc<int>(c.Context, out _itemCountVariable)))
            {
                itemCountVariable = _itemCountVariable;
                return true;
            }

            itemCountVariable = null;
            return false;
        }
    }
}
