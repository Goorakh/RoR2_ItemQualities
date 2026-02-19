using RoR2;
using RoR2.ContentManagement;
using RoR2.Projectile;
using UnityEngine;

namespace ItemQualities.ContentManagement
{
    internal sealed class ProjectileExplosionEffectScaleFixHelper
    {
        public void Step(ExtendedContentPack contentPack, GetContentPackAsyncArgs args)
        {
            foreach (ContentPackLoadInfo peerLoadInfo in args.peerLoadInfos)
            {
                foreach (GameObject projectilePrefab in peerLoadInfo.previousContentPack.projectilePrefabs)
                {
                    if (projectilePrefab.TryGetComponent(out ProjectileExplosion projectileExplosion))
                    {
                        tryFixProjectileEffectPrefab(ref projectileExplosion.explosionEffect, projectileExplosion.blastRadius);
                    }

                    if (projectilePrefab.TryGetComponent(out ProjectileImpactExplosion projectileImpactExplosion))
                    {
                        tryFixProjectileEffectPrefab(ref projectileImpactExplosion.impactEffect, projectileImpactExplosion.blastRadius);
                    }

                    void tryFixProjectileEffectPrefab(ref GameObject effectPrefab, float defaultRadius)
                    {
                        if (!effectPrefab || !effectPrefab.TryGetComponent(out EffectComponent effectComponent) || effectComponent.applyScale)
                            return;

                        EffectDef scaleFixExplosionEffectDef = EffectScalingFixer.GetOrCreateFixedScalingCopy(effectPrefab, defaultRadius);
                        if (scaleFixExplosionEffectDef != null)
                        {
                            Log.Debug($"Replacing explosion effect {effectPrefab.name} on projectile {projectilePrefab.name}");
                            effectPrefab = scaleFixExplosionEffectDef.prefab;
                        }
                    }
                }
            }
        }
    }
}
