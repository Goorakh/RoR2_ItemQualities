using HG;
using RoR2;
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
            purchaseContext.FirstInteractionResults ??= payCostResults;
            purchaseContext.Results = payCostResults;

            return payCostResults;
        }

        public CostTypeDef.PayCostResults FirstInteractionResults { get; private set; }

        public CostTypeDef.PayCostResults Results { get; private set; }
    }
}
