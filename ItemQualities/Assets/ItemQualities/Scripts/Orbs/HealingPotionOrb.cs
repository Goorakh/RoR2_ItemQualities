using ItemQualities.Items;
using RoR2;
using RoR2.Orbs;
using UnityEngine;

namespace ItemQualities.Orbs
{
    public sealed class HealingPotionOrb : Orb
    {
        public float damageValue;

        public GameObject attacker;

        public TeamIndex teamIndex;

        public bool isCrit;

        public ProcChainMask procChainMask;

        public float procCoefficient = 0.2f;

        public DamageColorIndex damageColorIndex;

        public DamageTypeCombo damageType;

        public float radius;

        public float dotDamageMultiplier = 1f;

        public override void Begin()
        {
            base.Begin();

            duration = 0.6f;

            EffectData effectData = new EffectData
            {
                origin = origin,
                genericFloat = duration
            };

            effectData.SetHurtBoxReference(target);

            EffectManager.SpawnEffect(ItemQualitiesContent.Prefabs.HealingPotionOrbEffect, effectData, true);
        }

        public override void OnArrival()
        {
            base.OnArrival();

            if (!target)
            {
                return;
            }

            CharacterBody attackerBody = attacker ? attacker.GetComponent<CharacterBody>() : null;

            float blastRadius = ExplodeOnDeath.GetExplosionRadius(radius, attackerBody);

            // TODO: Explosion VFX

            BlastAttack.Result result = new BlastAttack
            {
                position = target.transform.position,
                radius = blastRadius,
                attacker = attacker,
                baseDamage = damageValue,
                crit = isCrit,
                damageType = damageType,
                procChainMask = procChainMask,
                procCoefficient = procCoefficient,
                damageColorIndex = damageColorIndex,
                teamIndex = teamIndex,
                attackerFiltering = AttackerFiltering.NeverHitSelf,
            }.Fire();

            for (int i = 0; i < result.hitCount; i++)
            {
                BlastAttack.HitPoint hitPoint = result.hitPoints[i];
                if (hitPoint.hurtBox && hitPoint.hurtBox.healthComponent)
                {
                    InflictDotInfo inflictDotInfo = new InflictDotInfo
                    {
                        victimObject = hitPoint.hurtBox.healthComponent.gameObject,
                        attackerObject = attacker,
                        dotIndex = HealingPotion.ChemicalBurnDotIndex,
                        duration = HealingPotion.ChemicalBurnDuration,
                        damageMultiplier = dotDamageMultiplier,
                        hitHurtBox = hitPoint.hurtBox,
                    };

                    DotController.InflictDot(ref inflictDotInfo);
                }
            }
        }
    }
}
