using HG;
using ItemQualities.Utilities.Extensions;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;
using RoR2;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace ItemQualities
{
    static class ItemCostQualityPatch
    {
        [SystemInitializer(typeof(CostTypeCatalog))]
        static void Init()
        {
            CostTypeDef itemCostDef = CostTypeCatalog.GetCostTypeDef(CostTypeIndex.WhiteItem);
            if (itemCostDef?.isAffordable?.Method != null)
            {
                new Hook(itemCostDef.isAffordable.Method, new Func<CostTypeCatalog_IsAffordableItem_orig, CostTypeDef, CostTypeDef.IsAffordableContext, bool>(CostTypeCatalog_IsAffordableItem));
            }
            else
            {
                Log.Error("Failed to find IsAffordableItem method");
            }

            if (itemCostDef?.payCost?.Method != null)
            {
                new ILHook(itemCostDef.payCost.Method, CostTypeCatalog_PayCostItems);
            }
            else
            {
                Log.Error("Failed to find PayCostItems method");
            }

            IL.RoR2.ChestBehavior.BaseItemDrop += ChestBehavior_BaseItemDrop;
            IL.RoR2.ShopTerminalBehavior.DropPickup_bool += ShopTerminalBehavior_DropPickup;
            IL.RoR2.OptionChestBehavior.ItemDrop += OptionChestBehavior_ItemDrop;
        }

        delegate bool CostTypeCatalog_IsAffordableItem_orig(CostTypeDef costTypeDef, CostTypeDef.IsAffordableContext context);
        static bool CostTypeCatalog_IsAffordableItem(CostTypeCatalog_IsAffordableItem_orig orig, CostTypeDef costTypeDef, CostTypeDef.IsAffordableContext context)
        {
            return orig(costTypeDef, context) &&
                   context.activator &&
                   context.activator.TryGetComponent(out CharacterBody activatorBody) &&
                   activatorBody.inventory &&
                   activatorBody.inventory.HasAtLeastXTotalNonQualityItemsOfTierForPurchase(costTypeDef.itemTier, context.cost);
        }

        static void CostTypeCatalog_PayCostItems(ILContext il)
        {
            ILCursor c = new ILCursor(il);

            // Disallow quality items (except quality regen scrap)
            {
                c.Goto(0);

                /*
                 *  // if (itemIndex != CS$<>8__locals1.context.avoidedItemIndex)
                 *  IL_007A: ldloc.s   V_9
                 *  IL_007C: ldloc.0
                 *  IL_007D: ldfld     class RoR2.CostTypeDef/PayCostContext RoR2.CostTypeCatalog/'<>c__DisplayClass5_0'::context
                 *  IL_0082: ldfld     valuetype RoR2.ItemIndex RoR2.CostTypeDef/PayCostContext::avoidedItemIndex
                 *  IL_0087: beq.s     IL_00F7
                 */

                VariableDefinition itemIndexVar = default;
                ILLabel skipItemLabel = default;
                if (c.TryGotoNext(MoveType.After,
                                  x => x.MatchLdloc<ItemIndex>(il, out itemIndexVar),
                                  x => x.MatchLdloc(out _),
                                  x => x.MatchLdfld(out _), // context
                                  x => x.MatchLdfld<CostTypeDef.PayCostContext>(nameof(CostTypeDef.PayCostContext.avoidedItemIndex)),
                                  x => x.MatchBeq(out skipItemLabel)))
                {
                    c.Emit(OpCodes.Ldloc, itemIndexVar);
                    c.EmitDelegate<Func<ItemIndex, bool>>(isItemAllowed);
                    c.Emit(OpCodes.Brfalse, skipItemLabel);

                    [MethodImpl(MethodImplOptions.AggressiveInlining)]
                    static bool isItemAllowed(ItemIndex itemIndex)
                    {
                        return QualityCatalog.GetQualityTier(itemIndex) == QualityTier.None ||
                               QualityCatalog.FindItemQualityGroupIndex(itemIndex) == ItemQualitiesContent.ItemQualityGroups.RegeneratingScrap.GroupIndex;
                    }
                }
                else
                {
                    Log.Error("Failed to find quality item filter patch location");
                }
            }

            // Quality item transformation
            {
                c.Goto(0);

                /*
                 *  // if (itemTransformation.TryTransform(inventory, out tryTransformResult))
                 *  IL_01D8: ldloca.s  V_15
                 *  IL_01DA: ldloc.1
                 *  IL_01DB: ldloca.s  V_16
                 *  IL_01DD: call      instance bool RoR2.Inventory/ItemTransformation::TryTransform(class RoR2.Inventory, valuetype RoR2.Inventory/ItemTransformation/TryTransformResult&)
                 *  IL_01E2: brfalse.s IL_01EC
                 */

                VariableDefinition itemTransformationVar = default;
                if (c.TryGotoNext(MoveType.AfterLabel,
                                  x => x.MatchLdloca<Inventory.ItemTransformation>(il, out itemTransformationVar),
                                  x => x.MatchLdloc(out _), // inventory
                                  x => x.MatchLdloca(out _), // tryTransformResult
                                  x => x.MatchCallOrCallvirt<Inventory.ItemTransformation>(nameof(Inventory.ItemTransformation.TryTransform))))
                {
                    c.Emit(OpCodes.Ldloca, itemTransformationVar);
                    c.EmitDelegate<SetTransformedItemQualityDelegate>(setTransformedItemQuality);

                    // If there is a new item from the transformation, inherit whatever quality the input item had
                    static void setTransformedItemQuality(ref Inventory.ItemTransformation itemTransformation)
                    {
                        if (itemTransformation.newItemIndex != ItemIndex.None)
                        {
                            itemTransformation.newItemIndex = QualityCatalog.GetItemIndexOfQuality(itemTransformation.newItemIndex, QualityCatalog.GetQualityTier(itemTransformation.originalItemIndex));
                        }
                    }
                }
                else
                {
                    Log.Error("Failed to find item transformation quality patch location");
                }
            }
        }

        private delegate void SetTransformedItemQualityDelegate(ref Inventory.ItemTransformation itemTransformation);

        static QualityTier getOutputQualityTierFromCost(GameObject dropperObject)
        {
            if (!dropperObject ||
                !dropperObject.TryGetComponent(out ObjectPurchaseContext purchaseContext) ||
                purchaseContext.Results == null)
            {
                return QualityTier.None;
            }

            ObjectPurchaseContext.PurchaseResults payCostResults = purchaseContext.Results;

            using var _ = ListPool<UniquePickup>.RentCollection(out List<UniquePickup> pickupsSpentOnPurchase);
            ListUtils.EnsureCapacity(pickupsSpentOnPurchase, payCostResults.ItemStacksTaken.Length + payCostResults.EquipmentTaken.Length);

            foreach (Inventory.ItemAndStackValues itemStackValues in payCostResults.ItemStacksTaken)
            {
                itemStackValues.AddAsPickupsToList(pickupsSpentOnPurchase);
            }

            foreach (EquipmentIndex equipmentIndex in payCostResults.EquipmentTaken)
            {
                if (equipmentIndex != EquipmentIndex.None)
                {
                    pickupsSpentOnPurchase.Add(new UniquePickup(PickupCatalog.FindPickupIndex(equipmentIndex)));
                }
            }

            QualityTier highestInputQualityTier = QualityTier.None;
            foreach (UniquePickup inputPickup in pickupsSpentOnPurchase)
            {
                QualityTier qualityTier = QualityCatalog.GetQualityTier(inputPickup.pickupIndex);
                highestInputQualityTier = QualityCatalog.Max(highestInputQualityTier, qualityTier);
            }

            return highestInputQualityTier;
        }

        static PickupIndex tryUpgradeQualityFromCost(PickupIndex intendedDropPickupIndex, GameObject dropperObject)
        {
            QualityTier outputQualityTier = getOutputQualityTierFromCost(dropperObject);

            PickupIndex dropPickupIndex = intendedDropPickupIndex;
            QualityTier dropQualityTier = QualityCatalog.GetQualityTier(dropPickupIndex);

            if (outputQualityTier > dropQualityTier)
            {
                dropPickupIndex = QualityCatalog.GetPickupIndexOfQuality(dropPickupIndex, outputQualityTier);
                dropQualityTier = outputQualityTier;
            }

            return dropPickupIndex;
        }

        static void OptionChestBehavior_ItemDrop(ILContext il)
        {
            ILCursor c = new ILCursor(il);

            if (!c.TryGotoNext(MoveType.Before,
                               x => x.MatchStfld<GenericPickupController.CreatePickupInfo>(nameof(GenericPickupController.CreatePickupInfo.pickerOptions))))
            {
                Log.Error("Failed to find patch location");
                return;
            }

            c.Emit(OpCodes.Ldarg_0);
            c.EmitDelegate<Func<PickupPickerController.Option[], OptionChestBehavior, PickupPickerController.Option[]>>(getPickerOptions);

            static PickupPickerController.Option[] getPickerOptions(PickupPickerController.Option[] options, OptionChestBehavior optionChestBehavior)
            {
                if (options != null && options.Length > 0 && optionChestBehavior)
                {
                    QualityTier outputQualityTier = getOutputQualityTierFromCost(optionChestBehavior.gameObject);
                    if (outputQualityTier > QualityTier.None)
                    {
                        for (int i = 0; i < options.Length; i++)
                        {
                            ref UniquePickup pickup = ref options[i].pickup;
                            if (pickup.isValid && QualityCatalog.GetQualityTier(pickup.pickupIndex) < outputQualityTier)
                            {
                                pickup = pickup.WithQualityTier(outputQualityTier);
                            }
                        }
                    }
                }

                return options;
            }
        }

        static void ShopTerminalBehavior_DropPickup(ILContext il)
        {
            ILCursor c = new ILCursor(il);

            if (!c.TryGotoNext(MoveType.After,
                               x => x.MatchLdfld<ShopTerminalBehavior>(nameof(ShopTerminalBehavior.pickup))))
            {
                Log.Error("Failed to find patch location");
                return;
            }

            c.Emit(OpCodes.Ldarg_0);
            c.EmitDelegate<Func<UniquePickup, ShopTerminalBehavior, UniquePickup>>(tryUpgradeQuality);

            static UniquePickup tryUpgradeQuality(UniquePickup pickup, ShopTerminalBehavior shopTerminalBehavior)
            {
                if (pickup.isValid)
                {
                    pickup = pickup.WithPickupIndex(tryUpgradeQualityFromCost(pickup.pickupIndex, shopTerminalBehavior ? shopTerminalBehavior.gameObject : null));
                }

                return pickup;
            }
        }

        static void ChestBehavior_BaseItemDrop(ILContext il)
        {
            ILCursor c = new ILCursor(il);

            if (!c.TryGotoNext(MoveType.Before,
                               x => x.MatchCallOrCallvirt<GenericPickupController.CreatePickupInfo>("set_" + nameof(GenericPickupController.CreatePickupInfo.pickup))))
            {
                Log.Error("Failed to find patch location");
                return;
            }

            c.Emit(OpCodes.Ldarg_0);
            c.EmitDelegate<Func<UniquePickup, ChestBehavior, UniquePickup>>(tryUpgradeQuality);

            static UniquePickup tryUpgradeQuality(UniquePickup pickup, ChestBehavior chestBehavior)
            {
                if (pickup.isValid)
                {
                    pickup = pickup.WithPickupIndex(tryUpgradeQualityFromCost(pickup.pickupIndex, chestBehavior ? chestBehavior.gameObject : null));
                }

                return pickup;
            }
        }
    }
}
