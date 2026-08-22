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
using RoR2BepInExPack.Utilities;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace ItemQualities.Equipments
{
    internal static class BFG
    {
        private static readonly GameObject[] _qualityProjectilePrefabs = new GameObject[(int)QualityTier.Count];

        [ContentInitializer]
        private static IEnumerator LoadContent(ContentInitializerArgs args)
        {
            AsyncOperationHandle<GameObject> bfgProjectilePrefabLoad = AddressableUtil.LoadTempAssetAsync<GameObject>(RoR2_Base_BFG.BeamSphere_prefab);
            bfgProjectilePrefabLoad.OnSuccess(projectilePrefab =>
            {
                if (!projectilePrefab.ExpectComponent(out ProjectileController projectileController))
                {
                    return;
                }

                if (!projectilePrefab.ExpectComponent(out ProjectileImpactExplosion projectileExplosion))
                {
                    return;
                }

                float baseBlastRadius = projectileExplosion.blastRadius;

                GameObject qualityProjectileGhostPrefab = EffectScalingFixer.CreateFixedScalingCopy(projectileController.ghostPrefab, 1f);
                qualityProjectileGhostPrefab.name = "Quality" + qualityProjectileGhostPrefab.name;
                if (qualityProjectileGhostPrefab.ExpectComponent(out ProjectileGhostController qualityProjectileGhostController))
                {
                    qualityProjectileGhostController.inheritScaleFromProjectile = true;
                }

                GameObject qualityProjectileImpactEffect = EffectScalingFixer.CreateFixedScalingCopy(projectileExplosion.impactEffect, baseBlastRadius);
                qualityProjectileImpactEffect.name = "Quality" + qualityProjectileImpactEffect.name;

                args.ContentPack.effectDefs.Add(new EffectDef(qualityProjectileImpactEffect));

                for (QualityTier qualityTier = 0; qualityTier < QualityTier.Count; qualityTier++)
                {
                    float blastRadiusIncrease = qualityTier switch
                    {
                        QualityTier.Uncommon => 10f,
                        QualityTier.Rare => 15f,
                        QualityTier.Epic => 25f,
                        QualityTier.Legendary => 35f,
                        _ => throw new NotImplementedException($"Quality tier {qualityTier} is not implemented")
                    };

                    float scaleMultiplier = (baseBlastRadius + blastRadiusIncrease) / baseBlastRadius;

                    const float lifetime = 30f;

                    GameObject qualityProjectilePrefab = projectilePrefab.InstantiateClone(projectilePrefab.name + qualityTier.ToString());
                    qualityProjectilePrefab.transform.localScale *= scaleMultiplier;

                    // Original prefab is already checked for this component, so no need to check it on the clone
                    ProjectileController qualityProjectileController = qualityProjectilePrefab.GetComponent<ProjectileController>();
                    qualityProjectileController.ghostPrefab = qualityProjectileGhostPrefab;

                    ProjectileImpactExplosion qualityProjectileExplosion = qualityProjectilePrefab.GetComponent<ProjectileImpactExplosion>();
                    qualityProjectileExplosion.blastRadius += blastRadiusIncrease;
                    qualityProjectileExplosion.impactEffect = qualityProjectileImpactEffect;
                    qualityProjectileExplosion.falloffModel = BlastAttack.FalloffModel.None;
                    qualityProjectileExplosion.lifetime = lifetime;

                    if (qualityProjectilePrefab.ExpectComponent(out ProjectileProximityBeamController qualityProjectileBeamController))
                    {
                        qualityProjectileBeamController.attackRange += blastRadiusIncrease;
                    }

                    ProjectileSimple projectileSimple = qualityProjectilePrefab.GetComponent<ProjectileSimple>();
                    projectileSimple.updateAfterFiring = true;
                    projectileSimple.lifetime = lifetime;

                    ProjectileSteerTowardTarget projectileSteerTowardTarget = qualityProjectilePrefab.AddComponent<ProjectileSteerTowardTarget>();
                    projectileSteerTowardTarget.rotationSpeed = 90f;

                    ProjectileDirectionalTargetFinder projectileDirectionalTargetFinder = qualityProjectilePrefab.AddComponent<ProjectileDirectionalTargetFinder>();
                    projectileDirectionalTargetFinder.lookRange = 600f;
                    projectileDirectionalTargetFinder.lookCone = 180f;
                    projectileDirectionalTargetFinder.targetSearchInterval = 1f;
                    projectileDirectionalTargetFinder.onlySearchIfNoTarget = true;
                    projectileDirectionalTargetFinder.allowTargetLoss = false;
                    projectileDirectionalTargetFinder.testLoS = true;

                    float damageBonusCoefficientPerSecond = qualityTier switch
                    {
                        QualityTier.Uncommon => 0.015f,
                        QualityTier.Rare => 0.03f,
                        QualityTier.Epic => 0.06f,
                        QualityTier.Legendary => 0.10f,
                        _ => throw new NotImplementedException()
                    };

                    BFGQualityController qualityController = qualityProjectilePrefab.AddComponent<BFGQualityController>();
                    qualityController.DamageBonusCoefficientPerSecond = damageBonusCoefficientPerSecond;

                    _qualityProjectilePrefabs[(int)qualityTier] = qualityProjectilePrefab;
                }

                args.ContentPack.projectilePrefabs.Add(_qualityProjectilePrefabs);
            });

            return bfgProjectilePrefabLoad.AsProgressCoroutine(args.ProgressReceiver);
        }

        private static readonly FixedConditionalWeakTable<EquipmentSlot, EquipmentSlotBFGQualityInfo> _equipmentSlotQualityInfoLookup = new FixedConditionalWeakTable<EquipmentSlot, EquipmentSlotBFGQualityInfo>();

        private sealed class EquipmentSlotBFGQualityInfo
        {
            public QualityTier QualityTier = QualityTier.None;
        }

        [SystemInitializer]
        private static void Init()
        {
            On.RoR2.EquipmentSlot.FireBfg += EquipmentSlot_FireBfg;
            IL.RoR2.EquipmentSlot.MyFixedUpdate += EquipmentSlot_MyFixedUpdate;
        }

        private static bool EquipmentSlot_FireBfg(On.RoR2.EquipmentSlot.orig_FireBfg orig, EquipmentSlot self)
        {
            bool success = orig(self);

            QualityTier qualityTier = QualityTier.None;
            if (success)
            {
                qualityTier = self.GetCurrentEquipmentActionQualityTier();
            }

            if (qualityTier > QualityTier.None)
            {
                EquipmentSlotBFGQualityInfo qualityInfo = _equipmentSlotQualityInfoLookup.GetOrCreateValue(self);
                qualityInfo.QualityTier = qualityTier;
            }
            else
            {
                _equipmentSlotQualityInfoLookup.Remove(self);
            }

            return success;
        }

        private static void EquipmentSlot_MyFixedUpdate(ILContext il)
        {
            ILCursor c = new ILCursor(il);

            if (!c.TryGotoNext(MoveType.After,
                               x => x.MatchLdstr("Prefabs/Projectiles/BeamSphere"),
                               x => x.MatchCallOrCallvirt(typeof(LegacyResourcesAPI), nameof(LegacyResourcesAPI.Load))))
            {
                Log.Error("Failed to find patch location");
                return;
            }

            c.Emit(OpCodes.Ldarg_0);
            c.EmitDelegate<Func<GameObject, EquipmentSlot, GameObject>>(getProjectilePrefab);

            static GameObject getProjectilePrefab(GameObject prefab, EquipmentSlot equipmentSlot)
            {
                if (_equipmentSlotQualityInfoLookup.Remove(equipmentSlot, out EquipmentSlotBFGQualityInfo qualityInfo))
                {
                    QualityTier qualityTier = qualityInfo.QualityTier;
                    if (qualityTier > QualityTier.None)
                    {
                        GameObject qualityPrefab = ArrayUtils.GetSafe(_qualityProjectilePrefabs, (int)qualityTier);
                        if (qualityPrefab)
                        {
                            prefab = qualityPrefab;
                        }
                    }
                }

                return prefab;
            }
        }
    }
}
