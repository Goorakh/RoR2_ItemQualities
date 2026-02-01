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

            byte slot = inventory.activeEquipmentSlot;
            if (slot >= inventory.activeEquipmentSet.Length)
                return QualityTier.None;

            byte set = inventory.activeEquipmentSet[slot];

            return inventory.GetEquipmentQualityTier(slot, set);
        }

        public static QualityTier GetEquipmentQualityTier(this Inventory inventory, uint slot, uint set)
        {
            EquipmentState equipmentState = inventory.GetEquipment(slot, set);
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
    }
}
