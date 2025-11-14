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
            IL.RoR2.ShopTerminalBehavior.DropPickup += ShopTerminalBehavior_DropPickup;
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

            CostTypeDef.PayCostResults payCostResults = purchaseContext.Results;
            List<PickupIndex> pickupIndicesSpentOnPurchase = new List<PickupIndex>(payCostResults.itemsTaken.Count + payCostResults.equipmentTaken.Count);

            foreach (ItemIndex itemIndex in payCostResults.itemsTaken)
            {
                if (itemIndex != ItemIndex.None)
                {
                    pickupIndicesSpentOnPurchase.Add(PickupCatalog.FindPickupIndex(itemIndex));
                }
            }

            foreach (EquipmentIndex equipmentIndex in payCostResults.equipmentTaken)
            {
                if (equipmentIndex != EquipmentIndex.None)
                {
                    pickupIndicesSpentOnPurchase.Add(PickupCatalog.FindPickupIndex(equipmentIndex));
                }
            }

            if (pickupIndicesSpentOnPurchase.Count == 0)
                return QualityTier.None;

            float averageInputQualityTierValue = 0f;
            int numInputItemsWithQuality = 0;

            foreach (PickupIndex inputPickupIndex in pickupIndicesSpentOnPurchase)
            {
                QualityTier qualityTier = QualityCatalog.GetQualityTier(inputPickupIndex);

                averageInputQualityTierValue += (float)qualityTier;

                if (qualityTier > QualityTier.None)
                {
                    numInputItemsWithQuality++;
                }
            }

            if (numInputItemsWithQuality == 0)
                return QualityTier.None;

            averageInputQualityTierValue /= pickupIndicesSpentOnPurchase.Count;
            QualityTier outputQualityTier = (QualityTier)Mathf.Clamp(Mathf.CeilToInt(averageInputQualityTierValue), 0, (int)QualityTier.Count - 1);

            Log.Debug($"{numInputItemsWithQuality}/{pickupIndicesSpentOnPurchase.Count} input items of quality (avg={outputQualityTier})");

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
                        Xoroshiro128Plus rng = new Xoroshiro128Plus((optionChestBehavior.rng ?? RoR2Application.rng).nextUlong);

                        int[] upgradeOptionIndicesPriority = new int[options.Length];
                        for (int i = 0; i < upgradeOptionIndicesPriority.Length; i++)
                        {
                            upgradeOptionIndicesPriority[i] = i;
                        }

                        Util.ShuffleArray(upgradeOptionIndicesPriority, rng);

                        int maxOptionsToUpgrade = 1;
                        int numOptionsUpgraded = 0;

                        foreach (int i in upgradeOptionIndicesPriority)
                        {
                            ref PickupIndex optionPickupIndex = ref options[i].pickupIndex;
                            QualityTier optionQualityTier = QualityCatalog.GetQualityTier(optionPickupIndex);

                            PickupIndex dropPickupIndex = QualityCatalog.GetPickupIndexOfQuality(optionPickupIndex, outputQualityTier);

                            // If the pickup of the output quality does not exist, we should just move on and try the next one and not "consume" a quality upgrade
                            if (dropPickupIndex != optionPickupIndex)
                            {
                                if (outputQualityTier > optionQualityTier)
                                {
                                    optionPickupIndex = dropPickupIndex;
                                }

                                if (++numOptionsUpgraded >= maxOptionsToUpgrade)
                                {
                                    break;
                                }
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
                               x => x.MatchLdfld<ShopTerminalBehavior>(nameof(ShopTerminalBehavior.pickupIndex))))
            {
                Log.Error("Failed to find patch location");
                return;
            }

            c.Emit(OpCodes.Ldarg_0);
            c.EmitDelegate<Func<PickupIndex, ShopTerminalBehavior, PickupIndex>>(tryUpgradeQuality);

            static PickupIndex tryUpgradeQuality(PickupIndex pickupIndex, ShopTerminalBehavior shopTerminalBehavior)
            {
                return tryUpgradeQualityFromCost(pickupIndex, shopTerminalBehavior ? shopTerminalBehavior.gameObject : null);
            }
        }

        static void ChestBehavior_BaseItemDrop(ILContext il)
        {
            ILCursor c = new ILCursor(il);

            int patchCount = 0;

            while (c.TryGotoNext(MoveType.After,
                                 x => x.MatchCallOrCallvirt<ChestBehavior>("get_"+nameof(ChestBehavior.dropPickup))))
            {
                c.Emit(OpCodes.Ldarg_0);
                c.EmitDelegate<Func<PickupIndex, ChestBehavior, PickupIndex>>(tryUpgradeQuality);

                static PickupIndex tryUpgradeQuality(PickupIndex pickupIndex, ChestBehavior chestBehavior)
                {
                    return tryUpgradeQualityFromCost(pickupIndex, chestBehavior ? chestBehavior.gameObject : null);
                }

                patchCount++;
            }

            if (patchCount == 0)
            {
                Log.Error("Failed to find patch location");
            }
            else
            {
                Log.Debug($"Found {patchCount} patch location(s)");
            }
        }
    }
}
