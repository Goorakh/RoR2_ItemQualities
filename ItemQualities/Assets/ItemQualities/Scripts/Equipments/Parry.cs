using HG;
using ItemQualities.Utilities.Extensions;
using RoR2;
using RoR2.Projectile;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using VultureHunter;

namespace ItemQualities.Equipments
{
    internal static class Parry
    {
        private static int[] _projectileIndexConversions = Array.Empty<int>();

        [SystemInitializer(typeof(ProjectileCatalog))]
        private static void Init()
        {
            int projectileCount = ProjectileCatalog.projectilePrefabCount;

            HashSet<int>[] spawnedByProjectileIndicesMap = new HashSet<int>[projectileCount];

            for (int projectileIndex = 0; projectileIndex < projectileCount; projectileIndex++)
            {
                GameObject projectilePrefab = ProjectileCatalog.GetProjectilePrefab(projectileIndex);

                void tryRecordChildProjectile(GameObject childProjectilePrefab)
                {
                    int childProjectileIndex = ProjectileCatalog.GetProjectileIndex(childProjectilePrefab);
                    if (childProjectileIndex != -1)
                    {
                        HashSet<int> spawnedByProjectiles = spawnedByProjectileIndicesMap[childProjectileIndex] ??= SetPool<int>.RentCollection();
                        spawnedByProjectiles.Add(projectileIndex);
                    }
                }

                if (projectilePrefab.TryGetComponent(out ProjectileFireChildren projectileFireChildren))
                {
                    tryRecordChildProjectile(projectileFireChildren.childProjectilePrefab);
                }

                if (projectilePrefab.TryGetComponent(out ProjectileExplosion projectileExplosion) &&
                    projectileExplosion.fireChildren)
                {
                    tryRecordChildProjectile(projectileExplosion.childrenProjectilePrefab);
                }

                if (projectilePrefab.TryGetComponent(out LunarStakesLightningController lunarStakesLightningController))
                {
                    tryRecordChildProjectile(lunarStakesLightningController.childProjectilePrefab);
                }

                if (projectilePrefab.TryGetComponent(out VultureHunterSpearController vultureHunterSpearController))
                {
                    tryRecordChildProjectile(vultureHunterSpearController.delayedProjectilePrefab);
                }
            }

            _projectileIndexConversions = new int[projectileCount];
            for (int projectileIndex = 0; projectileIndex < projectileCount; projectileIndex++)
            {
                int rootProjectileIndex = projectileIndex;

                // Walk up the spawn chain while there is only 1 projectile that spawns this one
                // If there are multiple projectiles that spawn this one, it is ambiguous which one any given projectile is created by, so stop at the "highest point" that leads to spawning only this projectile
                while (spawnedByProjectileIndicesMap[rootProjectileIndex]?.Count == 1)
                {
                    rootProjectileIndex = spawnedByProjectileIndicesMap[rootProjectileIndex].First();
                }

                if (rootProjectileIndex != projectileIndex)
                {
                    Log.Debug($"Determined root projectile {ProjectileCatalog.GetProjectilePrefab(rootProjectileIndex)} for {ProjectileCatalog.GetProjectilePrefab(projectileIndex)}");
                }

                _projectileIndexConversions[projectileIndex] = rootProjectileIndex;
            }

            for (int i = 0; i < spawnedByProjectileIndicesMap.Length; i++)
            {
                ref HashSet<int> spawnedByProjectilesSet = ref spawnedByProjectileIndicesMap[i];
                if (spawnedByProjectilesSet != null)
                {
                    spawnedByProjectilesSet = SetPool<int>.ReturnCollection(spawnedByProjectilesSet);
                }
            }

            On.RoR2.HealthComponent.ProcParry += HealthComponent_ProcParry;
            On.RoR2.EquipmentSlot.FireParry += EquipmentSlot_FireParry;
        }

        private static void HealthComponent_ProcParry(On.RoR2.HealthComponent.orig_ProcParry orig, HealthComponent self, DamageInfo damageInfo)
        {
            orig(self, damageInfo);

            int parriedProjectileIndex = -1;
            float parriedProjectileDamage = 0f;
            if (damageInfo.inflictor)
            {
                if (damageInfo.inflictor.TryGetComponent(out ProjectileController inflictorProjectileController))
                {
                    parriedProjectileIndex = inflictorProjectileController.catalogIndex;
                }

                if (damageInfo.inflictor.TryGetComponent(out ProjectileDamage projectileDamage))
                {
                    parriedProjectileDamage = projectileDamage.damage;
                }
            }

            if (ArrayUtils.IsInBounds(_projectileIndexConversions, parriedProjectileIndex))
            {
                parriedProjectileIndex = _projectileIndexConversions[parriedProjectileIndex];
            }

            if (parriedProjectileIndex != -1)
            {
                Log.Debug($"{Util.GetBestBodyName(self.gameObject)} parried {ProjectileCatalog.GetProjectilePrefab(parriedProjectileIndex)} from {Util.GetBestBodyName(damageInfo.attacker)}");
            }

            if (self.body && self.body.TryGetComponentCached(out CharacterBodyExtraStatsTracker bodyExtraStats))
            {
                bodyExtraStats.ParryStoredProjectileIndex = parriedProjectileIndex;
                bodyExtraStats.ParryStoredProjectileAttackerBodyIndex = parriedProjectileIndex != -1 ? BodyCatalog.FindBodyIndex(damageInfo.attacker) : BodyIndex.None;
                bodyExtraStats.ParryStoredProjectileDamage = parriedProjectileDamage;
                bodyExtraStats.ParryStoredProjectileCrit = damageInfo.crit;
            }
        }

        private static bool EquipmentSlot_FireParry(On.RoR2.EquipmentSlot.orig_FireParry orig, EquipmentSlot self)
        {
            int storedProjectileIndex = -1;
            float storedProjectileDamage = 0f;
            bool storedProjectileCrit = false;
            if (self.characterBody && self.characterBody.TryGetComponentCached(out CharacterBodyExtraStatsTracker bodyExtraStats))
            {
                if (bodyExtraStats.ParryStoredProjectileIndex != -1)
                {
                    storedProjectileIndex = bodyExtraStats.ParryStoredProjectileIndex;
                    storedProjectileDamage = bodyExtraStats.ParryStoredProjectileDamage;
                    storedProjectileCrit = bodyExtraStats.ParryStoredProjectileCrit;

                    bodyExtraStats.ParryStoredProjectileIndex = -1;
                    bodyExtraStats.ParryStoredProjectileAttackerBodyIndex = BodyIndex.None;
                    bodyExtraStats.ParryStoredProjectileDamage = 0f;
                    bodyExtraStats.ParryStoredProjectileCrit = false;
                }
            }

            QualityTier qualityTier = self.GetCurrentEquipmentActionQualityTier();
            if (qualityTier > QualityTier.None && storedProjectileIndex != -1)
            {
                float damageCoefficient;
                switch (qualityTier)
                {
                    case QualityTier.Uncommon:
                        damageCoefficient = 1.5f;
                        break;
                    case QualityTier.Rare:
                        damageCoefficient = 2.5f;
                        break;
                    case QualityTier.Epic:
                        damageCoefficient = 4f;
                        break;
                    case QualityTier.Legendary:
                        damageCoefficient = 10f;
                        break;
                    default:
                        damageCoefficient = 1f;
                        Log.Warning($"Quality tier {qualityTier} is not implemented");
                        break;
                }

                Ray aimRay = self.characterBody.inputBank.GetAimRay();

                GameObject projectilePrefab = ProjectileCatalog.GetProjectilePrefab(storedProjectileIndex);

                FireProjectileInfo fireProjectileInfo = new FireProjectileInfo
                {
                    projectilePrefab = projectilePrefab,
                    position = aimRay.origin,
                    rotation = Util.QuaternionSafeLookRotation(aimRay.direction),
                    owner = self.gameObject,
                    damage = storedProjectileDamage * damageCoefficient,
                    crit = storedProjectileCrit || self.characterBody.RollCrit(),
                    damageColorIndex = DamageColorIndex.Item,
                };

                if (!projectilePrefab.TryGetComponent(out ProjectileDamage projectileDamage) ||
                    (projectileDamage.damageType.damageSource & DamageSource.SkillMask) == 0)
                {
                    // In order for this projectile to behave like a normal survivor attack it needs a skill damage source, so just pretend its a primary and hopefully this doesn't cause any unintended side effects :)
                    // This will not override the projectile's set damage type, only the DamageSource
                    fireProjectileInfo.damageTypeOverride = DamageSource.Primary;
                }

                ProjectileManager.instance.FireProjectile(fireProjectileInfo);
            }

            return orig(self);
        }
    }
}
