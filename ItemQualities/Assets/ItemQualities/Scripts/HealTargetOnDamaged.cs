using RoR2;
using RoR2.Orbs;
using UnityEngine;

namespace ItemQualities
{
    [RequireComponent(typeof(HealthComponent))]
    public sealed class HealTargetOnDamaged : MonoBehaviour, IOnIncomingDamageServerReceiver, IOnTakeDamageServerReceiver
    {
        public CharacterBody HealTarget;

        [Min(0)]
        public float DamageToHealingConversionRate = 0.5f;

        HealthComponent _healthComponent;

        void Awake()
        {
            _healthComponent = GetComponent<HealthComponent>();
        }

        void IOnIncomingDamageServerReceiver.OnIncomingDamageServer(DamageInfo damageInfo)
        {
            if (HealTarget && damageInfo.attacker == HealTarget.gameObject)
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
                HurtBox targetHurtBox = HealTarget ? HealTarget.mainHurtBox : null;
                if (targetHurtBox)
                {
                    OrbManager.instance.AddOrb(new HealOrb
                    {
                        origin = damageReport.damageInfo.position,
                        target = HealTarget.mainHurtBox,
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
