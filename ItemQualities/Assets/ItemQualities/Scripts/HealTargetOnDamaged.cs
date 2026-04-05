using RoR2;
using RoR2.Orbs;
using UnityEngine;

namespace ItemQualities
{
    [RequireComponent(typeof(HealthComponent))]
    public sealed class HealTargetOnDamaged : MonoBehaviour, IOnIncomingDamageServerReceiver, IOnTakeDamageServerReceiver
    {
        [SerializeField]
        CharacterBody _healTarget;

        [Min(0)]
        public float DamageToHealingConversionRate = 0.5f;

        [Tooltip("The collisions manager to ignore collisions with the heal target, if set")]
        public IgnoredCollisionsProvider IgnoredCollisionsProvider;

        public CharacterBody HealTarget
        {
            get => _healTarget;
            set
            {
                if (_healTarget == value)
                    return;

                _healTarget = value;

                if (IgnoredCollisionsProvider && isActiveAndEnabled)
                {
                    refreshCollisionFilter();
                }
            }
        }

        HealthComponent _healthComponent;

        void Awake()
        {
            _healthComponent = GetComponent<HealthComponent>();
        }

        void OnEnable()
        {
            if (IgnoredCollisionsProvider)
            {
                refreshCollisionFilter();
            }
        }

        void OnDisable()
        {
            if (IgnoredCollisionsProvider)
            {
                IgnoredCollisionsProvider.CollisionWhitelistFilter = null;
            }
        }

        void refreshCollisionFilter()
        {
            if (IgnoredCollisionsProvider)
            {
                IgnoredCollisionsProvider.CollisionWhitelistFilter = _healTarget ? new TeamObjectFilter(_healTarget.teamComponent.teamIndex) { InvertFilter = true } : null;
            }
        }

        void IOnIncomingDamageServerReceiver.OnIncomingDamageServer(DamageInfo damageInfo)
        {
            if (_healTarget && damageInfo.attacker == _healTarget.gameObject)
            {
                damageInfo.rejected = true;
            }
        }

        void IOnTakeDamageServerReceiver.OnTakeDamageServer(DamageReport damageReport)
        {
            if (damageReport.damageInfo.rejected)
                return;

            float healAmount = damageReport.damageDealt * DamageToHealingConversionRate;
            if (healAmount > 0)
            {
                HurtBox targetHurtBox = _healTarget ? _healTarget.mainHurtBox : null;
                if (targetHurtBox)
                {
                    OrbManager.instance.AddOrb(new HealOrb
                    {
                        origin = damageReport.damageInfo.position,
                        target = targetHurtBox,
                        scaleOrb = true,
                        healValue = healAmount,
                    });
                }
            }

            // Restore HP so that damage can be taken again
            _healthComponent.Networkhealth = _healthComponent.fullHealth;
        }
    }
}
