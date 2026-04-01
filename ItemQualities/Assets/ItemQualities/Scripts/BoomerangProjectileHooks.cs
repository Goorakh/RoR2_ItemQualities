using ItemQualities.Utilities.Extensions;
using RoR2;
using RoR2.Projectile;

namespace ItemQualities
{
    static class BoomerangProjectileHooks
    {
        [SystemInitializer]
        static void Init()
        {
            On.RoR2.Projectile.BoomerangProjectile.FixedUpdate += BoomerangProjectile_FixedUpdate;
        }

        static void BoomerangProjectile_FixedUpdate(On.RoR2.Projectile.BoomerangProjectile.orig_FixedUpdate orig, BoomerangProjectile self)
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
