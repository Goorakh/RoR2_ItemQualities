using ItemQualities.Items;
using RoR2;
using RoR2.Orbs;
using UnityEngine;

namespace ItemQualities.Orbs
{
    public sealed class LightningUpgradeOrb : GenericDamageOrb
    {
        public float baseBlastRadius = 3f;

        public override void Begin()
        {
            base.Begin();
            duration = 0.5f;
        }

        public override GameObject GetOrbEffect()
        {
            return OrbStorageUtility.Get("Prefabs/Effects/OrbEffects/LightningStrikeOrbEffect");
        }

        public override void OnArrival()
        {
            if (!target)
            {
                return;
            }

            CharacterBody attackerBody = attacker ? attacker.GetComponent<CharacterBody>() : null;

            float blastRadius = baseBlastRadius;

            blastRadius += ExplodeOnDeath.GetExplosionRadius(blastRadius, attackerBody);

            EffectManager.SpawnEffect(OrbStorageUtility.Get("Prefabs/Effects/ImpactEffects/LightningStrikeImpact"), new EffectData
            {
                origin = target.transform.position,
                scale = blastRadius,
            }, true);

            if (attacker)
            {
                BlastAttack blastAttack = new BlastAttack();
                blastAttack.attacker = attacker;
                blastAttack.baseDamage = damageValue;
                blastAttack.baseForce = 0f;
                blastAttack.bonusForce = Vector3.down * 1500f;
                blastAttack.crit = isCrit;
                blastAttack.damageColorIndex = DamageColorIndex.Item;
                blastAttack.falloffModel = BlastAttack.FalloffModel.None;
                blastAttack.inflictor = null;
                blastAttack.position = target.transform.position;
                blastAttack.procChainMask = procChainMask;
                blastAttack.procCoefficient = procCoefficient;
                blastAttack.radius = blastRadius;
                blastAttack.teamIndex = TeamComponent.GetObjectTeam(attacker);
                blastAttack.damageType.damageTypeExtended = DamageTypeExtended.Electrical;
                blastAttack.Fire();
            }
        }
    }
}
