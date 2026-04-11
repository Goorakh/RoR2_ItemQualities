using ItemQualities.Utilities.Extensions;
using MonoMod.Cil;
using RoR2;
using System;

namespace ItemQualities
{
    static class QualityTempItemHandler
    {
        [SystemInitializer]
        static void Init()
        {
            IL.EntityStates.Drone.DroneJunk.Surprise.DropTempItemServer += Surprise_DropTempItemServer;

            IL.EntityStates.Drifter.Salvage.DropTempItemServer += Salvage_DropTempItemServer;
        }

        static void Surprise_DropTempItemServer(ILContext il)
        {
            ILCursor c = new ILCursor(il);

            if (!c.TryGotoNext(MoveType.After,
                               x => x.MatchCallOrCallvirt<PickupDropTable>(nameof(PickupDropTable.GeneratePickup))))
            {
                Log.Error("Failed to find patch location");
                return;
            }

            c.EmitDelegate<Func<UniquePickup, UniquePickup>>(clampPickupQuality);

            static UniquePickup clampPickupQuality(UniquePickup pickup)
            {
                return pickup.WithQualityTier(QualityTier.None);
            }
        }

        static void Salvage_DropTempItemServer(ILContext il)
        {
            ILCursor c = new ILCursor(il);

            if (!c.TryGotoNext(MoveType.After,
                               x => x.MatchCallOrCallvirt<PickupDropTable>(nameof(PickupDropTable.GeneratePickup))))
            {
                Log.Error("Failed to find patch location");
                return;
            }

            c.EmitDelegate<Func<UniquePickup, UniquePickup>>(clampPickupQuality);

            static UniquePickup clampPickupQuality(UniquePickup pickup)
            {
                return pickup.WithQualityTier(QualityTier.None);
            }
        }
    }
}
