using HG;
using RoR2;
using UnityEngine;

namespace ItemQualities
{
    public sealed class TreebotUnlockableQualityController : MonoBehaviour
    {
        private static int _treebotUnlockInteractableIndex = -1;

        [SystemInitializer(typeof(InteractableCatalog), typeof(MasterCatalog), typeof(BodyCatalog))]
        private static void Init()
        {
            _treebotUnlockInteractableIndex = InteractableCatalog.FindInteractableIndex("TreebotUnlockInteractable");
            if (_treebotUnlockInteractableIndex == -1)
            {
                Log.Error("Failed to find treebot unlockable interactabel index");
            }

            InteractableInfoProvider.OnCatalogedInteractableStartGlobal += onCatalogedInteractableStartGlobal;
        }

        private static void onCatalogedInteractableStartGlobal(InteractableInfoProvider interactableInfo)
        {
            if (interactableInfo.CatalogIndex == _treebotUnlockInteractableIndex)
            {
                interactableInfo.gameObject.EnsureComponent<TreebotUnlockableQualityController>();
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
