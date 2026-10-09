using HG;
using HG.Coroutines;
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
            PartitionedProgress<ReadableProgress<float>> progress = new PartitionedProgress<ReadableProgress<float>>(args.ProgressReceiver);
            ProgressPartition molotovClusterPrefabLoadProgress = progress.AddPartition();
            ProgressPartition molotovClusterGhostPrefabLoadProgress = progress.AddPartition();
            ProgressPartition molotovSingleGhostPrefabLoadProgress = progress.AddPartition();

            AsyncOperationHandle<GameObject> molotovClusterProjectileLoad = AddressableUtil.LoadTempAssetAsync<GameObject>(RoR2_DLC1_Molotov.MolotovClusterProjectile_prefab);
            yield return molotovClusterProjectileLoad.AsProgressCoroutine(molotovClusterPrefabLoadProgress);

            if (!molotovClusterProjectileLoad.AssertLoaded())
            {
                yield break;
            }

            GameObject molotovClusterProjectilePrefab = molotovClusterProjectileLoad.Result;

            if (!molotovClusterProjectilePrefab.ExpectComponent(out ProjectileController molotovClusterProjectilePrefabController))
            {
                yield break;
            }

            GameObject molotovClusterGhostPrefab;
            {
                CoroutineResult<GameObject> result = CoroutineResultPool<GameObject>.instance.Request();
                try
                {
                    yield return AddressableUtil.LoadTempAssetOrDirectReferenceAsync(molotovClusterProjectilePrefabController.ghostPrefabAddress, molotovClusterProjectilePrefabController.ghostPrefab, molotovClusterGhostPrefabLoadProgress, result);

                    molotovClusterGhostPrefab = result.Value;
                }
                finally
                {
                    CoroutineResultPool<GameObject>.instance.Return(result);
                }
            }


            if (!molotovClusterProjectilePrefab.ExpectComponent(out ProjectileImpactExplosion molotovClusterImpactExplosion))
            {
                yield break;
            }

            if (!molotovClusterImpactExplosion.childrenProjectilePrefab)
            {
                Log.Error($"{molotovClusterImpactExplosion} is missing a value for {nameof(ProjectileImpactExplosion.childrenProjectilePrefab)}");
                yield break;
            }

            GameObject molotovSingleProjectilePrefab = molotovClusterImpactExplosion.childrenProjectilePrefab;
            if (!molotovSingleProjectilePrefab.ExpectComponent(out ProjectileController molotovSingleProjectilePrefabController))
            {
                yield break;
            }

            GameObject molotovSingleGhostPrefab;
            {
                CoroutineResult<GameObject> result = CoroutineResultPool<GameObject>.instance.Request();
                try
                {
                    yield return AddressableUtil.LoadTempAssetOrDirectReferenceAsync(molotovSingleProjectilePrefabController.ghostPrefabAddress, molotovSingleProjectilePrefabController.ghostPrefab, molotovSingleGhostPrefabLoadProgress, result);

                    molotovSingleGhostPrefab = result.Value;
                }
                finally
                {
                    CoroutineResultPool<GameObject>.instance.Return(result);
                }
            }

            using var _ = ListPool<GameObject>.RentCollection(out List<GameObject> projectilePrefabs);

            for (QualityTier qualityTier = 0; qualityTier < QualityTier.Count; qualityTier++)
            {
                GameObject qualityMolotovClusterProjectile = molotovClusterProjectilePrefab.InstantiateClone(molotovClusterProjectilePrefab.name + qualityTier.ToString());

                float scaleMult = qualityTier switch
                {
                    QualityTier.Uncommon => 1.3f,
                    QualityTier.Rare => 2f,
                    QualityTier.Epic => 3.5f,
                    QualityTier.Legendary => 4.5f,
                    _ => throw new NotImplementedException($"Quality tier {qualityTier} is not implemented")
                };

                if (qualityMolotovClusterProjectile.ExpectComponent(out ProjectileController qualityMolotovClusterProjectileController))
                {
                    GameObject qualityMolotovClusterGhostPrefab = molotovClusterGhostPrefab.InstantiateClone(molotovClusterGhostPrefab.name + qualityTier.ToString(), false);
                    qualityMolotovClusterGhostPrefab.transform.localScale *= scaleMult;

                    qualityMolotovClusterProjectileController.ghostPrefab = qualityMolotovClusterGhostPrefab;
                }

                if (qualityMolotovClusterProjectile.ExpectComponent(out ProjectileSimple qualityMolotovClusterProjectileSimple))
                {
                    qualityMolotovClusterProjectileSimple.desiredForwardSpeed *= ((scaleMult - 1f) / 5f) + 1f;
                }

                if (qualityMolotovClusterProjectile.ExpectComponent(out ProjectileImpactExplosion qualityMolotovClusterImpactExplosion))
                {
                    qualityMolotovClusterImpactExplosion.childrenCount += (int)qualityTier + 1;

                    GameObject qualityMolotovSingleProjectile = qualityMolotovClusterImpactExplosion.childrenProjectilePrefab.InstantiateClone(qualityMolotovClusterImpactExplosion.childrenProjectilePrefab.name + qualityTier.ToString());
                    qualityMolotovClusterImpactExplosion.childrenProjectilePrefab = qualityMolotovSingleProjectile;

                    if (qualityMolotovSingleProjectile.ExpectComponent(out ProjectileController qualityMolotovSingleProjectileController))
                    {
                        GameObject qualityMolotovSingleGhostPrefab = molotovSingleGhostPrefab.InstantiateClone(molotovSingleGhostPrefab.name + qualityTier.ToString(), false);
                        qualityMolotovSingleGhostPrefab.transform.localScale *= scaleMult;

                        qualityMolotovSingleProjectileController.ghostPrefab = qualityMolotovSingleGhostPrefab;
                    }

                    if (qualityMolotovSingleProjectile.ExpectComponent(out ProjectileSimple qualityMolotovSingleProjectileSimple))
                    {
                        qualityMolotovSingleProjectileSimple.desiredForwardSpeed *= ((scaleMult - 1f) / 5f) + 1f;
                    }

                    if (qualityMolotovSingleProjectile.ExpectComponent(out ProjectileImpactExplosion qualityMolotovSingleImpactExplosion))
                    {
                        GameObject qualityMolotovDotZoneProjectile = qualityMolotovSingleImpactExplosion.childrenProjectilePrefab.InstantiateClone(qualityMolotovSingleImpactExplosion.childrenProjectilePrefab.name + qualityTier.ToString());
                        qualityMolotovSingleImpactExplosion.childrenProjectilePrefab = qualityMolotovDotZoneProjectile;

                        float lifetime = 0f;

                        if (qualityMolotovDotZoneProjectile.ExpectComponent(out ProjectileDotZone qualityMolotovDotZone))
                        {
                            qualityMolotovDotZone.damageCoefficient += 1f;
                            lifetime = qualityMolotovDotZone.lifetime;
                        }

                        Transform qualityMolotovDotZoneFX = qualityMolotovDotZoneProjectile.transform.Find("FX");
                        if (qualityMolotovDotZoneFX)
                        {
                            qualityMolotovDotZoneFX.transform.localScale *= scaleMult;

                            ObjectScaleCurve dotZoneScaleCurve = qualityMolotovDotZoneFX.gameObject.AddComponent<ObjectScaleCurve>();
                            dotZoneScaleCurve.timeMax = lifetime + 0.5f;
                            dotZoneScaleCurve.useOverallCurveOnly = true;
                            dotZoneScaleCurve.overallCurve = AnimationCurve.EaseInOut(0f, 1f, 1f, 2f);
                        }
                        else
                        {
                            Log.Warning($"Failed to find FX child on {qualityMolotovDotZoneProjectile}");
                        }

                        projectilePrefabs.Add(qualityMolotovDotZoneProjectile);
                    }

                    projectilePrefabs.Add(qualityMolotovSingleProjectile);
                }

                projectilePrefabs.Add(qualityMolotovClusterProjectile);

                _qualityMolotovClusterProjectilePrefabs[(int)qualityTier] = qualityMolotovClusterProjectile;
            }

            if (projectilePrefabs.Count > 0)
            {
                args.ContentPack.projectilePrefabs.Add(projectilePrefabs.ToArray());
            }
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
                Log.PatchError(il, "Failed to find patch location");
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
