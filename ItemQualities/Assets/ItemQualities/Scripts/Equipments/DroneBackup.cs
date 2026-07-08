using ItemQualities.Utilities.Extensions;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using RoR2;
using System;

namespace ItemQualities.Equipments
{
    internal static class DroneBackup
    {
        [SystemInitializer]
        private static void Init()
        {
            IL.RoR2.EquipmentSlot.FireDroneBackup += EquipmentSlot_FireDroneBackup;
        }

        private static void EquipmentSlot_FireDroneBackup(ILContext il)
        {
            ILCursor c = new ILCursor(il);

            VariableDefinition droneCountVar = null;
            if (!c.TryGotoNext(MoveType.Before,
                               x => x.MatchLdloc<int>(il, out droneCountVar),
                               x => x.MatchLdcR4(out _),
                               x => x.MatchNewobj<DegreeSlices>()))
            {
                Log.Error("Failed to find droneCount variable");
                return;
            }

            if (!c.TryGotoPrev(MoveType.After,
                              x => x.MatchStloc(droneCountVar)))
            {
                Log.Warning("Failed to find droneCount set location");
            }

            c.Emit(OpCodes.Ldloc, droneCountVar);
            c.Emit(OpCodes.Ldarg_0);
            c.EmitDelegate<Func<EquipmentSlot, int>>(getBonusDroneCount);
            c.Emit(OpCodes.Add);
            c.Emit(OpCodes.Stloc, droneCountVar);

            static int getBonusDroneCount(EquipmentSlot equipmentSlot)
            {
                QualityTier qualityTier = equipmentSlot.GetCurrentEquipmentActionQualityTier();
                switch (qualityTier)
                {
                    case QualityTier.None:
                        return 0;
                    case QualityTier.Uncommon:
                        return 1;
                    case QualityTier.Rare:
                        return 2;
                    case QualityTier.Epic:
                        return 3;
                    case QualityTier.Legendary:
                        return 4;
                    default:
                        Log.Warning($"Quality tier {qualityTier} is not implemented");
                        return 0;
                }
            }
        }
    }
}
