using ItemQualities.Utilities.Extensions;
using RoR2;
using UnityEngine;

namespace ItemQualities.Equipments
{
    static class Fruit
    {
        [SystemInitializer]
        static void Init()
        {
            On.RoR2.EquipmentSlot.FireFruit += EquipmentSlot_FireFruit;

            On.RoR2.CharacterBody.AddTimedBuff_BuffDef_float += CharacterBody_AddTimedBuff_BuffDef_float;
        }

        static void CharacterBody_AddTimedBuff_BuffDef_float(On.RoR2.CharacterBody.orig_AddTimedBuff_BuffDef_float orig, CharacterBody self, BuffDef buffDef, float duration)
        {
            if (buffDef && !buffDef.isHidden && !buffDef.isCooldown && !buffDef.isDebuff && !buffDef.isDOT)
            {
                EquipmentIndex currentEquipmentIndex = EquipmentIndex.None;
                QualityTier currentEquipmentQualityTier = QualityTier.None;
                if (self.inventory)
                {
                    currentEquipmentIndex = self.inventory.currentEquipmentIndex;
                    currentEquipmentQualityTier = self.inventory.GetActiveEquipmentQualityTier();
                }

                if (currentEquipmentIndex == RoR2Content.Equipment.Fruit.equipmentIndex &&
                    currentEquipmentQualityTier != QualityTier.None &&
                    self.HasBuff(ItemQualitiesContent.Buffs.SlugHealth))
                {
                    float durationMultiplier;
                    switch (currentEquipmentQualityTier)
                    {
                        case QualityTier.Uncommon:
                            durationMultiplier = 1.25f;
                            break;
                        case QualityTier.Rare:
                            durationMultiplier = 1.75f;
                            break;
                        case QualityTier.Epic:
                            durationMultiplier = 2.00f;
                            break;
                        case QualityTier.Legendary:
                            durationMultiplier = 3.00f;
                            break;
                        default:
                            Log.Warning($"Quality tier {currentEquipmentQualityTier} is not implemented");
                            durationMultiplier = 1f;
                            break;
                    }

                    if (durationMultiplier > 1f)
                    {
                        duration *= durationMultiplier;
                    }
                }
            }

            orig(self, buffDef, duration);
        }

        static bool EquipmentSlot_FireFruit(On.RoR2.EquipmentSlot.orig_FireFruit orig, EquipmentSlot self)
        {
            bool success = orig(self);
            if (success)
            {
                QualityTier qualityTier = self.GetCurrentEquipmentActionQualityTier();
                if (qualityTier != QualityTier.None)
                {
                    float temporaryHealthFraction;
                    switch (qualityTier)
                    {
                        case QualityTier.Uncommon:
                            temporaryHealthFraction = 0.05f;
                            break;
                        case QualityTier.Rare:
                            temporaryHealthFraction = 0.10f;
                            break;
                        case QualityTier.Epic:
                            temporaryHealthFraction = 0.20f;
                            break;
                        case QualityTier.Legendary:
                            temporaryHealthFraction = 0.25f;
                            break;
                        default:
                            Log.Warning($"Quality tier {qualityTier} is not implemented");
                            temporaryHealthFraction = 0f;
                            break;
                    }

                    if (temporaryHealthFraction > 0f)
                    {
                        int temporaryHealthAmount = Mathf.Max(1, (int)(self.healthComponent.fullHealth * temporaryHealthFraction));

                        for (int i = 0; i < temporaryHealthAmount; i++)
                        {
                            self.characterBody.AddBuff(ItemQualitiesContent.Buffs.SlugHealth);
                        }
                    }
                }
            }

            return success;
        }
    }
}
