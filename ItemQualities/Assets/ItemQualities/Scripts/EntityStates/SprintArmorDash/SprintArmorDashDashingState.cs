using HG;
using ItemQualities;
using ItemQualities.Utilities.Extensions;
using RoR2;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

namespace EntityStates.SprintArmorDash
{
    public sealed class SprintArmorDashDashingState : EntityState
    {
        private static readonly SphereSearch _dashSphereSearch = new SphereSearch();

        private static EffectIndex _blinkEffectIndex;

        private CharacterBody _attachedBody;
        private IPhysMotor _motor;

        private Vector3 _dashDirection;
        private bool _stoppedDash;

        [SystemInitializer(typeof(EffectCatalogUtils))]
        private static void Init()
        {
            _blinkEffectIndex = EffectCatalogUtils.FindEffectIndex("HuntressBlinkEffect");
            if (_blinkEffectIndex == EffectIndex.Invalid)
            {
                Log.Warning("Failed to find blink effect index");
            }
        }

        public override void OnEnter()
        {
            base.OnEnter();

            NetworkedBodyAttachment networkedBodyAttachment = GetComponent<NetworkedBodyAttachment>();
            if (!networkedBodyAttachment || !networkedBodyAttachment.attachedBody)
                return;

            _attachedBody = networkedBodyAttachment.attachedBody;
            _dashDirection = _attachedBody.inputBank.aimDirection;
            _motor = _attachedBody.characterMotor ? _attachedBody.characterMotor : _attachedBody.GetComponent<IPhysMotor>();

            if (isAuthority)
            {
                _attachedBody.isSprinting = true;
            }

            if (NetworkServer.active)
            {
                ItemQualityCounts sprintArmor = _attachedBody.inventory.GetItemCountsEffective(ItemQualitiesContent.ItemQualityGroups.SprintArmor);

                int cooldown = sprintArmor.HighestQuality switch
                {
                    QualityTier.Uncommon => 20,
                    QualityTier.Rare => 15,
                    QualityTier.Epic => 10,
                    QualityTier.Legendary => 5,
                    _ => 30,
                };

                for (int i = 0; i < cooldown; i++)
                {
                    _attachedBody.AddTimedBuff(ItemQualitiesContent.Buffs.SprintArmorDashCooldown, i);
                }

                _attachedBody.AddBuff(JunkContent.Buffs.IgnoreFallDamage);
            }

            EffectData effectData = new EffectData();
            effectData.rotation = Util.QuaternionSafeLookRotation(_dashDirection);
            effectData.origin = _attachedBody.corePosition;
            EffectManager.SpawnEffect(_blinkEffectIndex, effectData, transmit: false);
        }

        public override void OnExit()
        {
            if (NetworkServer.active)
            {
                if (_attachedBody)
                {
                    _attachedBody.RemoveBuff(JunkContent.Buffs.IgnoreFallDamage);
                    _attachedBody.AddTimedBuff(JunkContent.Buffs.IgnoreFallDamage, 0.2f);
                }
            }

            base.OnExit();
        }

        public override void FixedUpdate()
        {
            base.FixedUpdate();

            if (!_attachedBody)
            {
                if (isAuthority)
                {
                    outer.SetNextStateToMain();
                }

                return;
            }

            if (NetworkServer.active)
            {
                _attachedBody.isSprinting = true;
            }

            if (_attachedBody.hasEffectiveAuthority)
            {
                if (_motor != null)
                {
                    if (fixedAge < 0.1f)
                    {
                        _motor.ApplyForceImpulse(new PhysForceInfo
                        {
                            resetVelocity = true,
                            force = _dashDirection * (Time.deltaTime * _attachedBody.moveSpeed * 1000),
                            ignoreGroundStick = true,
                            massIsOne = true,
                        });
                    }
                    else if (!_stoppedDash)
                    {
                        _stoppedDash = true;
                        _motor.ApplyForceImpulse(new PhysForceInfo
                        {
                            resetVelocity = true,
                            force = _dashDirection * (_attachedBody.moveSpeed * 3),
                            ignoreGroundStick = true,
                            massIsOne = true,
                        });
                    }
                }
            }

            if (isAuthority)
            {
                if (fixedAge > 0.2f)
                {
                    outer.SetNextStateToMain();
                }

                tryAttack();
            }
        }

        private void tryAttack()
        {
            using var _ = ListPool<HurtBox>.RentCollection(out List<HurtBox> hurtBoxes);

            _dashSphereSearch.origin = _attachedBody.corePosition;
            _dashSphereSearch.mask = LayerIndex.entityPrecise.mask;
            _dashSphereSearch.radius = _attachedBody.radius + 3;
            _dashSphereSearch.RefreshCandidates();
            _dashSphereSearch.FilterCandidatesByHurtBoxTeam(TeamMask.GetUnprotectedTeams(_attachedBody.teamComponent.teamIndex));
            _dashSphereSearch.FilterCandidatesByDistinctHurtBoxEntities();
            _dashSphereSearch.GetHurtBoxes(hurtBoxes);
            _dashSphereSearch.ClearCandidates();

            if (hurtBoxes.Count > 0)
            {
                SprintArmorDashBounce sprintArmorDashBounce = new SprintArmorDashBounce();
                sprintArmorDashBounce.attackPos = _attachedBody.corePosition;
                sprintArmorDashBounce.dashDirection = _dashDirection;
                outer.SetNextState(sprintArmorDashBounce);
            }
        }
    }
}
