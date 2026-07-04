using ItemQualities.Utilities.Extensions;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using RoR2;
using System;
using UnityEngine;

namespace ItemQualities.Equipments
{
    static class BossHunter
    {
        [SystemInitializer]
        static void Init()
        {
            IL.RoR2.EquipmentSlot.FireBossHunter += EquipmentSlot_FireBossHunter;
        }

        static void EquipmentSlot_FireBossHunter(ILContext il)
        {
            EquipmentHooks.GenericPatchAllGetEquipmentQuality(il);

            ILCursor c = new ILCursor(il);

            if (c.TryGotoNext(MoveType.After,
                              x => x.MatchCallOrCallvirt<PickupDropTable>(nameof(PickupDropTable.GeneratePickup))))
            {
                c.Emit(OpCodes.Ldarg_0);
                c.EmitDelegate<Func<UniquePickup, EquipmentSlot, UniquePickup>>(tryGetQualityPickup);

                static UniquePickup tryGetQualityPickup(UniquePickup pickup, EquipmentSlot equipmentSlot)
                {
                    QualityTier qualityTier = equipmentSlot.GetCurrentEquipmentActionQualityTier();
                    if (qualityTier > QualityTier.None && qualityTier > QualityCatalog.GetQualityTier(pickup.pickupIndex))
                    {
                        PickupIndex qualityPickupIndex = QualityCatalog.GetPickupIndexOfQuality(pickup.pickupIndex, qualityTier);
                        if (qualityPickupIndex.isValid && qualityPickupIndex != pickup.pickupIndex)
                        {
                            return pickup.WithPickupIndex(qualityPickupIndex);
                        }
                        else
                        {
                            // Fallback for no quality version of item: just drop more of them

                            Vector3 spawnPosition = equipmentSlot.currentTarget.hurtBox ? equipmentSlot.currentTarget.hurtBox.transform.position : Vector3.zero;
                            Vector3 baseDropletDirection = (spawnPosition - equipmentSlot.characterBody.corePosition).normalized;

                            int extraPickupsToSpawn = (int)qualityTier + 1;
                            for (int i = 0; i < extraPickupsToSpawn; i++)
                            {
                                Vector3 dropletDirection = Quaternion.Euler(UnityEngine.Random.Range(-10f, 10f), UnityEngine.Random.Range(-10f, 10f), 0f) * baseDropletDirection;

                                PickupDropletController.CreatePickupDroplet(new GenericPickupController.CreatePickupInfo
                                {
                                    pickup = pickup,
                                    position = spawnPosition
                                }, spawnPosition, dropletDirection * 15f);
                            }
                        }
                    }

                    return pickup;
                }
            }
            else
            {
                Log.Error("Failed to find drop patch location");
            }

            c.Goto(0);

            int staticEquipmentPatchCount = 0;

            while (c.TryGotoNext(MoveType.After,
                                 x => x.MatchLdsfld(typeof(DLC1Content.Equipment), nameof(DLC1Content.Equipment.BossHunterConsumed))))
            {
                c.Emit(OpCodes.Ldarg_0);
                c.EmitDelegate<Func<EquipmentDef, EquipmentSlot, EquipmentDef>>(tryGetQualityEquipment);

                static EquipmentDef tryGetQualityEquipment(EquipmentDef equipmentDef, EquipmentSlot equipmentSlot)
                {
                    QualityTier qualityTier = equipmentSlot.GetCurrentEquipmentActionQualityTier();

                    EquipmentIndex equipmentIndex = equipmentDef ? equipmentDef.equipmentIndex : EquipmentIndex.None;
                    if (equipmentIndex != EquipmentIndex.None && qualityTier > QualityCatalog.GetQualityTier(equipmentIndex))
                    {
                        EquipmentIndex qualityEquipmentIndex = QualityCatalog.GetEquipmentIndexOfQuality(equipmentIndex, qualityTier);
                        if (qualityEquipmentIndex != EquipmentIndex.None && qualityEquipmentIndex != equipmentIndex)
                        {
                            equipmentDef = EquipmentCatalog.GetEquipmentDef(qualityEquipmentIndex);
                            equipmentIndex = qualityEquipmentIndex;
                        }
                    }

                    return equipmentDef;
                }

                staticEquipmentPatchCount++;
            }

            if (staticEquipmentPatchCount == 0)
            {
                Log.Error("Failed to find static equipment reference patch location");
            }
            else
            {
                Log.Debug($"Found {staticEquipmentPatchCount} static equipment reference patch location(s)");
            }
        }
    }
}
