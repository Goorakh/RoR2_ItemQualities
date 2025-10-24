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
            CostTypeDef whiteItemCostDef = CostTypeCatalog.GetCostTypeDef(CostTypeIndex.WhiteItem);
            if (whiteItemCostDef?.isAffordable?.Method != null)
            {
                new Hook(whiteItemCostDef.isAffordable.Method, new Func<CostTypeCatalog_IsAffordableItem_orig, CostTypeDef, CostTypeDef.IsAffordableContext, bool>(CostTypeCatalog_IsAffordableItem));
            }
            else
            {
                Log.Error("Failed to find IsAffordableItem method");
            }

            if (whiteItemCostDef?.payCost?.Method != null)
            {
                new ILHook(whiteItemCostDef.payCost.Method, CostTypeCatalog_PayCostItems);
            }
            else
            {
                Log.Error("Failed to find PayCostItems method");
            }

            IL.RoR2.ChestBehavior.BaseItemDrop += ChestBehavior_BaseItemDrop;
            IL.RoR2.ShopTerminalBehavior.DropPickup += ShopTerminalBehavior_DropPickup;
        }

        delegate bool CostTypeCatalog_IsAffordableItem_orig(CostTypeDef costTypeDef, CostTypeDef.IsAffordableContext context);
        static bool CostTypeCatalog_IsAffordableItem(CostTypeCatalog_IsAffordableItem_orig orig, CostTypeDef costTypeDef, CostTypeDef.IsAffordableContext context)
        {
            if (!orig(costTypeDef, context))
                return false;

            bool hasEnoughNonQualityItems = false;
            if (context.activator && context.activator.TryGetComponent(out CharacterBody activatorBody) && activatorBody.inventory)
            {
                int foundItems = 0;
                foreach (ItemDef itemDef in ItemCatalog.allItemDefs)
                {
                    if (itemDef && itemDef.tier == costTypeDef.itemTier && QualityCatalog.GetQualityTier(itemDef.itemIndex) == QualityTier.None)
                    {
                        foundItems += activatorBody.inventory.GetItemCount(itemDef);
                        if (foundItems >= context.cost)
                        {
                            hasEnoughNonQualityItems = true;
                            break;
                        }
                    }
                }
            }

            return hasEnoughNonQualityItems;
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
            c.EmitDelegate<Func<ItemIndex, bool>>(isQualityItem);
            c.Emit(OpCodes.Brtrue, skipItemLabel);

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            static bool isQualityItem(ItemIndex itemIndex)
            {
                return QualityCatalog.GetQualityTier(itemIndex) > QualityTier.None;
            }
        }

        static PickupIndex tryUpgradeQualityFromCost(PickupIndex intendedDropPickupIndex, GameObject dropperObject, Xoroshiro128Plus rng)
        {
            if (!dropperObject ||
                !dropperObject.TryGetComponent(out ObjectPurchaseContext purchaseContext) ||
                purchaseContext.Results == null)
            {
                return intendedDropPickupIndex;
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
                return intendedDropPickupIndex;

            float averageInputQualityTierValue = 0f;
            int numInputItemsWithQuality = 0;

            QualityTier minInputQualityTier = QualityTier.Count;

            foreach (PickupIndex inputPickupIndex in pickupIndicesSpentOnPurchase)
            {
                QualityTier qualityTier = QualityCatalog.GetQualityTier(inputPickupIndex);

                averageInputQualityTierValue += (float)qualityTier;

                if (qualityTier > QualityTier.None)
                {
                    numInputItemsWithQuality++;
                }

                if (qualityTier < minInputQualityTier)
                {
                    minInputQualityTier = qualityTier;
                }
            }

            averageInputQualityTierValue /= pickupIndicesSpentOnPurchase.Count;
            QualityTier outputQualityTier = (QualityTier)Mathf.Clamp(Mathf.CeilToInt(averageInputQualityTierValue), 0, (int)QualityTier.Count - 1);

            Log.Debug($"{numInputItemsWithQuality}/{pickupIndicesSpentOnPurchase.Count} input items of quality (avg={outputQualityTier})");

            PickupIndex dropPickupIndex = intendedDropPickupIndex;
            QualityTier dropQualityTier = QualityCatalog.GetQualityTier(dropPickupIndex);

            if (outputQualityTier > dropQualityTier)
            {
                bool shouldUpgradeQualityTier = true;
                if (numInputItemsWithQuality < pickupIndicesSpentOnPurchase.Count)
                {
                    // If at least 1 regular item in the input, reduce chance of upgrading the quality of the output
                    float inputItemsWithQualityFraction = (float)numInputItemsWithQuality / pickupIndicesSpentOnPurchase.Count;
                    shouldUpgradeQualityTier = rng.nextNormalizedFloat < Mathf.Pow(inputItemsWithQualityFraction, 3f);
                }

                if (shouldUpgradeQualityTier)
                {
                    dropPickupIndex = QualityCatalog.GetPickupIndexOfQuality(dropPickupIndex, outputQualityTier);
                    dropQualityTier = outputQualityTier;
                }
            }

            return dropPickupIndex;
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
                return tryUpgradeQualityFromCost(pickupIndex, shopTerminalBehavior ? shopTerminalBehavior.gameObject : null, shopTerminalBehavior ? shopTerminalBehavior.rng : RoR2Application.rng);
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
                    return tryUpgradeQualityFromCost(pickupIndex, chestBehavior ? chestBehavior.gameObject : null, chestBehavior ? chestBehavior.rng : RoR2Application.rng);
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
