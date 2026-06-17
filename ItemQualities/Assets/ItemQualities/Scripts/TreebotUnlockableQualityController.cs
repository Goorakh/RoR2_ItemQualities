using HG;
using ItemQualities.Utilities;
using ItemQualities.Utilities.Extensions;
using RoR2;
using RoR2BepInExPack.GameAssetPathsBetter;
using UnityEngine;

namespace ItemQualities
{
    public sealed class TreebotUnlockableQualityController : MonoBehaviour
    {
        [SystemInitializer]
        private static void Init()
        {
            AddressableUtil.LoadAssetAsync<GameObject>(RoR2_Base_Treebot.TreebotUnlockInteractable_prefab).OnSuccess(treebotUnlockInteractablePrefab =>
            {
                treebotUnlockInteractablePrefab.EnsureComponent<TreebotUnlockableQualityController>();
            });

            On.RoR2.PurchaseInteraction.Start += PurchaseInteraction_Start;
        }

        private static void PurchaseInteraction_Start(On.RoR2.PurchaseInteraction.orig_Start orig, PurchaseInteraction self)
        {
            orig(self);
            if (self.name.StartsWith("TreebotUnlockInteractable"))
            {
                self.gameObject.EnsureComponent<TreebotUnlockableQualityController>();
            }
        }

        private PurchaseInteraction _purchaseInteraction;

        private void Awake()
        {
            _purchaseInteraction = GetComponent<PurchaseInteraction>();
            _purchaseInteraction.onDetailedPurchaseServer ??= new DetailedPurchaseEvent();
        }

        private void OnEnable()
        {
            _purchaseInteraction.onDetailedPurchaseServer.AddListener(onDetailedPurchaseServer);
        }

        private void OnDisable()
        {
            _purchaseInteraction.onDetailedPurchaseServer.RemoveListener(onDetailedPurchaseServer);
        }

        private static void onDetailedPurchaseServer(CostTypeDef.PayCostContext context, CostTypeDef.PayCostResults results)
        {
            if (!context.activatorBody)
                return;

            QualityTier highestBatteryQualityTier = QualityTier.None;

            foreach (EquipmentIndex equipmentIndex in results.equipmentTaken)
            {
                QualityTier qualityTier = QualityCatalog.GetQualityTier(equipmentIndex);
                EquipmentQualityGroupIndex equipmentGroupIndex = QualityCatalog.FindEquipmentQualityGroupIndex(equipmentIndex);

                if (equipmentGroupIndex == ItemQualitiesContent.EquipmentQualityGroups.QuestVolatileBattery.GroupIndex &&
                    qualityTier > highestBatteryQualityTier)
                {
                    highestBatteryQualityTier = qualityTier;
                }
            }

            if (highestBatteryQualityTier != QualityTier.None)
            {
                context.activatorInventory.GiveItemPermanent(ItemQualitiesContent.ItemQualityGroups.TreebotBuddy.GetItemIndex(highestBatteryQualityTier));
            }
        }
    }
}
