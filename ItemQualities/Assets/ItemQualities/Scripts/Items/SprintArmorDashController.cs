using ItemQualities.Utilities.Extensions;
using RoR2;
using RoR2BepInExPack.GameAssetPathsBetter;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace ItemQualities.Items
{
    [RequireComponent(typeof(NetworkedBodyAttachment))]
    public sealed class SprintArmorDashController : MonoBehaviour
    {
        static GameObject _blinkPrefab;
        static GameObject _hitEffectPrefab;

        NetworkedBodyAttachment _bodyAttachment;
        static readonly SphereSearch _dashSphereSearch = new SphereSearch();
        static readonly List<HurtBox> _dashHurtBoxBuffer = new List<HurtBox>();
        Vector3 _dashDirection;
        float _timer;
        bool _heldForward;
        State _state = State.idle;

        enum State {
            idle,
            startDash,
            dashing,
            endDash
        }

        [SystemInitializer]
        static void Init()
        {
            Addressables.LoadAssetAsync<GameObject>(RoR2_Base_Huntress.HuntressBlinkEffect_prefab).OnSuccess(prefab =>
            {
                _blinkPrefab = prefab;
            });
            Addressables.LoadAssetAsync<GameObject>(RoR2_Base_Common_VFX.OmniImpactVFX_prefab).OnSuccess(prefab =>
            {
                _hitEffectPrefab = prefab;
            });
        }

        void Awake()
        {
            _bodyAttachment = GetComponent<NetworkedBodyAttachment>();
        }

        private void FixedUpdate()
        {
            if (!_bodyAttachment || !_bodyAttachment.attachedBody)
                return;
            _timer += Time.deltaTime;
            switch (_state) {
                case State.idle:
                    idle();
                    break;
                case State.startDash:
                    startDash();
                    break;
                case State.dashing:
                    performDash();
                    break;
                case State.endDash:
                    endDash();
                    break;
            }
        }

        void switchState(State state)
        {
            _state = state;
            _timer = 0;
        }

        void idle()
        {
            Vector3 moveVector = _bodyAttachment.attachedBody.inputBank.moveVector;
            Vector3 aimVector = _bodyAttachment.attachedBody.inputBank.aimDirection;
            aimVector.y = 0;
            float angleDiff = Vector3.Angle(moveVector.normalized, aimVector);

            if (!_bodyAttachment.attachedBody.HasBuff(ItemQualitiesContent.Buffs.SprintArmorDashCooldown) &&
            angleDiff < 70 && moveVector.magnitude > 0.2)
            {
                if (!_heldForward)
                {
                    _heldForward = true;
                    if (_timer < 0.2f)
                    {
                        switchState(State.startDash);
                    }
                    else
                    {
                        _timer = 0f;
                    }
                }
            }
            else
            {
                _heldForward = false;
            }
        }

        void startDash()
        {
            switchState(State.dashing);
            _dashDirection = _bodyAttachment.attachedBody.inputBank.aimDirection;

            if (_bodyAttachment.attachedBody.hasAuthority)
            {
                _bodyAttachment.attachedBody.isSprinting = true;
                ItemQualityCounts sprintArmor = _bodyAttachment.attachedBody.inventory.GetItemCountsEffective(ItemQualitiesContent.ItemQualityGroups.SprintArmor);
                float cooldown = sprintArmor.HighestQuality switch
                {
                    QualityTier.Uncommon => 20,
                    QualityTier.Rare => 15,
                    QualityTier.Epic => 10,
                    QualityTier.Legendary => 5,
                    _ => 30,
                };
                _bodyAttachment.attachedBody.AddTimedBuffAuthority(ItemQualitiesContent.Buffs.SprintArmorDashCooldown.buffIndex, cooldown);
            }

            EffectData effectData = new EffectData();
            effectData.rotation = Util.QuaternionSafeLookRotation(_dashDirection);
            effectData.origin = Util.GetCorePosition(base.gameObject);
            EffectManager.SpawnEffect(_blinkPrefab, effectData, transmit: false);
        }

        void performDash()
        {
            if (_bodyAttachment.attachedBody.hasAuthority)
            {
                _bodyAttachment.attachedBody.isSprinting = true;

                if (_bodyAttachment.attachedBody.TryGetComponent<IPhysMotor>(out var motor))
                {
                    if (_timer < 0.1f)
                    {
                        motor.ApplyForceImpulse(new PhysForceInfo
                        {
                            resetVelocity = true,
                            force = _dashDirection * (Time.deltaTime * _bodyAttachment.attachedBody.moveSpeed * 1000),
                            ignoreGroundStick = true,
                            massIsOne = true,
                        });
                    }
                    else
                    {
                        motor.ApplyForceImpulse(new PhysForceInfo
                        {
                            resetVelocity = true,
                            force = _dashDirection * (_bodyAttachment.attachedBody.moveSpeed * 3),
                            ignoreGroundStick = true,
                            massIsOne = true,
                        });
                    }
                }
            }
            tryAttack();
            if (_timer > 0.1)
            {
                switchState(State.endDash);
            }
        }

        void endDash()
        {
            if (_bodyAttachment.attachedBody.hasAuthority)
            {
                _bodyAttachment.attachedBody.isSprinting = true;
            }
                
            if (_timer > 0.1f)
            {
                switchState(State.idle);
            }            
            tryAttack();
        }

        void tryAttack()
        {
            _dashSphereSearch.origin = Util.GetCorePosition(base.gameObject);
            _dashSphereSearch.mask = LayerIndex.entityPrecise.mask;
            _dashSphereSearch.radius = _bodyAttachment.attachedBody.radius + 3;
            _dashSphereSearch.RefreshCandidates();
            _dashSphereSearch.FilterCandidatesByHurtBoxTeam(TeamMask.GetUnprotectedTeams(_bodyAttachment.attachedBody.teamComponent.teamIndex));
            _dashSphereSearch.FilterCandidatesByDistinctHurtBoxEntities();
            _dashSphereSearch.GetHurtBoxes(_dashHurtBoxBuffer);
            _dashSphereSearch.ClearCandidates();
            if (_dashHurtBoxBuffer.Count > 0)
            {
                switchState(State.idle);
                if (_bodyAttachment.attachedBody.hasAuthority)
                {
                    if (_bodyAttachment.attachedBody.TryGetComponent<IPhysMotor>(out var motor))
                    {
                        motor.ApplyForceImpulse(new PhysForceInfo
                        {
                            resetVelocity = true,
                            force = new Vector3(-_dashDirection.x * 20, 20, -_dashDirection.z * 20),
                            ignoreGroundStick = true,
                            massIsOne = true,
                        });
                    }
                }

                _dashSphereSearch.radius = _bodyAttachment.attachedBody.radius + 12;
                _dashHurtBoxBuffer.Clear();
                _dashSphereSearch.RefreshCandidates();
                _dashSphereSearch.FilterCandidatesByHurtBoxTeam(TeamMask.GetUnprotectedTeams(_bodyAttachment.attachedBody.teamComponent.teamIndex));
                _dashSphereSearch.FilterCandidatesByDistinctHurtBoxEntities();
                _dashSphereSearch.GetHurtBoxes(_dashHurtBoxBuffer);
                _dashSphereSearch.ClearCandidates();
            }

            for (int i = 0; i < _dashHurtBoxBuffer.Count; i++)
            {
                HurtBox hurtBox = _dashHurtBoxBuffer[i];
                if (hurtBox && hurtBox.healthComponent && hurtBox.healthComponent.body)
                {
                    EffectData effectData = new EffectData
                    {
                        origin = hurtBox.transform.position
                    };
                    EffectManager.SpawnEffect(_hitEffectPrefab, effectData, false);

                    if (_bodyAttachment.attachedBody.isServer)
                    {
                        DamageInfo damageInfo = new DamageInfo();
                        damageInfo.damage = 0;
                        damageInfo.attacker = base.gameObject;
                        damageInfo.procCoefficient = 0;
                        damageInfo.position = hurtBox.transform.position;
                        damageInfo.crit = false;
                        damageInfo.inflictedHurtbox = hurtBox;
                        damageInfo.canRejectForce = false;
                        damageInfo.physForceFlags |= PhysForceFlags.ignoreGroundStick;
                        damageInfo.physForceFlags |= PhysForceFlags.disableAirControlUntilCollision;
                        damageInfo.physForceFlags |= PhysForceFlags.massIsOne;
                        damageInfo.physForceFlags |= PhysForceFlags.resetVelocity;
                        if (hurtBox.healthComponent.body.isChampion)
                        {
                            damageInfo.force = Vector3.zero;
                        } else {
                            damageInfo.force = _dashDirection * 60;
                        }
                        hurtBox.healthComponent.TakeDamageForce(damageInfo);
                        GlobalEventManager.instance.OnHitEnemy(damageInfo, hurtBox.healthComponent.gameObject);
                        GlobalEventManager.instance.OnHitAll(damageInfo, hurtBox.healthComponent.gameObject);
                        
                        if (hurtBox.healthComponent.TryGetComponent(out SetStateOnHurt attackerSetStateOnHurt) && attackerSetStateOnHurt.canBeStunned)
                        {
                            attackerSetStateOnHurt.SetStun(1);
                            Crowbar.HandleDelayedHit(_bodyAttachment.attachedBody.gameObject, hurtBox.gameObject);
                        }
                        ItemQualityCounts sprintArmor = _bodyAttachment.attachedBody.inventory.GetItemCountsEffective(ItemQualitiesContent.ItemQualityGroups.SprintArmor);
                        float duration = sprintArmor.UncommonCount * 3 +
                                            sprintArmor.RareCount * 6 +
                                            sprintArmor.EpicCount * 9 +
                                            sprintArmor.LegendaryCount * 12;
                        hurtBox.healthComponent.body.AddTimedBuff(ItemQualitiesContent.Buffs.SprintArmorWeaken, duration);
                    }
                }
            }
            _dashHurtBoxBuffer.Clear();
        }
    }
}
