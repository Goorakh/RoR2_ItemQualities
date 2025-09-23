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
        }

        static CostTypeDef.PayCostResults CostTypeDef_PayCost(On.RoR2.CostTypeDef.orig_PayCost orig, CostTypeDef self, int cost, Interactor activator, GameObject purchasedObject, Xoroshiro128Plus rng, ItemIndex avoidedItemIndex)
        {
            CostTypeDef.PayCostResults payCostResults = orig(self, cost, activator, purchasedObject, rng, avoidedItemIndex);

            List<PickupIndex> pickupIndicesSpentOnPurchase = new List<PickupIndex>(payCostResults.itemsTaken.Count + payCostResults.equipmentTaken.Count);

            foreach (ItemIndex itemIndex in payCostResults.itemsTaken)
            {
                pickupIndicesSpentOnPurchase.Add(PickupCatalog.FindPickupIndex(itemIndex));
            }

            foreach (EquipmentIndex equipmentIndex in payCostResults.equipmentTaken)
            {
                pickupIndicesSpentOnPurchase.Add(PickupCatalog.FindPickupIndex(equipmentIndex));
            }

            ObjectPurchaseContext purchaseContext = purchasedObject.EnsureComponent<ObjectPurchaseContext>();
            purchaseContext.PickupIndicesSpentOnLastPurchase = pickupIndicesSpentOnPurchase.ToArray();

            return payCostResults;
        }

        public PickupIndex[] PickupIndicesSpentOnLastPurchase = Array.Empty<PickupIndex>();
    }
}
