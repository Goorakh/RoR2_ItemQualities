using ItemQualities.Utilities.Extensions;
using RoR2;
using RoR2.Projectile;

namespace ItemQualities
{
    internal static class BoomerangProjectileHooks
    {
        [SystemInitializer]
        private static void Init()
        {
            On.RoR2.Projectile.BoomerangProjectile.FixedUpdate += BoomerangProjectile_FixedUpdate;
        }

        private static void BoomerangProjectile_FixedUpdate(On.RoR2.Projectile.BoomerangProjectile.orig_FixedUpdate orig, BoomerangProjectile self)
        {
            if (self && self.TryGetComponentCached(out BoomerangProjectileQualityController boomerangProjectileQualityController))
            {
                if (boomerangProjectileQualityController.IsInHitPause)
                    return;
            }

            orig(self);
        }
    }
}
