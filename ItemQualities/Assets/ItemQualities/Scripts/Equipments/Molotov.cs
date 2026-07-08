using HG;
using ItemQualities.ContentManagement;
using ItemQualities.Utilities;
using ItemQualities.Utilities.Extensions;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using R2API;
using RoR2;
using RoR2.Projectile;
using RoR2BepInExPack.GameAssetPathsBetter;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace ItemQualities.Equipments
{
    internal static class Molotov
    {
        private static readonly GameObject[] _qualityMolotovClusterProjectilePrefabs = new GameObject[(int)QualityTier.Count];

        [ContentInitializer]
        private static IEnumerator LoadContent(ContentInitializerArgs args)
        {
            AsyncOperationHandle<GameObject> molotovClusterProjectileLoad = AddressableUtil.LoadTempAssetAsync<GameObject>(RoR2_DLC1_Molotov.MolotovClusterProjectile_prefab);
            molotovClusterProjectileLoad.OnSuccess(molotovClusterProjectilePrefab =>
            {
                using var _ = ListPool<GameObject>.RentCollection(out List<GameObject> projectilePrefabs);

                for (QualityTier qualityTier = 0; qualityTier < QualityTier.Count; qualityTier++)
                {
                    GameObject qualityMolotovClusterProjectile = molotovClusterProjectilePrefab.InstantiateClone(molotovClusterProjectilePrefab.name + qualityTier.ToString());

                    float scaleMult = qualityTier switch
                    {
                        QualityTier.Uncommon => 1.6f,
                        QualityTier.Rare => 2.0f,
                        QualityTier.Epic => 3.0f,
                        QualityTier.Legendary => 4.0f,
                        _ => throw new NotImplementedException($"Quality tier {qualityTier} is not implemented")
                    };

                    if (qualityMolotovClusterProjectile.TryGetComponent(out ProjectileController clusterProjectileController))
                    {
                        clusterProjectileController.ghostPrefab = clusterProjectileController.ghostPrefab.InstantiateClone(clusterProjectileController.ghostPrefab.name + qualityTier.ToString(), false);
                        clusterProjectileController.ghostPrefab.transform.localScale *= scaleMult;
                    }
                    else
                    {
                        Log.Warning($"Expected ProjectileController component on {qualityMolotovClusterProjectile}");
                    }

                    if (qualityMolotovClusterProjectile.TryGetComponent(out ProjectileSimple clusterProjectileSimple))
                    {
                        clusterProjectileSimple.desiredForwardSpeed *= ((scaleMult - 1f) / 5f) + 1f;
                    }
                    else
                    {
                        Log.Warning($"Expected ProjectileSimple component on {qualityMolotovClusterProjectile}");
                    }

                    if (qualityMolotovClusterProjectile.TryGetComponent(out ProjectileImpactExplosion clusterImpactExplosion))
                    {
                        clusterImpactExplosion.childrenCount += (int)qualityTier + 1;

                        GameObject qualityMolotovSingleProjectile = clusterImpactExplosion.childrenProjectilePrefab.InstantiateClone(clusterImpactExplosion.childrenProjectilePrefab.name + qualityTier.ToString());
                        clusterImpactExplosion.childrenProjectilePrefab = qualityMolotovSingleProjectile;

                        if (qualityMolotovSingleProjectile.TryGetComponent(out ProjectileController singleProjectileController))
                        {
                            singleProjectileController.ghostPrefab = singleProjectileController.ghostPrefab.InstantiateClone(singleProjectileController.ghostPrefab.name + qualityTier.ToString(), false);
                            singleProjectileController.ghostPrefab.transform.localScale *= scaleMult;
                        }
                        else
                        {
                            Log.Warning($"Expected ProjectileController component on {qualityMolotovSingleProjectile}");
                        }

                        if (qualityMolotovSingleProjectile.TryGetComponent(out ProjectileSimple singleProjectileSimple))
                        {
                            singleProjectileSimple.desiredForwardSpeed *= ((scaleMult - 1f) / 5f) + 1f;
                        }
                        else
                        {
                            Log.Warning($"Expected ProjectileSimple component on {qualityMolotovSingleProjectile}");
                        }

                        if (qualityMolotovSingleProjectile.TryGetComponent(out ProjectileImpactExplosion singleImpactExplosion))
                        {
                            GameObject qualityMolotovDotZoneProjectile = singleImpactExplosion.childrenProjectilePrefab.InstantiateClone(singleImpactExplosion.childrenProjectilePrefab.name + qualityTier.ToString());
                            singleImpactExplosion.childrenProjectilePrefab = qualityMolotovDotZoneProjectile;

                            float lifetime = 0f;

                            if (qualityMolotovDotZoneProjectile.TryGetComponent(out ProjectileDotZone dotZone))
                            {
                                dotZone.damageCoefficient += qualityTier switch
                                {
                                    QualityTier.Uncommon => 0.5f,
                                    QualityTier.Rare => 1.0f,
                                    QualityTier.Epic => 2.0f,
                                    QualityTier.Legendary => 3.0f,
                                    _ => throw new NotImplementedException($"Quality tier {qualityTier} is not implemented")
                                };

                                lifetime = dotZone.lifetime;
                            }
                            else
                            {
                                Log.Warning($"Expected ProjectileDotZone component on {qualityMolotovDotZoneProjectile}");
                            }

                            Transform dotZoneFX = qualityMolotovDotZoneProjectile.transform.Find("FX");
                            if (dotZoneFX)
                            {
                                dotZoneFX.transform.localScale *= scaleMult;

                                ObjectScaleCurve dotZoneScaleCurve = dotZoneFX.gameObject.AddComponent<ObjectScaleCurve>();
                                dotZoneScaleCurve.timeMax = lifetime + 0.5f;
                                dotZoneScaleCurve.useOverallCurveOnly = true;
                                dotZoneScaleCurve.overallCurve = AnimationCurve.EaseInOut(0f, 1f, 1f, qualityTier switch
                                {
                                    QualityTier.Uncommon => 1.5f,
                                    QualityTier.Rare => 2f,
                                    QualityTier.Epic => 2.5f,
                                    QualityTier.Legendary => 3f,
                                    _ => throw new NotImplementedException($"Quality tier {qualityTier} is not implemented")
                                });
                            }
                            else
                            {
                                Log.Warning($"Failed to find FX child on {qualityMolotovDotZoneProjectile}");
                            }

                            projectilePrefabs.Add(qualityMolotovDotZoneProjectile);
                        }
                        else
                        {
                            Log.Warning($"Expected ProjectileImpactExplosion component on {qualityMolotovSingleProjectile}");
                        }

                        projectilePrefabs.Add(qualityMolotovSingleProjectile);
                    }
                    else
                    {
                        Log.Warning($"Expected ProjectileImpactExplosion component on {qualityMolotovClusterProjectile}");
                    }

                    projectilePrefabs.Add(qualityMolotovClusterProjectile);

                    _qualityMolotovClusterProjectilePrefabs[(int)qualityTier] = qualityMolotovClusterProjectile;
                }

                if (projectilePrefabs.Count > 0)
                {
                    args.ContentPack.projectilePrefabs.Add(projectilePrefabs.ToArray());
                }
            });

            return molotovClusterProjectileLoad.AsProgressCoroutine(args.ProgressReceiver);
        }

        [SystemInitializer]
        private static void Init()
        {
            IL.RoR2.EquipmentSlot.FireMolotov += EquipmentSlot_FireMolotov;
        }

        private static void EquipmentSlot_FireMolotov(ILContext il)
        {
            ILCursor c = new ILCursor(il);

            if (!c.TryGotoNext(MoveType.After,
                               x => x.MatchLdstr("Prefabs/Projectiles/MolotovClusterProjectile"),
                               x => x.MatchCallOrCallvirt(typeof(LegacyResourcesAPI), nameof(LegacyResourcesAPI.Load))))
            {
                Log.Error("Failed to find patch location");
                return;
            }

            c.Emit(OpCodes.Ldarg_0);
            c.EmitDelegate<Func<GameObject, EquipmentSlot, GameObject>>(getProjectilePrefab);

            static GameObject getProjectilePrefab(GameObject prefab, EquipmentSlot equipmentSlot)
            {
                QualityTier qualityTier = equipmentSlot.GetCurrentEquipmentActionQualityTier();

                GameObject qualityPrefab = ArrayUtils.GetSafe(_qualityMolotovClusterProjectilePrefabs, (int)qualityTier);
                return qualityPrefab ? qualityPrefab : prefab;
            }
        }
    }
}
