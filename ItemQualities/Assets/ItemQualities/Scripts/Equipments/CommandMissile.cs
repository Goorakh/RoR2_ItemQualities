using ItemQualities.Utilities.Extensions;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using RoR2;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ItemQualities.Equipments
{
    static class CommandMissile
    {
        static EquipmentIndex[] _missileEquipments = Array.Empty<EquipmentIndex>();

        [SystemInitializer(typeof(QualityCatalog))]
        static void Init()
        {
            HashSet<EquipmentIndex> missileEquipments = new HashSet<EquipmentIndex>(EquipmentCatalog.equipmentCount);
            for (QualityTier qualityTier = QualityTier.None; qualityTier < QualityTier.Count; qualityTier++)
            {
                void tryAddEquipment(EquipmentQualityGroup equipmentGroup)
                {
                    if (!equipmentGroup)
                        return;

                    EquipmentIndex equipmentIndex = equipmentGroup.GetEquipmentIndex(qualityTier);
                    if (equipmentIndex != EquipmentIndex.None)
                    {
                        missileEquipments.Add(equipmentIndex);
                    }
                }

                tryAddEquipment(ItemQualitiesContent.EquipmentQualityGroups.CommandMissile);

                if (qualityTier != QualityTier.None)
                {
                    tryAddEquipment(ItemQualitiesContent.EquipmentQualityGroups.Jetpack);
                }
            }

            if (missileEquipments.Count > 0)
            {
                _missileEquipments = missileEquipments.ToArray();
                Array.Sort(_missileEquipments);
            }

            IL.RoR2.EquipmentSlot.FireCommandMissile += EquipmentSlot_FireCommandMissile;
        }

        static void EquipmentSlot_FireCommandMissile(ILContext il)
        {
            ILCursor c = new ILCursor(il);

            if (!c.TryGotoNext(MoveType.Before,
                               x => x.MatchAdd(),
                               x => x.MatchStfld<EquipmentSlot>(nameof(EquipmentSlot.remainingMissiles))))
            {
                Log.Error("Failed to find patch location");
                return;
            }

            c.Emit(OpCodes.Ldarg_0);
            c.EmitDelegate<Func<int, EquipmentSlot, int>>(getMissileProjectileCount);

            static int getMissileProjectileCount(int missileCount, EquipmentSlot equipmentSlot)
            {
                QualityTier qualityTier = equipmentSlot ? equipmentSlot.GetCurrentEquipmentActionQualityTier() : QualityTier.None;
                if (qualityTier > QualityTier.None)
                {
                    CharacterBody body = equipmentSlot.characterBody;
                    if (body && body.master)
                    {
                        int masterMissileCount = getMissileCount(body.master);

                        Log.Debug($"Missile count for {Util.GetBestBodyName(body.gameObject)}: {masterMissileCount}");

                        int missileBonusPerMissileItem;
                        switch (qualityTier)
                        {
                            case QualityTier.Uncommon:
                                missileBonusPerMissileItem = 2;
                                break;
                            case QualityTier.Rare:
                                missileBonusPerMissileItem = 5;
                                break;
                            case QualityTier.Epic:
                                missileBonusPerMissileItem = 8;
                                break;
                            case QualityTier.Legendary:
                                missileBonusPerMissileItem = 12;
                                break;
                            default:
                                missileBonusPerMissileItem = 0;
                                Log.Error($"Quality tier {qualityTier} is not implemented");
                                break;
                        }

                        missileCount += masterMissileCount * missileBonusPerMissileItem;
                    }
                }

                return missileCount;
            }
        }

        static int getMissileCount(CharacterMaster master)
        {
            int missileItemCount = 0;

            if (!master.inventory.inventoryDisabled)
            {
                foreach (ItemIndex itemIndex in ItemCatalog.GetItemsWithTag(ItemTags.MissileRelated))
                {
                    missileItemCount += master.inventory.CalculateEffectiveItemStacks(itemIndex);
                }
            }

            if (!master.inventory.GetEquipmentDisabled())
            {
                int equipmentSlotCount = master.inventory.GetEquipmentSlotCount();
                for (uint slot = 0; slot < equipmentSlotCount; slot++)
                {
                    int equipmentSetCount = master.inventory.GetEquipmentSetCount(slot);
                    for (uint set = 0; set < equipmentSetCount; set++)
                    {
                        EquipmentState equipmentState = master.inventory.GetEquipment(slot, set);
                        if (equipmentState.equipmentIndex != EquipmentIndex.None &&
                            Array.BinarySearch(_missileEquipments, equipmentState.equipmentIndex) >= 0)
                        {
                            missileItemCount++;
                        }
                    }
                }
            }

            if (master && master.minionOwnership.ownerMaster)
            {
                missileItemCount += getMissileCount(master.minionOwnership.ownerMaster);
            }

            return missileItemCount;
        }
    }
}
