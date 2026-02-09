using ItemQualities.Utilities.Extensions;
using R2API;
using RoR2;

namespace ItemQualities.Equipments
{
    static class GainArmor
    {
        [SystemInitializer]
        static void Init()
        {
            RecalculateStatsAPI.GetStatCoefficients += getStatCoefficients;

            On.RoR2.EquipmentSlot.FireGainArmor += EquipmentSlot_FireGainArmor;
        }

        static void getStatCoefficients(CharacterBody sender, RecalculateStatsAPI.StatHookEventArgs args)
        {
            if (!sender.inventory)
                return;

            if (sender.healthComponent.barrier > 0f)
            {
                int equipmentSlotCount = sender.inventory.GetEquipmentSlotCount();
                for (uint slot = 0; slot < equipmentSlotCount; slot++)
                {
                    int equipmentSetCount = sender.inventory.GetEquipmentSetCount(slot);
                    for (uint set = 0; set < equipmentSetCount; set++)
                    {
                        EquipmentIndex equipmentIndex = sender.inventory.GetEquipment(slot, set).equipmentIndex;
                        EquipmentQualityGroupIndex equipmentGroupIndex = QualityCatalog.FindEquipmentQualityGroupIndex(equipmentIndex);
                        if (equipmentIndex != EquipmentIndex.None && equipmentGroupIndex == ItemQualitiesContent.EquipmentQualityGroups.GainArmor.GroupIndex)
                        {
                            QualityTier qualityTier = sender.inventory.GetEquipmentQualityTier(slot, set);
                            switch (qualityTier)
                            {
                                case QualityTier.None:
                                    break;
                                case QualityTier.Uncommon:
                                    args.armorAdd += 30f;
                                    break;
                                case QualityTier.Rare:
                                    args.armorAdd += 60f;
                                    break;
                                case QualityTier.Epic:
                                    args.armorAdd += 100f;
                                    break;
                                case QualityTier.Legendary:
                                    args.armorAdd += 150f;
                                    break;
                                default:
                                    Log.Warning($"Quality tier {qualityTier} is not implemented");
                                    break;
                            }
                        }
                    }
                }
            }
        }

        static bool EquipmentSlot_FireGainArmor(On.RoR2.EquipmentSlot.orig_FireGainArmor orig, EquipmentSlot self)
        {
            bool success = orig(self);

            if (success)
            {
                self.characterBody.healthComponent.AddBarrier(self.characterBody.healthComponent.fullHealth * 0.2f);
            }

            return success;
        }
    }
}
