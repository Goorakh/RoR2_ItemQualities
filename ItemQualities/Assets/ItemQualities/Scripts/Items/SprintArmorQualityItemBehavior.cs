using ItemQualities.Utilities.Extensions;
using RoR2;
using RoR2BepInExPack.GameAssetPathsBetter;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Networking;

namespace ItemQualities.Items
{
    public sealed class SprintArmorQualityItemBehavior : QualityItemBodyBehavior
    {
        [ItemGroupAssociation(QualityItemBehaviorUsageFlags.Server)]
        static ItemQualityGroup GetItemGroup()
        {
            return ItemQualitiesContent.ItemQualityGroups.SprintArmor;
        }

        static GameObject _blinkPrefab;
        static GameObject _hitEffectPrefab;

        float _activationWindow;
        float _dashWindow;
        bool _heldForward;
        bool _isDashing;
        Vector3 _dashDirection;
        static readonly SphereSearch _dashSphereSearch = new SphereSearch();
        static readonly List<HurtBox> _dashHurtBoxBuffer = new List<HurtBox>();

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

        void FixedUpdate()
        {
            Vector3 moveVector = Body.inputBank.moveVector;
            Vector3 aimVector = Body.inputBank.aimDirection;
            aimVector.y = 0;
            float angleDiff = Vector3.Angle(moveVector.normalized, aimVector);

            if (!Body.HasBuff(ItemQualitiesContent.Buffs.SprintArmorDashCooldown) &&
            angleDiff < 70 && moveVector.magnitude > 0.2)
            {
                if (!_heldForward)
                {
                    _heldForward = true;
                    if (_activationWindow > 0)
                    {
                        _dashWindow = 0.15f;
                        _dashDirection = Body.inputBank.aimDirection;
                        float cooldown = Stacks.HighestQuality switch
                        {
                            QualityTier.Uncommon => 20,
                            QualityTier.Rare => 15,
                            QualityTier.Epic => 10,
                            QualityTier.Legendary => 5,
                            _ => 30,
                        };
                        Body.AddTimedBuff(ItemQualitiesContent.Buffs.SprintArmorDashCooldown, cooldown);
                    }
                    else
                    {
                        _activationWindow = 0.2f;
                    }
                }
            }
            else
            {
                _heldForward = false;
            }
            _activationWindow -= Time.deltaTime;

            if (_dashWindow > 0)
            {
                _dashWindow -= Time.deltaTime;
                handleDash();
            }
        }

        private void handleDash()
        {
            Body.isSprinting = true;

            if (!_isDashing && _dashWindow > 0.05)
            {
                _isDashing = true;
                EffectData effectData = new EffectData();
                effectData.rotation = Util.QuaternionSafeLookRotation(_dashDirection);
                effectData.origin = Util.GetCorePosition(base.gameObject);
                EffectManager.SpawnEffect(_blinkPrefab, effectData, transmit: false);
            }

            if (_isDashing)
            {
                Body.characterMotor.rootMotion += _dashDirection * (Time.deltaTime * Body.moveSpeed * 10);
            }

            if (tryAttack())
            {
                _dashWindow = 0;
                _isDashing = false;
                Body.characterMotor.velocity = new Vector3(-_dashDirection.x * 20, 20, -_dashDirection.z * 20);
                Body.characterMotor.Motor.ForceUnground();
                float duration = Stacks.UncommonCount * 2 +
                                    Stacks.RareCount * 4 +
                                    Stacks.EpicCount * 6 +
                                    Stacks.LegendaryCount * 8;
                Body.AddTimedBuff(ItemQualitiesContent.Buffs.SprintArmorStrong, duration);
            }
            else if (_dashWindow < 0.05 && _isDashing)
            {
                _isDashing = false;
                Body.characterMotor.velocity = _dashDirection * (Body.moveSpeed * 3);
            }
        }

        private bool tryAttack()
        {
            bool hitEnemy = false;
            if (Body.hasAuthority)
            {
                _dashSphereSearch.origin = Util.GetCorePosition(base.gameObject);
                _dashSphereSearch.mask = LayerIndex.entityPrecise.mask;
                _dashSphereSearch.radius = Body.radius + 3;
                _dashSphereSearch.RefreshCandidates();
                _dashSphereSearch.FilterCandidatesByHurtBoxTeam(TeamMask.GetUnprotectedTeams(Body.teamComponent.teamIndex));
                _dashSphereSearch.FilterCandidatesByDistinctHurtBoxEntities();
                _dashSphereSearch.GetHurtBoxes(_dashHurtBoxBuffer);
                _dashSphereSearch.ClearCandidates();
                if (_dashHurtBoxBuffer.Count > 0)
                {
                    hitEnemy = true;
                    _dashSphereSearch.radius = Body.radius + 12;
                    _dashHurtBoxBuffer.Clear();
                    _dashSphereSearch.RefreshCandidates();
                    _dashSphereSearch.FilterCandidatesByHurtBoxTeam(TeamMask.GetUnprotectedTeams(Body.teamComponent.teamIndex));
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
                        EffectManager.SpawnEffect(_hitEffectPrefab, effectData, false);
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
                                Crowbar.HandleDelayedHit(Body.gameObject, hurtBox.gameObject);
                            }
                            hurtBox.healthComponent.TakeDamage(damageInfo);
                            GlobalEventManager.instance.OnHitEnemy(damageInfo, hurtBox.healthComponent.gameObject);
                            GlobalEventManager.instance.OnHitAll(damageInfo, hurtBox.healthComponent.gameObject);
                        }
                    }
                }
                _dashHurtBoxBuffer.Clear();
            }
            return hitEnemy;
        }
    }
}
