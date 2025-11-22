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

            ILLabel skipItemLabel = default;
            if (!c.TryFindNext(out ILCursor[] foundCursors,
                               x => x.MatchLdfld<CostTypeDef.PayCostContext>(nameof(CostTypeDef.PayCostContext.avoidedItemIndex)),
                               x => x.MatchBeq(out skipItemLabel)))
            {
                Log.Error("Failed to find patch location");
                return;
            }

            c.Goto(foundCursors[1].Next, MoveType.After); // beq

            if (!c.TryFindForeachVariable(out VariableDefinition itemIndexVar))
            {
                Log.Error("Failed to find ItemIndex variable");
                return;
            }

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

        static QualityTier getOutputQualityTierFromCost(GameObject dropperObject)
        {
            if (!dropperObject ||
                !dropperObject.TryGetComponent(out ObjectPurchaseContext purchaseContext) ||
                purchaseContext.Results == null)
            {
                return QualityTier.None;
            }

            ObjectPurchaseContext.PurchaseResults payCostResults = purchaseContext.Results;
            List<UniquePickup> pickupsSpentOnPurchase = new List<UniquePickup>(payCostResults.ItemStacksTaken.Length + payCostResults.EquipmentTaken.Length);

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

            if (pickupsSpentOnPurchase.Count == 0)
                return QualityTier.None;

            float averageInputQualityTierValue = 0f;
            int numInputItemsWithQuality = 0;

            foreach (UniquePickup inputPickup in pickupsSpentOnPurchase)
            {
                QualityTier qualityTier = QualityCatalog.GetQualityTier(inputPickup.pickupIndex);

                averageInputQualityTierValue += (float)qualityTier;

                if (qualityTier > QualityTier.None)
                {
                    numInputItemsWithQuality++;
                }
            }

            if (numInputItemsWithQuality == 0)
                return QualityTier.None;

            averageInputQualityTierValue /= pickupsSpentOnPurchase.Count;
            QualityTier outputQualityTier = (QualityTier)Mathf.Clamp(Mathf.CeilToInt(averageInputQualityTierValue), 0, (int)QualityTier.Count - 1);

            Log.Debug($"{numInputItemsWithQuality}/{pickupsSpentOnPurchase.Count} input items of quality (avg={outputQualityTier})");

            return outputQualityTier;
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
