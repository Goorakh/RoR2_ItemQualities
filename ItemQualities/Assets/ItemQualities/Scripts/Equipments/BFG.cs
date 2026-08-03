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
                if (!projectilePrefab.TryGetComponent(out ProjectileController projectileController))
                {
                    Log.Warning($"Expected ProjectileController component on {projectilePrefab}");
                    return;
                }

                if (!projectilePrefab.TryGetComponent(out ProjectileImpactExplosion projectileExplosion))
                {
                    Log.Warning($"Expected ProjectileImpactExplosion component on {projectilePrefab}");
                    return;
                }

                float baseBlastRadius = projectileExplosion.blastRadius;

                GameObject qualityProjectileGhostPrefab = EffectScalingFixer.CreateFixedScalingCopy(projectileController.ghostPrefab, 1f);
                qualityProjectileGhostPrefab.name = "Quality" + qualityProjectileGhostPrefab.name;
                if (qualityProjectileGhostPrefab.TryGetComponent(out ProjectileGhostController qualityProjectileGhostController))
                {
                    qualityProjectileGhostController.inheritScaleFromProjectile = true;
                }
                else
                {
                    Log.Warning($"Expected ProjectileGhostController component on {qualityProjectileGhostPrefab}");
                }

                GameObject qualityProjectileImpactEffect = EffectScalingFixer.CreateFixedScalingCopy(projectileExplosion.impactEffect, baseBlastRadius);
                qualityProjectileImpactEffect.name = "Quality" + qualityProjectileImpactEffect.name;

                args.ContentPack.effectDefs.Add(new EffectDef(qualityProjectileImpactEffect));

                for (QualityTier qualityTier = 0; qualityTier < QualityTier.Count; qualityTier++)
                {
                    float blastRadiusIncrease = qualityTier switch
                    {
                        QualityTier.Uncommon => 10f,
                        QualityTier.Rare => 20f,
                        QualityTier.Epic => 40f,
                        QualityTier.Legendary => 50f,
                        _ => throw new NotImplementedException($"Quality tier {qualityTier} is not implemented")
                    };

                    float scaleMultiplier = (baseBlastRadius + blastRadiusIncrease) / baseBlastRadius;

                    GameObject qualityProjectilePrefab = projectilePrefab.InstantiateClone(projectilePrefab.name + qualityTier.ToString());
                    qualityProjectilePrefab.transform.localScale *= scaleMultiplier;

                    // Original prefab is already checked for this component, so no need to check it on the clone
                    ProjectileController qualityProjectileController = qualityProjectilePrefab.GetComponent<ProjectileController>();
                    qualityProjectileController.ghostPrefab = qualityProjectileGhostPrefab;

                    ProjectileImpactExplosion qualityProjectileExplosion = qualityProjectilePrefab.GetComponent<ProjectileImpactExplosion>();
                    qualityProjectileExplosion.blastRadius += blastRadiusIncrease;
                    qualityProjectileExplosion.impactEffect = qualityProjectileImpactEffect;

                    if (qualityProjectilePrefab.TryGetComponent(out ProjectileProximityBeamController qualityProjectileBeamController))
                    {
                        qualityProjectileBeamController.attackRange += blastRadiusIncrease;
                    }
                    else
                    {
                        Log.Warning($"Expected ProjectileProximityBeamController component on {qualityProjectilePrefab}");
                    }

                    ProjectileSimple projectileSimple = qualityProjectilePrefab.GetComponent<ProjectileSimple>();
                    projectileSimple.updateAfterFiring = true;

                    ProjectileSteerTowardTarget projectileSteerTowardTarget = qualityProjectilePrefab.AddComponent<ProjectileSteerTowardTarget>();
                    projectileSteerTowardTarget.rotationSpeed = qualityTier switch
                    {
                        QualityTier.Uncommon => 10f,
                        QualityTier.Rare => 15f,
                        QualityTier.Epic => 25f,
                        QualityTier.Legendary => 30f,
                        _ => throw new NotImplementedException($"Quality tier {qualityTier} is not implemented")
                    };

                    ProjectileDirectionalTargetFinder projectileDirectionalTargetFinder = qualityProjectilePrefab.AddComponent<ProjectileDirectionalTargetFinder>();
                    projectileDirectionalTargetFinder.lookRange = 600f;
                    projectileDirectionalTargetFinder.lookCone = 180f;
                    projectileDirectionalTargetFinder.targetSearchInterval = 1f;
                    projectileDirectionalTargetFinder.onlySearchIfNoTarget = true;
                    projectileDirectionalTargetFinder.allowTargetLoss = false;
                    projectileDirectionalTargetFinder.testLoS = true;

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

            Instruction fireBfgProjectileStartInstruction = c.Next;

            if (c.TryGotoNext(MoveType.Before,
                              x => x.MatchCallOrCallvirt<ProjectileManager>(nameof(ProjectileManager.FireProjectileWithoutDamageType))) &&
                c.TryGotoPrev(MoveType.After,
                              x => x.MatchLdnull()) && c.IsAfter(fireBfgProjectileStartInstruction))
            {
                c.Emit(OpCodes.Ldarg_0);
                c.EmitDelegate<Func<GameObject, EquipmentSlot, GameObject>>(getTarget);

                static GameObject getTarget(GameObject target, EquipmentSlot equipmentSlot)
                {
                    if (equipmentSlot &&
                        equipmentSlot.TryGetComponentCached(out CharacterBodyExtraStatsTracker bodyExtraStats) &&
                        bodyExtraStats.LastHitBody)
                    {
                        target = bodyExtraStats.LastHitBody.gameObject;
                    }

                    return target;
                }
            }
            else
            {
                Log.Error("Failed to find target patch location");
            }
        }
    }
}
