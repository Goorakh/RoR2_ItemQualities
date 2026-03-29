using ItemQualities.Utilities.Extensions;
using RoR2;
using RoR2BepInExPack.GameAssetPathsBetter;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Networking;

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
        State _state;
        float _duration;

        enum State {
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
            _state = State.startDash;
        }

        private void FixedUpdate()
        {
            if (!_bodyAttachment || !_bodyAttachment.attachedBody)
                return;
            _duration += Time.deltaTime;
            _bodyAttachment.attachedBody.isSprinting = true;
            switch (_state) {
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

        void startDash()
        {
            _state = State.dashing;
            _dashDirection = _bodyAttachment.attachedBody.inputBank.aimDirection;

            ItemQualityCounts sprintArmor = _bodyAttachment.attachedBody.inventory.GetItemCountsEffective(ItemQualitiesContent.ItemQualityGroups.SprintArmor);
            float cooldown = sprintArmor.HighestQuality switch
            {
                QualityTier.Uncommon => 20,
                QualityTier.Rare => 15,
                QualityTier.Epic => 10,
                QualityTier.Legendary => 5,
                _ => 30,
            };
            _bodyAttachment.attachedBody.AddTimedBuff(ItemQualitiesContent.Buffs.SprintArmorDashCooldown, cooldown);

            EffectData effectData = new EffectData();
            effectData.rotation = Util.QuaternionSafeLookRotation(_dashDirection);
            effectData.origin = Util.GetCorePosition(base.gameObject);
            EffectManager.SpawnEffect(_blinkPrefab, effectData, transmit: true);
        }

        void performDash()
        {
            tryAttack();

            if (_duration < 0.1f)
            {
                if (_bodyAttachment.attachedBody.TryGetComponent<IPhysMotor>(out var motor))
                {
                    motor.ApplyForceImpulse(
                    new PhysForceInfo
                    {
                        resetVelocity = true,
                        force = _dashDirection * (Time.deltaTime * _bodyAttachment.attachedBody.moveSpeed * 1000),
                        ignoreGroundStick = true,
                        massIsOne = true,
                    });
                }
            } else {
                if (_bodyAttachment.attachedBody.TryGetComponent<IPhysMotor>(out var motor))
                {
                    motor.ApplyForceImpulse(
                    new PhysForceInfo
                    {
                        resetVelocity = true,
                        force = _dashDirection * (_bodyAttachment.attachedBody.moveSpeed * 3),
                        ignoreGroundStick = true,
                        massIsOne = true,
                    });
                }
                _state = State.endDash;
            }
        }

        void endDash()
        {
            tryAttack();
            if (_duration > 0.2f)
            {
                Destroy(gameObject);
            }
        }

        void tryAttack()
        {
            bool hitEnemy = false;
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
                hitEnemy = true;
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
                if (hurtBox && hurtBox.healthComponent && hurtBox.healthComponent.body && !hurtBox.healthComponent.body.isChampion)
                {
                    EffectData effectData = new EffectData
                    {
                        origin = hurtBox.transform.position
                    };
                    EffectManager.SpawnEffect(_hitEffectPrefab, effectData, true);
                    if (NetworkServer.active)
                    {
                        DamageInfo damageInfo = new DamageInfo();
                        damageInfo.damage = 0;
                        damageInfo.attacker = base.gameObject;
                        damageInfo.procCoefficient = 0;
                        damageInfo.position = hurtBox.transform.position;
                        damageInfo.crit = false;
                        damageInfo.inflictedHurtbox = hurtBox;
                        damageInfo.force = _dashDirection * 60;
                        damageInfo.canRejectForce = false;
                        damageInfo.physForceFlags |= PhysForceFlags.ignoreGroundStick;
                        damageInfo.physForceFlags |= PhysForceFlags.disableAirControlUntilCollision;
                        damageInfo.physForceFlags |= PhysForceFlags.massIsOne;
                        damageInfo.physForceFlags |= PhysForceFlags.resetVelocity;
                        damageInfo.physForceFlags |= PhysForceFlags.respectKnockbackImmuneFlag;

                        if (hurtBox.healthComponent.TryGetComponent(out SetStateOnHurt attackerSetStateOnHurt) && attackerSetStateOnHurt.canBeStunned)
                        {
                            attackerSetStateOnHurt.SetStun(1);
                            Crowbar.HandleDelayedHit(_bodyAttachment.attachedBody.gameObject, hurtBox.gameObject);
                        }
                        hurtBox.healthComponent.TakeDamage(damageInfo);
                        GlobalEventManager.instance.OnHitEnemy(damageInfo, hurtBox.healthComponent.gameObject);
                        GlobalEventManager.instance.OnHitAll(damageInfo, hurtBox.healthComponent.gameObject);
                    }
                }
            }
            _dashHurtBoxBuffer.Clear();

            if (hitEnemy)
            {
                _bodyAttachment.attachedBody.characterMotor.velocity = new Vector3(-_dashDirection.x * 20, 20, -_dashDirection.z * 20);
                _bodyAttachment.attachedBody.characterMotor.Motor.ForceUnground();
                ItemQualityCounts sprintArmor = _bodyAttachment.attachedBody.inventory.GetItemCountsEffective(ItemQualitiesContent.ItemQualityGroups.SprintArmor);
                float duration = sprintArmor.UncommonCount * 2 +
                                    sprintArmor.RareCount * 4 +
                                    sprintArmor.EpicCount * 6 +
                                    sprintArmor.LegendaryCount * 8;
                _bodyAttachment.attachedBody.AddTimedBuff(ItemQualitiesContent.Buffs.SprintArmorStrong, duration);
                Destroy(gameObject);
            }
        }
    }
}
