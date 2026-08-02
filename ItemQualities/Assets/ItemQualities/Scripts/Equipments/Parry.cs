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
        }

        private static void HealthComponent_ProcParry(On.RoR2.HealthComponent.orig_ProcParry orig, HealthComponent self, DamageInfo damageInfo)
        {
            orig(self, damageInfo);

            int parriedProjectileIndex = -1;
            float parriedProjectileDamage = 0f;
            float parriedProjectileForce = 0f;
            if (damageInfo.inflictor)
            {
                if (damageInfo.inflictor.TryGetComponent(out ProjectileController inflictorProjectileController))
                {
                    parriedProjectileIndex = inflictorProjectileController.catalogIndex;
                }

                if (damageInfo.inflictor.TryGetComponent(out ProjectileDamage projectileDamage))
                {
                    parriedProjectileDamage = projectileDamage.damage;
                    parriedProjectileForce = projectileDamage.force;
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

            if (self.body && self.body.master && self.body.master.TryGetComponentCached(out CharacterMasterExtraStatsTracker masterStats))
            {
                if (parriedProjectileIndex != -1)
                {
                    masterStats.ParryStoredProjectileInfo = new ParryStoredProjectileInfo
                    {
                        ProjectileIndex = parriedProjectileIndex,
                        Damage = parriedProjectileDamage,
                        Crit = damageInfo.crit,
                        Force = parriedProjectileForce,
                        AttackerBodyIndex = BodyCatalog.FindBodyIndex(damageInfo.attacker),
                        QualityTier = self.body.inventory.GetActiveEquipmentQualityTier(),
                    };
                }
                else
                {
                    masterStats.ParryStoredProjectileInfo = ParryStoredProjectileInfo.None;
                }
            }
        }
    }

    public sealed class ParryQualityEquipmentBehavior : QualityEquipmentBodyBehavior
    {
        [EquipmentGroupAssociation(QualityEquipmentBehaviorUsageFlags.Authority, AllowOffhand = true)]
        private static EquipmentQualityGroup GetEquipmentGroup() => ItemQualitiesContent.EquipmentQualityGroups.Parry;

        private CharacterMasterExtraStatsTracker _masterStats;

        private bool _skillOverrideActive;

        protected override void Awake()
        {
            base.Awake();
            _masterStats = BodyStats ? BodyStats.MasterExtraStatsTracker : null;
        }

        private void OnEnable()
        {
            if (!ReferenceEquals(_masterStats, null))
            {
                _masterStats.OnParryStoredProjectileInfoChanged += onParryStoredProjectileInfoChanged;
            }

            Body.onSkillActivatedAuthority += onSkillActivatedAuthority;

            refreshSkillOverride();
        }

        private void OnDisable()
        {
            if (!ReferenceEquals(_masterStats, null))
            {
                _masterStats.OnParryStoredProjectileInfoChanged -= onParryStoredProjectileInfoChanged;
            }

            Body.onSkillActivatedAuthority -= onSkillActivatedAuthority;

            trySetSkillOverride(false);
        }

        private void onSkillActivatedAuthority(GenericSkill skill)
        {
            if (ReferenceEquals(Body.skillLocator.primary, skill))
            {
                // While the client waits for the parried projectile info to be reset on their end, don't let the override skill execute more than once.
                trySetSkillOverride(false);
            }
        }

        private void onParryStoredProjectileInfoChanged(CharacterMasterExtraStatsTracker masterStats)
        {
            refreshSkillOverride();
        }

        private void refreshSkillOverride()
        {
            trySetSkillOverride(_masterStats && _masterStats.ParryStoredProjectileInfo.ProjectileIndex != -1);
        }

        private void trySetSkillOverride(bool enabled)
        {
            if (_skillOverrideActive == enabled)
            {
                return;
            }

            GenericSkill primarySkill = Body.skillLocator.primary;
            if (enabled)
            {
                if (!ReferenceEquals(primarySkill, null))
                {
                    primarySkill.SetSkillOverride(this, ItemQualitiesContent.SkillDefs.ParryProjectileSkill, GenericSkill.SkillOverridePriority.Contextual);
                }
                else
                {
                    // There is no primary skill, therefore we don't have an override, despite what the argument says
                    enabled = false;
                }
            }
            else
            {
                if (!ReferenceEquals(primarySkill, null))
                {
                    primarySkill.UnsetSkillOverride(this, ItemQualitiesContent.SkillDefs.ParryProjectileSkill, GenericSkill.SkillOverridePriority.Contextual);
                }
            }

            _skillOverrideActive = enabled;
        }
    }
}
