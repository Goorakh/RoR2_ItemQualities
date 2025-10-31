using HG;
using RoR2;
using System;
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

            ObjectPurchaseContext purchaseContext = purchasedObject.EnsureComponent<ObjectPurchaseContext>();
            purchaseContext.CostTypeIndex = (CostTypeIndex)Mathf.Max(0, Array.IndexOf(CostTypeCatalog.costTypeDefs, self));
            purchaseContext.FirstInteractionResults ??= payCostResults;
            purchaseContext.Results = payCostResults;

            return payCostResults;
        }

        public CostTypeIndex CostTypeIndex { get; private set; } = CostTypeIndex.None;

        public CostTypeDef.PayCostResults FirstInteractionResults { get; private set; }

        public CostTypeDef.PayCostResults Results { get; private set; }
    }
}
