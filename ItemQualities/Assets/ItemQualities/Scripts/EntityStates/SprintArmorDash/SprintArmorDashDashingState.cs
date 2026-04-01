using ItemQualities;
using ItemQualities.Utilities.Extensions;
using RoR2;
using RoR2BepInExPack.GameAssetPathsBetter;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace EntityStates.SprintArmorDash
{
    public class SprintArmorDashDashingState : EntityState
    {
        Vector3 _dashDirection;
        bool _stoppedDash;
        CharacterBody _attachedBody;
        static readonly SphereSearch _dashSphereSearch = new SphereSearch();
        static readonly List<HurtBox> _dashHurtBoxBuffer = new List<HurtBox>();
        static GameObject _blinkPrefab;

        [SystemInitializer]
        static void Init()
        {
            Addressables.LoadAssetAsync<GameObject>(RoR2_Base_Huntress.HuntressBlinkEffect_prefab).OnSuccess(prefab =>
            {
                _blinkPrefab = prefab;
            });
        }

        public override void OnEnter()
        {
            base.OnEnter();
            NetworkedBodyAttachment networkedBodyAttachment = GetComponent<NetworkedBodyAttachment>();
            if (!networkedBodyAttachment || !networkedBodyAttachment.attachedBody)
                return;
            _attachedBody = networkedBodyAttachment.attachedBody;

            _dashDirection = _attachedBody.inputBank.aimDirection;

            if (base.isAuthority)
            {
                _attachedBody.isSprinting = true;
                ItemQualityCounts sprintArmor = _attachedBody.inventory.GetItemCountsEffective(ItemQualitiesContent.ItemQualityGroups.SprintArmor);
                float cooldown = sprintArmor.HighestQuality switch
                {
                    QualityTier.Uncommon => 20,
                    QualityTier.Rare => 15,
                    QualityTier.Epic => 10,
                    QualityTier.Legendary => 5,
                    _ => 30,
                };
                _attachedBody.AddTimedBuffAuthority(ItemQualitiesContent.Buffs.SprintArmorDashCooldown.buffIndex, cooldown);
                _attachedBody.AddTimedBuffAuthority(JunkContent.Buffs.IgnoreFallDamage.buffIndex, 0.3f);
            }

            EffectData effectData = new EffectData();
            effectData.rotation = Util.QuaternionSafeLookRotation(_dashDirection);
            effectData.origin = Util.GetCorePosition(base.gameObject);
            EffectManager.SpawnEffect(_blinkPrefab, effectData, transmit: false);
        }

        public override void FixedUpdate()
        {
            base.FixedUpdate();
            if (base.isAuthority)
            {
                _attachedBody.isSprinting = true;

                if (_attachedBody.TryGetComponent<IPhysMotor>(out var motor))
                {
                    if (base.fixedAge < 0.1f)
                    {
                        motor.ApplyForceImpulse(new PhysForceInfo
                        {
                            resetVelocity = true,
                            force = _dashDirection * (Time.deltaTime * _attachedBody.moveSpeed * 1000),
                            ignoreGroundStick = true,
                            massIsOne = true,
                        });
                    } else if (!_stoppedDash)
                    {
                        _stoppedDash = true;
                        motor.ApplyForceImpulse(new PhysForceInfo
                        {
                            resetVelocity = true,
                            force = _dashDirection * (_attachedBody.moveSpeed * 3),
                            ignoreGroundStick = true,
                            massIsOne = true,
                        });
                    }
                }
                if (base.fixedAge > 0.2)
                {
                    outer.SetNextStateToMain();
                }
            }
            tryAttack();
        }

        void tryAttack()
        {
            _dashSphereSearch.origin = Util.GetCorePosition(base.gameObject);
            _dashSphereSearch.mask = LayerIndex.entityPrecise.mask;
            _dashSphereSearch.radius = _attachedBody.radius + 3;
            _dashSphereSearch.RefreshCandidates();
            _dashSphereSearch.FilterCandidatesByHurtBoxTeam(TeamMask.GetUnprotectedTeams(_attachedBody.teamComponent.teamIndex));
            _dashSphereSearch.FilterCandidatesByDistinctHurtBoxEntities();
            _dashSphereSearch.GetHurtBoxes(_dashHurtBoxBuffer);
            _dashSphereSearch.ClearCandidates();
            if (_dashHurtBoxBuffer.Count > 0)
            {
                SprintArmorDashBounce sprintArmorDashBounce = new SprintArmorDashBounce();
                sprintArmorDashBounce.attackPos = Util.GetCorePosition(base.gameObject);
                sprintArmorDashBounce.dashDirection = _dashDirection;
                outer.SetNextState(sprintArmorDashBounce);
            }
            _dashHurtBoxBuffer.Clear();
        }
    }
}
