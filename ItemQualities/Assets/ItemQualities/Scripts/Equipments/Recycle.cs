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
            IL.RoR2.EquipmentSlot.FireRecycle += RecycleQualityItemManipulator;
            IL.RoR2.EquipmentSlot.UpdateTargets += RecycleQualityItemManipulator;
        }

        static void RecycleQualityItemManipulator(ILContext il)
        {
            ILCursor c = new ILCursor(il);

            int patchCount = 0;

            int pickupControllerVarIndex = -1;
            while (c.TryGotoNext(MoveType.After,
                                 x => x.MatchLdloc(out pickupControllerVarIndex),
                                 x => x.MatchLdfld<GenericPickupController>(nameof(GenericPickupController.Recycled))))
            {
                c.Emit(OpCodes.Ldloc, pickupControllerVarIndex);
                c.EmitDelegate<Func<bool, GenericPickupController, bool>>(isUnrecyclable);

                static bool isUnrecyclable(bool isRecycled, GenericPickupController pickupController)
                {
                    return isRecycled || (pickupController && QualityCatalog.GetQualityTier(pickupController.pickup.pickupIndex) > QualityTier.None);
                }

                patchCount++;
            }

            if (patchCount == 0)
            {
                Log.Error($"{il.Method.FullName}: Failed to find patch location");
            }
            else
            {
                Log.Debug($"{il.Method.FullName}: Found {patchCount} patch location(s)");
            }
        }
    }
}
