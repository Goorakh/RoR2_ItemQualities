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

                    if (pickupController && QualityCatalog.GetQualityTier(pickupController.pickup.pickupIndex) > equipmentSlot.GetActiveEquipmentQualityTier())
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

                    if (pickupController && QualityCatalog.GetQualityTier(pickupController.pickup.pickupIndex) > equipmentSlot.GetCurrentEquipmentActionQualityTier())
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
    }
}
