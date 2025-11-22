using HG;
using ItemQualities.Utilities.Extensions;
using RoR2;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace ItemQualities
{
    public sealed class ObjectPurchaseContext : MonoBehaviour
    {
        [SystemInitializer]
        static void Init()
        {
            On.RoR2.CostTypeDef.PayCost += CostTypeDef_PayCost;
            QualityDuplicatorBehavior.OnPickupsSelectedForPurchase += QualityDuplicatorBehavior_OnPickupsSelectedForPurchase;
        }

        static void CostTypeDef_PayCost(On.RoR2.CostTypeDef.orig_PayCost orig, CostTypeDef self, CostTypeDef.PayCostContext context, CostTypeDef.PayCostResults result)
        {
            orig(self, context, result);

            PurchaseResults purchaseResult = new PurchaseResults(result);

            ObjectPurchaseContext purchaseContext = context.purchasedObject.EnsureComponent<ObjectPurchaseContext>();
            purchaseContext.CostTypeIndex = (CostTypeIndex)Math.Max(0, Array.IndexOf(CostTypeCatalog.costTypeDefs, context.costTypeDef));
            purchaseContext.FirstInteractionResults ??= purchaseResult;
            purchaseContext.Results = purchaseResult;
        }

        static void QualityDuplicatorBehavior_OnPickupsSelectedForPurchase(QualityDuplicatorBehavior qualityDuplicatorBehavior, Interactor activator, IReadOnlyList<PickupIndex> pickupsSpent)
        {
            CostTypeDef.PayCostResults payCostResult = new CostTypeDef.PayCostResults();
            foreach (PickupIndex pickupIndex in pickupsSpent)
            {
                PickupDef pickupDef = PickupCatalog.GetPickupDef(pickupIndex);
                if (pickupDef != null)
                {
                    if (pickupDef.itemIndex != ItemIndex.None)
                    {
                        payCostResult.AddTakenItems(new Inventory.ItemAndStackValues
                        {
                            itemIndex = pickupDef.itemIndex,
                            stackValues = new Inventory.ItemStackValues { permanentStacks = 1 }
                        });
                    }
                    else if (pickupDef.equipmentIndex != EquipmentIndex.None)
                    {
                        payCostResult.equipmentTaken.Add(pickupDef.equipmentIndex);
                    }
                }
            }

            PurchaseResults result = new PurchaseResults(payCostResult);

            ObjectPurchaseContext purchaseContext = qualityDuplicatorBehavior.gameObject.EnsureComponent<ObjectPurchaseContext>();
            purchaseContext.CostTypeIndex = qualityDuplicatorBehavior.CostTypeIndex;
            purchaseContext.FirstInteractionResults ??= result;
            purchaseContext.Results = result;
        }

        public CostTypeIndex CostTypeIndex { get; private set; } = CostTypeIndex.None;

        public PurchaseResults FirstInteractionResults { get; private set; }

        public PurchaseResults Results { get; private set; }

        // Because CostTypeDef.PayCostResults is pooled, we cannot store an instance of it since it may be retreived and used for other purposes
        public sealed class PurchaseResults
        {
            public readonly Inventory.ItemAndStackValues[] ItemStacksTaken;

            public readonly EquipmentIndex[] EquipmentTaken;

            public readonly ItemQualityCounts UsedSaleStarCounts;

            public PurchaseResults(CostTypeDef.PayCostResults payCostResults)
            {
                ItemStacksTaken = payCostResults.itemStacksTaken ? payCostResults.itemStacksTaken.ToArray() : Array.Empty<Inventory.ItemAndStackValues>();
                EquipmentTaken = payCostResults.equipmentTaken?.ToArray() ?? Array.Empty<EquipmentIndex>();
                UsedSaleStarCounts = payCostResults.GetUsedSaleStars();
            }
        }
    }
}
