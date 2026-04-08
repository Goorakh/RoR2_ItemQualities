using RoR2;
using System;
using UnityEngine;
using UnityEngine.Networking;

namespace EntityStates.FriendUnit
{
    public sealed class FriendUnitPunt : BaseState
    {
        public static float BaseVelocity;

        public static float MaxLockOnAngle;

        public static float MaxDistance;

        public static float LaunchHorizontalSpeedMultiplier;

        public static string PuntSound;

        [NonSerialized]
        public GameObject Punter;

        [NonSerialized]
        public Ray AimRay;

        public override void OnSerialize(NetworkWriter writer)
        {
            base.OnSerialize(writer);
            writer.Write(Punter);
            writer.Write(AimRay);
        }

        public override void OnDeserialize(NetworkReader reader)
        {
            base.OnDeserialize(reader);
            Punter = reader.ReadGameObject();
            AimRay = reader.ReadRay();
        }

        public override void OnEnter()
        {
            if (isAuthority)
            {
                characterBody.isSprinting = true;
            }

            base.OnEnter();

            if (isAuthority)
            {
                CharacterBody punterBody = Punter ? Punter.GetComponent<CharacterBody>() : null;

                BullseyeSearch bullseyeSearch = new BullseyeSearch
                {
                    searchOrigin = AimRay.origin,
                    searchDirection = AimRay.direction,
                    viewer = punterBody,
                    filterByLoS = true,
                    filterByDistinctEntity = true,
                    maxAngleFilter = MaxLockOnAngle,
                    maxDistanceFilter = MaxDistance,
                    sortMode = BullseyeSearch.SortMode.Angle,
                    queryTriggerInteraction = QueryTriggerInteraction.Ignore,
                    teamMaskFilter = TeamMask.GetEnemyTeams(TeamComponent.GetObjectTeam(Punter))
                };

                bullseyeSearch.RefreshCandidates();

                IPhysMotor punterMotor = null;
                if (punterBody)
                {
                    punterMotor = punterBody.characterMotor ? punterBody.characterMotor : punterBody.GetComponent<IPhysMotor>();
                }

                Vector3 punterVelocity = Vector3.zero;
                if (punterMotor != null)
                {
                    punterVelocity = punterMotor.velocity;
                }

                Vector3 punterHorizontalVelocity = punterVelocity;
                punterHorizontalVelocity.y = 0f;

                float puntSpeed = punterHorizontalVelocity.magnitude + BaseVelocity + (moveSpeedStat * LaunchHorizontalSpeedMultiplier);

                Vector3 puntVelocity = AimRay.direction * puntSpeed;

                foreach (HurtBox hurtBox in bullseyeSearch.GetResults())
                {
                    puntVelocity = Trajectory.CalculateInitialVelocityFromHSpeed(transform.position, hurtBox.transform.position, puntSpeed);
                    break;
                }

                characterMotor.ApplyForceImpulse(new PhysForceInfo
                {
                    force = puntVelocity,
                    massIsOne = true,
                    disableAirControlUntilCollision = true,
                    resetVelocity = true,
                    ignoreGroundStick = true
                });

                characterMotor.onMovementHit += onMovementHit;
            }

            Util.PlaySound(PuntSound, gameObject);
        }

        public override void OnExit()
        {
            base.OnExit();

            if (isAuthority)
            {
                characterBody.isSprinting = false;

                characterMotor.onMovementHit -= onMovementHit;
            }
        }

        public override void FixedUpdate()
        {
            base.FixedUpdate();

            if (isAuthority)
            {
                characterBody.isSprinting = true;
            }
        }

        void onMovementHit(ref CharacterMotor.MovementHitInfo movementHitInfo)
        {
            outer.SetNextState(new FriendUnitPuntImpact
            {
                Punter = Punter,
                ImpactPoint = movementHitInfo.hitPoint,
                ImpactNormal = movementHitInfo.hitNormal,
                ImpactVelocity = movementHitInfo.velocity,
                DamageMultiplierFromSpeed = Mathf.Max(1f, movementHitInfo.velocity.magnitude / moveSpeedStat),
            });
        }

        public override InterruptPriority GetMinimumInterruptPriority()
        {
            return InterruptPriority.Frozen;
        }
    }
}
