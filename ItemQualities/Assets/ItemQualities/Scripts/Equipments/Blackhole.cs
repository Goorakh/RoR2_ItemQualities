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
using UnityEngine;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace ItemQualities.Equipments
{
    static class Blackhole
    {
        static readonly GameObject[] _qualityProjectilePrefabs = new GameObject[(int)QualityTier.Count];

        [ContentInitializer]
        static IEnumerator LoadContent(ContentIntializerArgs args)
        {
            AsyncOperationHandle<GameObject> gravSphereProjectileLoad = AddressableUtil.LoadTempAssetAsync<GameObject>(RoR2_Base_Blackhole.GravSphere_prefab);
            gravSphereProjectileLoad.OnSuccess(gravSphereProjectilePrefab =>
            {
                if (!gravSphereProjectilePrefab.TryGetComponent(out ProjectileSimple projectileSimple))
                {
                    Log.Warning($"Expected component ProjectileSimple on {gravSphereProjectilePrefab}");
                    return;
                }

                if (!gravSphereProjectilePrefab.TryGetComponent(out RadialForce radialForce))
                {
                    Log.Warning($"Expected component RadialForce on {gravSphereProjectilePrefab}");
                    return;
                }

                float baseRange = radialForce.radius;

                for (QualityTier qualityTier = 0; qualityTier < QualityTier.Count; qualityTier++)
                {
                    float rangeIncrease;
                    float durationIncrease;
                    switch (qualityTier)
                    {
                        case QualityTier.Uncommon:
                            rangeIncrease = 10f;
                            durationIncrease = 4f;
                            break;
                        case QualityTier.Rare:
                            rangeIncrease = 25f;
                            durationIncrease = 8f;
                            break;
                        case QualityTier.Epic:
                            rangeIncrease = 40f;
                            durationIncrease = 15f;
                            break;
                        case QualityTier.Legendary:
                            rangeIncrease = 60f;
                            durationIncrease = 25f;
                            break;
                        default:
                            throw new NotImplementedException($"Quality tier {qualityTier} is not implemented");
                    }

                    GameObject qualityGravSphereProjectilePrefab = gravSphereProjectilePrefab.InstantiateClone(gravSphereProjectilePrefab.name + qualityTier.ToString());

                    ProjectileSimple qualityProjectileSimple = qualityGravSphereProjectilePrefab.GetComponent<ProjectileSimple>();
                    qualityProjectileSimple.lifetime += durationIncrease;

                    RadialForce qualityRadialForce = qualityGravSphereProjectilePrefab.GetComponent<RadialForce>();
                    qualityRadialForce.radius += rangeIncrease;

                    Transform sphereTransform = qualityGravSphereProjectilePrefab.transform.Find("Sphere");
                    if (sphereTransform && sphereTransform.TryGetComponent(out ObjectScaleCurve sphereScaleCurve))
                    {
                        sphereScaleCurve.timeMax += durationIncrease;
                    }

                    Transform lightTransform = qualityGravSphereProjectilePrefab.transform.Find("Point light");
                    if (lightTransform && lightTransform.TryGetComponent(out LightIntensityCurve lightCurve))
                    {
                        lightCurve.timeMax += durationIncrease;
                    }

                    float scaleMultiplier = (baseRange + rangeIncrease) / baseRange;
                    for (int i = qualityGravSphereProjectilePrefab.transform.childCount - 1; i >= 0; i--)
                    {
                        Transform child = qualityGravSphereProjectilePrefab.transform.GetChild(i);
                        if (child)
                        {
                            child.localScale *= scaleMultiplier;
                        }
                    }

                    foreach (ParticleSystem particleSystem in qualityGravSphereProjectilePrefab.GetComponentsInChildren<ParticleSystem>(true))
                    {
                        ParticleSystem.MainModule main = particleSystem.main;
                        if (main.scalingMode == ParticleSystemScalingMode.Local)
                        {
                            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
                        }
                    }

                    _qualityProjectilePrefabs[(int)qualityTier] = qualityGravSphereProjectilePrefab;
                }

                args.ContentPack.projectilePrefabs.Add(_qualityProjectilePrefabs);
            });

            return gravSphereProjectileLoad.AsProgressCoroutine(args.ProgressReceiver);
        }

        [SystemInitializer]
        static void Init()
        {
            IL.RoR2.EquipmentSlot.FireBlackhole += EquipmentSlot_FireBlackhole;
        }

        static void EquipmentSlot_FireBlackhole(ILContext il)
        {
            ILCursor c = new ILCursor(il);

            if (!c.TryGotoNext(MoveType.After,
                               x => x.MatchLdstr("Prefabs/Projectiles/GravSphere"),
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
                if (qualityTier > QualityTier.None)
                {
                    GameObject qualityPrefab = ArrayUtils.GetSafe(_qualityProjectilePrefabs, (int)qualityTier);
                    if (qualityPrefab)
                    {
                        prefab = qualityPrefab;
                    }
                }

                return prefab;
            }
        }
    }
}
