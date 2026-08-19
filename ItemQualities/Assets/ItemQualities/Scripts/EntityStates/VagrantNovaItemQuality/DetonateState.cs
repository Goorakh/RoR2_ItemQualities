using ItemQualities;
using ItemQualities.Items;
using RoR2;
using UnityEngine;
using UnityEngine.Networking;

namespace EntityStates.VagrantNovaItemQuality
{
    public sealed class DetonateState : BaseVagrantNovaItemQualityState
    {
        public static float blastRadius;

        public static float blastProcCoefficient;

        public static float blastForce;

        public static float baseDuration;

        public static string explosionSound;

        private float duration;

        public override void OnEnter()
        {
            base.OnEnter();

            duration = baseDuration;

            if (NetworkServer.active && attachedBody)
            {
                ItemQualityCounts itemCounts = GetItemCounts();
                if (itemCounts.TotalQualityCount == 0)
                {
                    itemCounts.UncommonCount = 1;
                }

                float damageCoefficient = (7f * itemCounts.UncommonCount) +
                                          (15f * itemCounts.RareCount) +
                                          (30f * itemCounts.EpicCount) +
                                          (60f * itemCounts.LegendaryCount);

                new BlastAttack
                {
                    attacker = attachedBody.gameObject,
                    baseDamage = attachedBody.damage * damageCoefficient,
                    baseForce = blastForce,
                    bonusForce = Vector3.zero,
                    attackerFiltering = AttackerFiltering.NeverHitSelf,
                    crit = attachedBody.RollCrit(),
                    damageColorIndex = DamageColorIndex.Item,
                    damageType = DamageTypeExtended.Electrical,
                    falloffModel = BlastAttack.FalloffModel.None,
                    inflictor = gameObject,
                    position = attachedBody.corePosition,
                    procChainMask = new ProcChainMask(),
                    procCoefficient = blastProcCoefficient,
                    radius = ExplodeOnDeath.GetExplosionRadius(blastRadius, attachedBody),
                    losType = BlastAttack.LoSType.NearestHit,
                    teamIndex = attachedBody.teamComponent.teamIndex,
                }.Fire();
            }

            EffectData effectData = new EffectData
            {
                origin = attachedBody ? attachedBody.corePosition : transform.position
            };

            if (attachedBody)
            {
                effectData.SetHurtBoxReference(attachedBody.mainHurtBox);
            }

            EffectManager.SpawnEffect(VagrantMonster.FireMegaNova.novaEffectPrefab, effectData, false);

            Util.PlaySound(explosionSound, gameObject);
        }

        public override void FixedUpdate()
        {
            base.FixedUpdate();
            if (isAuthority && fixedAge >= duration)
            {
                outer.SetNextStateToMain();
            }
        }
    }
}
