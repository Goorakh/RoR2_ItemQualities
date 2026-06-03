using ItemQualities.Utilities;
using RoR2;

namespace ItemQualities.Items
{
    public sealed class DuplicatorQualityItemBehavior : QualityItemBodyBehavior
    {
        [ItemGroupAssociation(QualityItemBehaviorUsageFlags.Server)]
        private static ItemQualityGroup GetItemGroup()
        {
            return ItemQualitiesContent.ItemQualityGroups.Duplicator;
        }

        private QualityDuplicatorMinionInventoryController _minionInventoryController;

        private void OnEnable()
        {
            _minionInventoryController = QualityDuplicatorMinionInventoryController.EnsureMinionInventoryControllerServer(Body.master);
            PickupHooks.OnPickupGlobalServer += onPickupGlobalServer;
        }

        private void OnDisable()
        {
            PickupHooks.OnPickupGlobalServer -= onPickupGlobalServer;
            _minionInventoryController = null;
        }

        private void onPickupGlobalServer(in PickupDef.GrantContext context)
        {
            if (!ReferenceEquals(context.body, Body))
                return;

            // Ignore printed or non-permanent items
            if (context.controller.Duplicated || context.controller.pickup.isTempItem)
                return;

            PickupDef pickupDef = PickupCatalog.GetPickupDef(context.pickup.pickupIndex);
            if (pickupDef == null)
                return;

            ItemIndex sharedItemIndex = Duplicator.GetItemToShare(pickupDef.itemIndex);
            if (sharedItemIndex == ItemIndex.None)
                return;

            ref readonly ItemQualityCounts stacks = ref Stacks;

            float itemShareChance = (stacks.UncommonCount * 10f) +
                                    (stacks.RareCount * 25f) +
                                    (stacks.EpicCount * 50f) +
                                    (stacks.LegendaryCount * 75f);

            int itemShareStackCount = RollUtil.GetOverflowRoll(itemShareChance, Body.master, false);
            if (itemShareStackCount > 0)
            {
                _minionInventoryController.GiveItemToMinionsServer(context.position, sharedItemIndex, itemShareStackCount);
            }
        }
    }
}
