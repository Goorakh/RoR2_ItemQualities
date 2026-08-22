using ItemQualities.Utilities.Extensions;
using RoR2;
using RoR2.Projectile;
using UnityEngine;
using UnityEngine.Networking;

namespace ItemQualities.Equipments
{
    [RequireComponent(typeof(ProjectileController))]
    [RequireComponent(typeof(ProjectileTargetComponent))]
    [RequireComponent(typeof(ProjectileImpactExplosion))]
    [RequireComponent(typeof(ProjectileProximityBeamController))]
    public sealed class BFGQualityController : MonoBehaviour
    {
        public float DamageBonusCoefficientPerSecond;

        private ProjectileController _projectileController;
        private ProjectileTargetComponent _projectileTargetComponent;
        private ProjectileImpactExplosion _projectileImpactExplosion;
        private ProjectileProximityBeamController _projectileProximityBeamController;

        private CharacterBodyExtraStatsTracker _ownerBodyStats;

        private float _blastDamageCoefficientIncreasePerSecond;
        private float _zapDamageCoefficientIncreasePerSecond;

        private void Awake()
        {
            _projectileController = GetComponent<ProjectileController>();
            _projectileTargetComponent = GetComponent<ProjectileTargetComponent>();
            _projectileImpactExplosion = GetComponent<ProjectileImpactExplosion>();
            _projectileProximityBeamController = GetComponent<ProjectileProximityBeamController>();
        }

        private void Start()
        {
            if (_projectileController.owner && _projectileController.owner.TryGetComponentCached(out CharacterBodyExtraStatsTracker ownerBodyStats))
            {
                if (NetworkServer.active)
                {
                    ownerBodyStats.OnDamageDealtServer += onDamageDealtServer;
                }

                _ownerBodyStats = ownerBodyStats;

                if (ownerBodyStats.LastHitBody && ownerBodyStats.LastHitBody.healthComponent.alive)
                {
                    _projectileTargetComponent.target = ownerBodyStats.LastHitBody.coreTransform;
                }
            }

            _blastDamageCoefficientIncreasePerSecond = DamageBonusCoefficientPerSecond * _projectileImpactExplosion.blastDamageCoefficient;
            _zapDamageCoefficientIncreasePerSecond = DamageBonusCoefficientPerSecond * _projectileProximityBeamController.damageCoefficient;
        }

        private void OnDestroy()
        {
            if (!ReferenceEquals(_ownerBodyStats, null))
            {
                _ownerBodyStats.OnDamageDealtServer -= onDamageDealtServer;
            }
        }

        private void FixedUpdate()
        {
            _projectileImpactExplosion.blastDamageCoefficient += _blastDamageCoefficientIncreasePerSecond * Time.fixedDeltaTime;
            _projectileProximityBeamController.damageCoefficient += _zapDamageCoefficientIncreasePerSecond * Time.fixedDeltaTime;
        }

        private void onDamageDealtServer(DamageReport damageReport)
        {
            DamageInfo damageInfo = damageReport.damageInfo;
            if (damageInfo.damageType.IsDamageSourceSkillBased)
            {
                Transform targetTransform = damageInfo.inflictedHurtbox ? damageInfo.inflictedHurtbox.transform : null;
                if (!targetTransform)
                {
                    if (damageReport.victimBody)
                    {
                        targetTransform = damageReport.victimBody.mainHurtBox ? damageReport.victimBody.mainHurtBox.transform : damageReport.victimBody.coreTransform;
                    }
                }

                if (targetTransform)
                {
                    _projectileTargetComponent.target = targetTransform;
                }
            }
        }
    }
}
