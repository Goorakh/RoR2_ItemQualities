using ItemQualities.Utilities.Extensions;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using RoR2;
using System;

namespace ItemQualities.Equipments
{
    static class Recycle
    {
        [SystemInitializer]
        static void Init()
        {
            IL.RoR2.EquipmentSlot.UpdateTargets += EquipmentSlot_UpdateTargets;
            IL.RoR2.EquipmentSlot.FireRecycle += EquipmentSlot_FireRecycle;
        }

        static void EquipmentSlot_UpdateTargets(ILContext il)
        {
            ILCursor c = new ILCursor(il);

            int patchCount = 0;

            int pickupControllerVarIndex = -1;
            while (c.TryGotoNext(MoveType.After,
                                 x => x.MatchLdloc(out pickupControllerVarIndex),
                                 x => x.MatchLdfld<GenericPickupController>(nameof(GenericPickupController.Recycled))))
            {
                c.Emit(OpCodes.Ldloc, pickupControllerVarIndex);
                c.Emit(OpCodes.Ldarg_0);
                c.EmitDelegate<Func<bool, GenericPickupController, EquipmentSlot, bool>>(isUnrecyclable);

                static bool isUnrecyclable(bool isRecycled, GenericPickupController pickupController, EquipmentSlot equipmentSlot)
                {
                    if (isRecycled)
                        return true;

                    bool pickupIsQuality = pickupController && QualityCatalog.GetQualityTier(pickupController.pickup.pickupIndex) > QualityTier.None;
                    bool recyclerIsQuality = equipmentSlot.GetActiveEquipmentQualityTier() > QualityTier.None;

                    if (pickupIsQuality && !recyclerIsQuality)
                        return true;

                    return false;
                }

                patchCount++;
            }

            if (patchCount == 0)
            {
                Log.Error("Failed to find patch location");
            }
            else
            {
                Log.Debug($"Found {patchCount} patch location(s)");
            }
        }

        static void EquipmentSlot_FireRecycle(ILContext il)
        {
            ILCursor c = new ILCursor(il);

            int patchCount = 0;

            int pickupControllerVarIndex = -1;
            while (c.TryGotoNext(MoveType.After,
                                 x => x.MatchLdloc(out pickupControllerVarIndex),
                                 x => x.MatchLdfld<GenericPickupController>(nameof(GenericPickupController.Recycled))))
            {
                c.Emit(OpCodes.Ldloc, pickupControllerVarIndex);
                c.Emit(OpCodes.Ldarg_0);
                c.EmitDelegate<Func<bool, GenericPickupController, EquipmentSlot, bool>>(isUnrecyclable);

                static bool isUnrecyclable(bool isRecycled, GenericPickupController pickupController, EquipmentSlot equipmentSlot)
                {
                    if (isRecycled)
                        return true;

                    bool pickupIsQuality = pickupController && QualityCatalog.GetQualityTier(pickupController.pickup.pickupIndex) > QualityTier.None;
                    bool recyclerIsQuality = equipmentSlot.GetCurrentEquipmentActionQualityTier() > QualityTier.None;

                    if (pickupIsQuality && !recyclerIsQuality)
                        return true;

                    return false;
                }

                patchCount++;
            }

            if (patchCount == 0)
            {
                Log.Error("Failed to find recyclable patch location");
            }
            else
            {
                Log.Debug($"Found {patchCount} recyclable patch location(s)");
            }

            c.Goto(0);

            if (c.TryGotoNext(MoveType.Before,
                              x => x.MatchCallOrCallvirt(typeof(PickupTransmutationManager), nameof(PickupTransmutationManager.GetAvailableGroupFromPickupIndex))))
            {
                c.Emit(OpCodes.Ldarg_0);
                c.EmitDelegate<Func<PickupIndex, EquipmentSlot, PickupIndex>>(getPickupIndex);

                static PickupIndex getPickupIndex(PickupIndex pickupIndex, EquipmentSlot equipmentSlot)
                {
                    QualityTier qualityTier = QualityCatalog.GetQualityTier(pickupIndex);
                    QualityTier equipmentQualityTier = equipmentSlot.GetCurrentEquipmentActionQualityTier();
                    if (qualityTier > equipmentQualityTier)
                    {
                        pickupIndex = QualityCatalog.GetPickupIndexOfQuality(pickupIndex, equipmentQualityTier);
                        qualityTier = equipmentQualityTier;
                    }

                    return pickupIndex;
                }
            }
            else
            {
                Log.Error("Failed to find pickup group patch location");
            }
        }
    }
}
