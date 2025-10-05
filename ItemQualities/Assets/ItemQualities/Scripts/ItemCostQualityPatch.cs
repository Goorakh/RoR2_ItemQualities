using Mono.Cecil.Cil;
using MonoMod.Cil;
using RoR2;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace ItemQualities
{
    static class ItemCostQualityPatch
    {
        [SystemInitializer]
        static void Init()
        {
            IL.RoR2.ChestBehavior.BaseItemDrop += ChestBehavior_BaseItemDrop;
            IL.RoR2.ShopTerminalBehavior.DropPickup += ShopTerminalBehavior_DropPickup;
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
