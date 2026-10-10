using ItemQualities.Equipments;
using RoR2;
using System;

namespace ItemQualities.Utilities.Extensions
{
    public static class EquipmentExtensions
    {
        public static QualityTier GetCurrentEquipmentActionQualityTier(this EquipmentSlot equipmentSlot)
        {
            if (!equipmentSlot)
                throw new ArgumentNullException(nameof(equipmentSlot));

            if (!EquipmentHooks.TryGetCurrentEquipmentAction(equipmentSlot, out EquipmentHooks.EquipmentAction action))
                return QualityTier.None;

            return action.QualityTier;
        }

        public static QualityTier GetActiveEquipmentQualityTier(this EquipmentSlot equipmentSlot)
        {
            if (!equipmentSlot)
                throw new ArgumentNullException(nameof(equipmentSlot));

            if (!equipmentSlot.characterBody || !equipmentSlot.characterBody.inventory)
                return QualityTier.None;

            return equipmentSlot.characterBody.inventory.GetActiveEquipmentQualityTier();
        }

        public static QualityTier GetActiveEquipmentQualityTier(this Inventory inventory)
        {
            if (!inventory)
                throw new ArgumentNullException(nameof(inventory));

            return inventory.GetEquipmentQualityTier(inventory.activeEquipmentLocation);
        }

        [Obsolete("Use GetEquipmentQualityTier(Inventory, EquipmentLocation) instead")]
        public static QualityTier GetEquipmentQualityTier(this Inventory inventory, uint slot, uint set)
        {
            return GetEquipmentQualityTier(inventory, new EquipmentLocation { slot = slot, set = set });
        }

        public static QualityTier GetEquipmentQualityTier(this Inventory inventory, in EquipmentLocation location)
        {
            EquipmentState equipmentState = inventory.GetEquipment(location);
            return QualityCatalog.GetQualityTier(equipmentState.equipmentIndex);
        }

        public static EquipmentState WithQualityTier(this EquipmentState equipmentState, QualityTier qualityTier)
        {
            EquipmentIndex baseEquipmentIndex = QualityCatalog.GetEquipmentIndexOfQuality(equipmentState.equipmentIndex, qualityTier);
            if (baseEquipmentIndex != equipmentState.equipmentIndex)
            {
                equipmentState.equipmentIndex = baseEquipmentIndex;
                equipmentState.equipmentDef = EquipmentCatalog.GetEquipmentDef(equipmentState.equipmentIndex);
            }

            return equipmentState;
        }

        public static bool HasAnyQualityEquipment(this Inventory inventory, EquipmentQualityGroupIndex equipmentGroupIndex, QualityTier minQualityTier = QualityTier.Uncommon)
        {
            if (!inventory)
                throw new ArgumentNullException(nameof(inventory));

            int equipmentSlotCount = inventory.equipmentSlotCount;
            int equipmentSetCount = inventory.equipmentSetCount;
            for (uint slot = 0; slot < equipmentSlotCount; slot++)
            {
                for (uint set = 0; set < equipmentSetCount; set++)
                {
                    EquipmentLocation location = new EquipmentLocation { slot = slot, set = set };

                    EquipmentState equipmentState = inventory.GetEquipment(location);
                    if (QualityCatalog.FindEquipmentQualityGroupIndex(equipmentState.equipmentIndex) == equipmentGroupIndex &&
                        QualityCatalog.GetQualityTier(equipmentState.equipmentIndex) >= minQualityTier)
                    {
                        return true;
                    }
                }
            }

            return false;
        }
    }
}
