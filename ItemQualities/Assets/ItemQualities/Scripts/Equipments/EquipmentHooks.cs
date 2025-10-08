using RoR2;

namespace ItemQualities.Equipments
{
    static class EquipmentHooks
    {
        public static QualityTier CurrentEquipmentQualityTier { get; set; } = QualityTier.None;

        [SystemInitializer]
        static void Init()
        {
            On.RoR2.EquipmentSlot.PerformEquipmentAction += EquipmentSlot_PerformEquipmentAction;
        }

        static bool EquipmentSlot_PerformEquipmentAction(On.RoR2.EquipmentSlot.orig_PerformEquipmentAction orig, EquipmentSlot self, EquipmentDef equipmentDef)
        {
            QualityTier equipmentQualityTier = QualityTier.None;
            if (equipmentDef)
            {
                equipmentQualityTier = QualityCatalog.GetQualityTier(equipmentDef.equipmentIndex);

                EquipmentIndex baseQualityEquipmentIndex = QualityCatalog.GetEquipmentIndexOfQuality(equipmentDef.equipmentIndex, QualityTier.None);
                if (baseQualityEquipmentIndex != EquipmentIndex.None && equipmentDef.equipmentIndex != baseQualityEquipmentIndex)
                {
                    equipmentDef = EquipmentCatalog.GetEquipmentDef(baseQualityEquipmentIndex);
                }
            }

            CurrentEquipmentQualityTier = equipmentQualityTier;

            try
            {
                return orig(self, equipmentDef);
            }
            finally
            {
                CurrentEquipmentQualityTier = QualityTier.None;
            }
        }
    }
}
