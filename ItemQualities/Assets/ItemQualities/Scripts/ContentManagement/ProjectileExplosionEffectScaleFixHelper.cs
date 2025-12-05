using R2API;
using RoR2;
using RoR2.ContentManagement;
using RoR2.Projectile;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ItemQualities.ContentManagement
{
    public sealed class ProjectileExplosionEffectScaleFixHelper
    {
        readonly Dictionary<GameObject, EffectDef> _fixedExplosionEffectCache = new Dictionary<GameObject, EffectDef>();

        public void Step(ExtendedContentPack contentPack, GetContentPackAsyncArgs args)
        {
            foreach (ContentPackLoadInfo peerLoadInfo in args.peerLoadInfos)
            {
                foreach (GameObject projectilePrefab in peerLoadInfo.previousContentPack.projectilePrefabs)
                {
                    if (projectilePrefab.TryGetComponent(out ProjectileExplosion projectileExplosion))
                    {
                        tryFixProjectileEffectPrefab(ref projectileExplosion.explosionEffect);
                    }

                    if (projectilePrefab.TryGetComponent(out ProjectileImpactExplosion projectileImpactExplosion))
                    {
                        tryFixProjectileEffectPrefab(ref projectileImpactExplosion.impactEffect);
                    }

                    void tryFixProjectileEffectPrefab(ref GameObject effectPrefab)
                    {
                        if (!effectPrefab || !effectPrefab.TryGetComponent(out EffectComponent effectComponent) || effectComponent.applyScale)
                            return;

                        if (!_fixedExplosionEffectCache.TryGetValue(effectPrefab, out EffectDef scaleFixExplosionEffectDef))
                        {
                            GameObject scaleFixExplosionEffectPrefab = effectPrefab.InstantiateClone($"{effectPrefab.name}_ScaleFix");
                            effectComponent = scaleFixExplosionEffectPrefab.GetComponent<EffectComponent>();
                            effectComponent.applyScale = true;

                            if (scaleFixExplosionEffectPrefab.transform.localScale != Vector3.one)
                            {
                                if (scaleFixExplosionEffectPrefab.transform.childCount > 0)
                                {
                                    GameObject scalerObj = new GameObject("Scaler");
                                    scalerObj.transform.SetParent(effectComponent.transform);
                                    scalerObj.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
                                    scalerObj.transform.localScale = scaleFixExplosionEffectPrefab.transform.localScale * (1f / projectileExplosion.blastRadius);

                                    for (int i = scaleFixExplosionEffectPrefab.transform.childCount - 1; i >= 0; i--)
                                    {
                                        Transform child = scaleFixExplosionEffectPrefab.transform.GetChild(i);
                                        if (child != scalerObj.transform)
                                        {
                                            child.SetParent(scalerObj.transform, false);
                                            child.SetAsFirstSibling();
                                        }
                                    }
                                }
                                else
                                {
                                    Log.Warning($"Scaled effect {effectPrefab.name} has no children, set prefab scale will be lost");
                                }

                                scaleFixExplosionEffectPrefab.transform.localScale = Vector3.one;
                            }

                            scaleFixExplosionEffectDef = new EffectDef(scaleFixExplosionEffectPrefab);
                            _fixedExplosionEffectCache.Add(effectPrefab, scaleFixExplosionEffectDef);
                        }

                        Log.Debug($"Replacing explosion effect {effectPrefab.name} on projectile {projectilePrefab.name}");
                        effectPrefab = scaleFixExplosionEffectDef.prefab;
                    }
                }
            }

            args.output.effectDefs.Add(_fixedExplosionEffectCache.Values.ToArray());
            Log.Debug($"Fixed {_fixedExplosionEffectCache.Count} projectile explosion effect(s)");
        }
    }
}
