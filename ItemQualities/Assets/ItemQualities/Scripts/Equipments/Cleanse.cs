using ItemQualities.Utilities.Extensions;
using RoR2;
using System;
using UnityEngine;

namespace ItemQualities.Equipments
{
    internal static class Cleanse
    {
        [SystemInitializer(typeof(BuffCatalog))]
        private static void Init()
        {
            On.RoR2.EquipmentSlot.FireCleanse += EquipmentSlot_FireCleanse;

            BuffDef voidFogStackCooldown = BuffCatalog.GetBuffDef(BuffCatalog.FindBuffIndex("bdVoidFogStackCooldown"));
            if (voidFogStackCooldown)
            {
                // Prevent blast shower from cleansing hidden cooldown marker for ramping damage
                voidFogStackCooldown.isCooldown = false;
            }
            else
            {
                Log.Warning("Failed to find bdVoidFogStackCooldown");
            }
        }

        private static bool EquipmentSlot_FireCleanse(On.RoR2.EquipmentSlot.orig_FireCleanse orig, EquipmentSlot self)
        {
            bool result = orig(self);

            try
            {
                if (self && self.characterBody)
                {
                    QualityTier qualityTier = self.GetCurrentEquipmentActionQualityTier();
                    float extraCleanseDuration = 0f;
                    switch (qualityTier)
                    {
                        case QualityTier.None:
                            extraCleanseDuration = 0;
                            break;
                        case QualityTier.Uncommon:
                            extraCleanseDuration = 2f;
                            break;
                        case QualityTier.Rare:
                            extraCleanseDuration = 3f;
                            break;
                        case QualityTier.Epic:
                            extraCleanseDuration = 5f;
                            break;
                        case QualityTier.Legendary:
                            extraCleanseDuration = 6f;
                            break;
                        default:
                            extraCleanseDuration = 0;
                            Log.Error($"Quality tier {qualityTier} is not implemented");
                            break;
                    }

                    if (extraCleanseDuration > 0)
                    {
                        RepeatCleanseAttachment repeatCleanse = RepeatCleanseAttachment.FindCleanseAttachmentForBody(self.characterBody);
                        if (!repeatCleanse)
                        {
                            GameObject cleanseAttachmentObj = GameObject.Instantiate(ItemQualitiesContent.NetworkedPrefabs.CleanseQualityAttachment);

                            NetworkedBodyAttachment bodyAttachment = cleanseAttachmentObj.GetComponent<NetworkedBodyAttachment>();
                            bodyAttachment.AttachToGameObjectAndSpawn(self.characterBody.gameObject);

                            repeatCleanse = cleanseAttachmentObj.GetComponent<RepeatCleanseAttachment>();
                            repeatCleanse.CleansesRemaining = 0;
                        }

                        repeatCleanse.CleansesRemaining += Mathf.CeilToInt(extraCleanseDuration / repeatCleanse.CleanseInterval);

                        result = true;
                    }
                }
            }
            catch (Exception e)
            {
                Log.Error_NoCallerPrefix(e.ToString());
            }

            return result;
        }
    }
}
