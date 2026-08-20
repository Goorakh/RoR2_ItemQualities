using ItemQualities;
using ItemQualities.Items;
using RoR2;
using UnityEngine;
using UnityEngine.Networking;

namespace EntityStates.VagrantNovaItemQualityStandalone
{
    public sealed class DetonateState : BaseVagrantNovaItemQualityStandaloneState
    {
        private float duration;

        public override void OnEnter()
        {
            base.OnEnter();

            duration = VagrantNovaItemQuality.DetonateState.baseDuration;

            float radius = ExplodeOnDeath.GetExplosionRadius(VagrantNovaItemQuality.DetonateState.blastRadius * delayBlast.procCoefficient, attachedBody);

            if (NetworkServer.active && attachedBody)
            {
                ItemQualityCounts itemCounts = GetItemCounts();
                if (itemCounts.TotalQualityCount == 0)
                {
                    itemCounts.UncommonCount = 1;
                }

                new BlastAttack
                {
                    attacker = attachedBody.gameObject,
                    baseDamage = attachedBody.damage * NovaOnLowHealth.GetMiniNovaDamageCoefficient(itemCounts),
                    baseForce = VagrantNovaItemQuality.DetonateState.blastForce,
                    bonusForce = Vector3.zero,
                    attackerFiltering = AttackerFiltering.NeverHitSelf,
                    crit = attachedBody.RollCrit(),
                    damageColorIndex = DamageColorIndex.Item,
                    damageType = DamageTypeExtended.Electrical,
                    falloffModel = BlastAttack.FalloffModel.None,
                    inflictor = gameObject,
                    position = transform.position,
                    procChainMask = delayBlast.procChainMask,
                    procCoefficient = VagrantNovaItemQuality.DetonateState.blastProcCoefficient * delayBlast.procCoefficient,
                    radius = radius,
                    losType = BlastAttack.LoSType.NearestHit,
                    teamIndex = attachedBody.teamComponent.teamIndex,
                }.Fire();
            }

            EffectData effectData = new EffectData
            {
                origin = transform.position,
                scale = radius,
            };

            EffectManager.SpawnEffect(VagrantNovaItemQuality.DetonateState.explosionEffectPrefab, effectData, false);

            Util.PlaySound(VagrantNovaItemQuality.DetonateState.explosionSound, gameObject);
        }

        public override void OnExit()
        {
            base.OnExit();

            if (NetworkServer.active)
            {
                Destroy(gameObject);
            }
        }

        public override void FixedUpdate()
        {
            base.FixedUpdate();
            if (isAuthority && fixedAge >= duration)
            {
                outer.SetNextState(new Idle());
            }
        }
    }
}
