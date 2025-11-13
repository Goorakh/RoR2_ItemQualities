using HG;
using RoR2;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace ItemQualities
{
    public class ObjectPurchaseContext : MonoBehaviour
    {
        [SystemInitializer]
        static void Init()
        {
            On.RoR2.CostTypeDef.PayCost += CostTypeDef_PayCost;
            QualityDuplicatorBehavior.OnPickupsSelectedForPurchase += QualityDuplicatorBehavior_OnPickupsSelectedForPurchase;
        }

        static CostTypeDef.PayCostResults CostTypeDef_PayCost(On.RoR2.CostTypeDef.orig_PayCost orig, CostTypeDef self, int cost, Interactor activator, GameObject purchasedObject, Xoroshiro128Plus rng, ItemIndex avoidedItemIndex)
        {
            CostTypeDef.PayCostResults payCostResults = orig(self, cost, activator, purchasedObject, rng, avoidedItemIndex);

            ObjectPurchaseContext purchaseContext = purchasedObject.EnsureComponent<ObjectPurchaseContext>();
            purchaseContext.CostTypeIndex = (CostTypeIndex)Mathf.Max(0, Array.IndexOf(CostTypeCatalog.costTypeDefs, self));
            purchaseContext.FirstInteractionResults ??= payCostResults;
            purchaseContext.Results = payCostResults;

            return payCostResults;
        }

        static void QualityDuplicatorBehavior_OnPickupsSelectedForPurchase(QualityDuplicatorBehavior qualityDuplicatorBehavior, Interactor activator, IReadOnlyList<PickupIndex> pickupsSpent)
        {
            CostTypeDef.PayCostResults results = new CostTypeDef.PayCostResults();
            foreach (PickupIndex pickupIndex in pickupsSpent)
            {
                PickupDef pickupDef = PickupCatalog.GetPickupDef(pickupIndex);
                if (pickupDef != null)
                {
                    if (pickupDef.itemIndex != ItemIndex.None)
                    {
                        results.itemsTaken.Add(pickupDef.itemIndex);
                    }
                    else if (pickupDef.equipmentIndex != EquipmentIndex.None)
                    {
                        results.equipmentTaken.Add(pickupDef.equipmentIndex);
                    }
                }
            }

            ObjectPurchaseContext purchaseContext = qualityDuplicatorBehavior.gameObject.EnsureComponent<ObjectPurchaseContext>();
            purchaseContext.CostTypeIndex = qualityDuplicatorBehavior.CostTypeIndex;
            purchaseContext.FirstInteractionResults ??= results;
            purchaseContext.Results = results;
        }

        public CostTypeIndex CostTypeIndex { get; private set; } = CostTypeIndex.None;

        public CostTypeDef.PayCostResults FirstInteractionResults { get; private set; }

        public CostTypeDef.PayCostResults Results { get; private set; }
    }
}
