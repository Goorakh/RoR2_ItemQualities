using HG;
using ItemQualities.Utilities.Extensions;
using RoR2;
using System;
using UnityEngine;

namespace ItemQualities
{
    public sealed class ObjectPurchaseContext : MonoBehaviour
    {
        [SystemInitializer]
        private static void Init()
        {
            On.RoR2.CostTypeDef.PayCost_PayCostContext_PayCostResults += CostTypeDef_PayCost_PayCostContext_PayCostResults;
        }

        private static void CostTypeDef_PayCost_PayCostContext_PayCostResults(On.RoR2.CostTypeDef.orig_PayCost_PayCostContext_PayCostResults orig, CostTypeDef self, CostTypeDef.PayCostContext context, CostTypeDef.PayCostResults result)
        {
            orig(self, context, result);

            PurchaseResults purchaseResult = new PurchaseResults(result);

            ObjectPurchaseContext purchaseContext = context.purchasedObject.EnsureComponent<ObjectPurchaseContext>();
            purchaseContext.CostTypeIndex = self.costTypeIndex;
            purchaseContext.FirstInteractionResults ??= purchaseResult;
            purchaseContext.Results = purchaseResult;
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
