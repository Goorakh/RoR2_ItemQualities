using Mono.Cecil.Cil;
using MonoMod.Cil;
using RoR2;
using System;
using UnityEngine;
using static ItemQualities.ItemQualitiesContent;

namespace ItemQualities
{
    static class ItemCostQualityPatch
    {
        [SystemInitializer]
        static void Init()
        {
            IL.RoR2.ShopTerminalBehavior.DropPickup += CopyItemQualityFromCost;
        }

        static void CopyItemQualityFromCost(ILContext il)
        {
            ILCursor c = new ILCursor(il);

            if (!c.TryGotoNext(MoveType.After,
                               x => x.MatchLdfld<ShopTerminalBehavior>(nameof(ShopTerminalBehavior.pickupIndex))))
            {
                Log.Error("Failed to find patch location");
                return;
            }

            c.Emit(OpCodes.Ldarg_0);
            c.EmitDelegate<Func<PickupIndex, ShopTerminalBehavior, PickupIndex>>(getAppropriateQualityDrop);

            static PickupIndex getAppropriateQualityDrop(PickupIndex pickupIndex, ShopTerminalBehavior shopTerminalBehavior)
            {
                if (!shopTerminalBehavior.TryGetComponent(out ObjectPurchaseContext purchaseContext) || purchaseContext.PickupIndicesSpentOnLastPurchase.Length == 0)
                    return pickupIndex;

                float averageInputQualityTierValue = 0f;
                int numInputItemsWithQuality = 0;

                QualityTier minInputQualityTier = QualityTier.Count;

                foreach (PickupIndex inputPickupIndex in purchaseContext.PickupIndicesSpentOnLastPurchase)
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

                averageInputQualityTierValue /= purchaseContext.PickupIndicesSpentOnLastPurchase.Length;
                QualityTier outputQualityTier = (QualityTier)Mathf.Clamp(Mathf.CeilToInt(averageInputQualityTierValue), 0, (int)QualityTier.Count - 1);

                Log.Debug($"{numInputItemsWithQuality}/{purchaseContext.PickupIndicesSpentOnLastPurchase.Length} input items of quality (avg={outputQualityTier})");

                if (outputQualityTier > QualityTier.None)
                {
                    bool shouldUpgradeQualityTier = true;
                    if (numInputItemsWithQuality < purchaseContext.PickupIndicesSpentOnLastPurchase.Length)
                    {
                        // If at least 1 regular item in the input, reduce chance of upgrading the quality of the output
                        float inputItemsWithQualityFraction = (float)numInputItemsWithQuality / purchaseContext.PickupIndicesSpentOnLastPurchase.Length;
                        shouldUpgradeQualityTier = shopTerminalBehavior.rng.nextNormalizedFloat < Mathf.Pow(inputItemsWithQualityFraction, 3f);
                    }

                    if (shouldUpgradeQualityTier)
                    {
                        PickupIndex qualityPickupIndex = QualityCatalog.GetPickupIndexOfQuality(pickupIndex, outputQualityTier);

                        if (qualityPickupIndex != PickupIndex.none && qualityPickupIndex != pickupIndex)
                        {
                            Log.Debug($"Upgraded tier of {pickupIndex}: {qualityPickupIndex}");
                            pickupIndex = qualityPickupIndex;
                        }
                        else
                        {
                            Log.Warning($"Pickup {pickupIndex} is missing quality variant {outputQualityTier}");
                        }
                    }
                }

                return pickupIndex;
            }
        }
    }
}
