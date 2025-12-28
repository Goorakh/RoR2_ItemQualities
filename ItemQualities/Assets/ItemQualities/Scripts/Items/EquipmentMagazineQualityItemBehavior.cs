using ItemQualities.Utilities;
using RoR2;

namespace ItemQualities.Items
{
    public sealed class EquipmentMagazineQualityItemBehavior : QualityItemBodyBehavior
    {
        [ItemGroupAssociation(QualityItemBehaviorUsageFlags.Server)]
        static ItemQualityGroup GetItemGroup()
        {
            return ItemQualitiesContent.ItemQualityGroups.EquipmentMagazine;
        }

        void OnEnable()
        {
            EquipmentSlot.onServerEquipmentActivated += onEquipmentActivated;
        }

        void OnDisable()
        {
            EquipmentSlot.onServerEquipmentActivated -= onEquipmentActivated;
        }

        void onEquipmentActivated(EquipmentSlot equipmentSlot, EquipmentIndex equipmentIndex)
        {
            if (Body.equipmentSlot != equipmentSlot || equipmentIndex == EquipmentIndex.None)
                return;

            ItemQualityCounts equipmentMagazine = Stacks;

            float freeRestockChance = (10f * equipmentMagazine.UncommonCount) +
                                      (20f * equipmentMagazine.RareCount) +
                                      (35f * equipmentMagazine.EpicCount) +
                                      (60f * equipmentMagazine.LegendaryCount);

            if (RollUtil.CheckRoll(Util.ConvertAmplificationPercentageIntoReductionPercentage(freeRestockChance), Body.master, false))
            {
                Body.inventory.RestockEquipmentCharges(equipmentSlot.activeEquipmentSlot, equipmentSlot.activeEquipmentSet[equipmentSlot.activeEquipmentSlot], 1);
            }
        }
    }
}
