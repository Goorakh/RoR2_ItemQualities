using ItemQualities.Utilities;
using RoR2;

namespace ItemQualities.Items
{
    public sealed class EquipmentMagazineQualityItemBehavior : QualityItemBodyBehavior
    {
        [ItemGroupAssociation(QualityItemBehaviorUsageFlags.Server)]
        private static ItemQualityGroup GetItemGroup()
        {
            return ItemQualitiesContent.ItemQualityGroups.EquipmentMagazine;
        }

        private void OnEnable()
        {
            EquipmentSlot.onServerEquipmentActivatedDetailed += onEquipmentActivated;
        }

        private void OnDisable()
        {
            EquipmentSlot.onServerEquipmentActivatedDetailed -= onEquipmentActivated;
        }

        private void onEquipmentActivated(EquipmentSlot equipmentSlot, EquipmentIndex equipmentIndex, in EquipmentLocation location)
        {
            if (Body.equipmentSlot != equipmentSlot || equipmentIndex == EquipmentIndex.None)
                return;

            ref readonly ItemQualityCounts equipmentMagazine = ref Stacks;

            float freeRestockChance = (10f * equipmentMagazine.UncommonCount) +
                                      (20f * equipmentMagazine.RareCount) +
                                      (35f * equipmentMagazine.EpicCount) +
                                      (60f * equipmentMagazine.LegendaryCount);

            if (RollUtil.CheckRoll(Util.ConvertAmplificationPercentageIntoReductionPercentage(freeRestockChance), Body.master, false))
            {
                Body.inventory.RestockEquipmentCharges(location, 1);
            }
        }
    }
}
