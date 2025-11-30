using RoR2;
using UnityEngine;

namespace ItemQualities.Items
{
    public sealed class EquipmentMagazineQualityItemBehavior : MonoBehaviour
    {
        CharacterBody _body;

        void Awake()
        {
            _body = GetComponent<CharacterBody>();
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
            if (_body.equipmentSlot != equipmentSlot || equipmentIndex == EquipmentIndex.None)
                return;

            ItemQualityCounts equipmentMagazine = ItemQualitiesContent.ItemQualityGroups.EquipmentMagazine.GetItemCountsEffective(_body.inventory);

            float freeRestockChance = (10f * equipmentMagazine.UncommonCount) +
                                      (20f * equipmentMagazine.RareCount) +
                                      (35f * equipmentMagazine.EpicCount) +
                                      (60f * equipmentMagazine.LegendaryCount);

            if (Util.CheckRoll(Util.ConvertAmplificationPercentageIntoReductionPercentage(freeRestockChance), _body.master))
            {
                _body.inventory.RestockEquipmentCharges(equipmentSlot.activeEquipmentSlot, equipmentSlot.activeEquipmentSet[equipmentSlot.activeEquipmentSlot], 1);
            }
        }
    }
}
