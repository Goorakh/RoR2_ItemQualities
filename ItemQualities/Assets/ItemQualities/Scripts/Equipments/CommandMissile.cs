using ItemQualities.Utilities.Extensions;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using RoR2;
using System;

namespace ItemQualities.Equipments
{
    static class CommandMissile
    {
        [SystemInitializer]
        static void Init()
        {
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
            c.EmitDelegate<Func<int, EquipmentSlot, int>>(getMissileCount);

            static int getMissileCount(int missileCount, EquipmentSlot equipmentSlot)
            {
                QualityTier qualityTier = equipmentSlot ? equipmentSlot.GetCurrentEquipmentActionQualityTier() : QualityTier.None;
                if (qualityTier > QualityTier.None)
                {
                    CharacterBody body = equipmentSlot.characterBody;
                    Inventory inventory = body ? body.inventory : null;

                    if (body && inventory)
                    {
                        int missileItemCount = 0;
                        foreach (ItemIndex itemIndex in ItemCatalog.GetItemsWithTag(ItemTags.MissileRelated))
                        {
                            missileItemCount += inventory.CalculateEffectiveItemStacks(itemIndex);
                        }

                        int equipmentSlotCount = inventory.GetEquipmentSlotCount();
                        for (uint slot = 0; slot < equipmentSlotCount; slot++)
                        {
                            int equipmentSetCount = inventory.GetEquipmentSetCount(slot);
                            for (uint set = 0; set < equipmentSetCount; set++)
                            {
                                EquipmentState equipmentState = inventory.GetEquipment(slot, set);
                                EquipmentQualityGroupIndex equipmentGroupIndex = QualityCatalog.FindEquipmentQualityGroupIndex(equipmentState.equipmentIndex);
                                if (equipmentGroupIndex == ItemQualitiesContent.EquipmentQualityGroups.CommandMissile.GroupIndex)
                                {
                                    missileItemCount++;
                                }
                            }
                        }

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

                        missileCount += missileItemCount * missileBonusPerMissileItem;
                    }
                }

                return missileCount;
            }
        }
    }
}
