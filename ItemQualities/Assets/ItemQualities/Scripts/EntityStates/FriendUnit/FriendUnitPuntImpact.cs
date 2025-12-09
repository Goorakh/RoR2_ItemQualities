using ItemQualities;
using ItemQualities.Items;
using RoR2;
using System;
using UnityEngine;
using UnityEngine.Networking;

namespace EntityStates.FriendUnit
{
    public sealed class FriendUnitPuntImpact : BaseState
    {
        static EffectIndex _explosionEffectIndex = EffectIndex.Invalid;

        [SystemInitializer(typeof(EffectCatalogUtils))]
        static void Init()
        {
            _explosionEffectIndex = EffectCatalogUtils.FindEffectIndex("OmniExplosionVFXRoboBallDeath");
            if (_explosionEffectIndex == EffectIndex.Invalid)
            {
                Log.Error("Failed to find explosion effect index");
            }
        }

        public static float ImpactBounciness;

        public static string ImpactSoundString;

        [NonSerialized]
        public GameObject Punter;

        [NonSerialized]
        public Vector3 ImpactNormal;

        [NonSerialized]
        public Vector3 ImpactVelocity;

        [NonSerialized]
        public float DamageMultiplierFromSpeed;

        public override void OnSerialize(NetworkWriter writer)
        {
            base.OnSerialize(writer);
            writer.Write(Punter);
            writer.Write(ImpactNormal);
            writer.Write(ImpactVelocity);
            writer.Write(DamageMultiplierFromSpeed);
        }

        public override void OnDeserialize(NetworkReader reader)
        {
            base.OnDeserialize(reader);
            Punter = reader.ReadGameObject();
            ImpactNormal = reader.ReadVector3();
            ImpactVelocity = reader.ReadVector3();
            DamageMultiplierFromSpeed = reader.ReadSingle();
        }

        public override void OnEnter()
        {
            base.OnEnter();

            if (NetworkServer.active)
            {
                CharacterMaster master = characterBody.master;

                ItemQualityCounts physicsProjectile = default;
                if (master && master.minionOwnership.ownerMaster)
                {
                    physicsProjectile = ItemQualitiesContent.ItemQualityGroups.PhysicsProjectile.GetItemCountsEffective(master.minionOwnership.ownerMaster.inventory);
                }

                if (physicsProjectile.TotalQualityCount == 0)
                    physicsProjectile.UncommonCount = 1;

                float damageCoefficient;
                switch (physicsProjectile.HighestQuality)
                {
                    case QualityTier.Uncommon:
                        damageCoefficient = 4f;
                        break;
                    case QualityTier.Rare:
                        damageCoefficient = 6f;
                        break;
                    case QualityTier.Epic:
                        damageCoefficient = 8f;
                        break;
                    case QualityTier.Legendary:
                        damageCoefficient = 10f;
                        break;
                    default:
                        Log.Error($"Quality tier {physicsProjectile.HighestQuality} is not implemented");
                        damageCoefficient = 1f;
                        break;
                }

                float blastRadius = (10f * physicsProjectile.UncommonCount) +
                                    (15f * physicsProjectile.RareCount) +
                                    (25f * physicsProjectile.EpicCount) +
                                    (35f * physicsProjectile.LegendaryCount);

                blastRadius = ExplodeOnDeath.GetExplosionRadius(blastRadius, characterBody);

                BlastAttack blastAttack = new BlastAttack
                {
                    position = characterBody.corePosition,
                    radius = blastRadius,
                    baseDamage = damageCoefficient * damageStat * DamageMultiplierFromSpeed,
                    damageType = new DamageTypeCombo(DamageType.Stun1s, DamageTypeExtended.Generic, DamageSource.Primary),
                    crit = RollCrit(),
                    attacker = gameObject,
                    inflictor = gameObject,
                    attackerFiltering = AttackerFiltering.NeverHitSelf,
                    damageColorIndex = DamageColorIndex.Item,
                    falloffModel = BlastAttack.FalloffModel.HalfLinear,
                    procCoefficient = 1f,
                    teamIndex = GetTeam()
                };

                blastAttack.Fire();

                if (_explosionEffectIndex != EffectIndex.Invalid)
                {
                    EffectManager.SpawnEffect(_explosionEffectIndex, new EffectData
                    {
                        origin = blastAttack.position,
                        scale = blastAttack.radius
                    }, true);
                }
            }

            Util.PlaySound(ImpactSoundString, gameObject);
            EffectManager.SimpleImpactEffect(KineticAura.knockbackEffectPrefab, characterBody.corePosition, -ImpactNormal, false);

            if (isAuthority)
            {
                if (ImpactVelocity == Vector3.zero)
                {
                    ImpactVelocity = characterMotor.velocity;
                }

                Vector3 newVelocity;
                if (ImpactNormal != Vector3.zero)
                {
                    Vector3 bounceDirection = Vector3.Reflect(ImpactVelocity.normalized, ImpactNormal);
                    float bounceVelocityMagnitude = ImpactVelocity.magnitude * ImpactBounciness;
                    newVelocity = bounceDirection * bounceVelocityMagnitude;
                }
                else
                {
                    newVelocity = characterMotor.velocity * -ImpactBounciness;
                }

                characterMotor.ApplyForceImpulse(new PhysForceInfo
                {
                    force = newVelocity,
                    resetVelocity = true,
                    disableAirControlUntilCollision = true,
                    ignoreGroundStick = true,
                    massIsOne = true,
                });

                outer.SetNextStateToMain();
            }
        }
    }
}
