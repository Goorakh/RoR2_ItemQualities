using ItemQualities;
using ItemQualities.Items;
using ItemQualities.Utilities.Extensions;
using RoR2;
using RoR2BepInExPack.GameAssetPathsBetter;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Networking;

namespace EntityStates.SprintArmorDash
{
    public class SprintArmorDashBounce : EntityState
    {
        CharacterBody _attachedBody;
        static readonly SphereSearch _dashSphereSearch = new SphereSearch();
        static readonly List<HurtBox> _dashHurtBoxBuffer = new List<HurtBox>();
        static GameObject _hitEffectPrefab;
        [NonSerialized]
        public Vector3 dashDirection;
        [NonSerialized]
        public Vector3 attackPos;

        [SystemInitializer]
        static void Init()
        {
            Addressables.LoadAssetAsync<GameObject>(RoR2_Base_Common_VFX.OmniImpactVFX_prefab).OnSuccess(prefab =>
            {
                _hitEffectPrefab = prefab;
            });
        }

        public override void OnSerialize(NetworkWriter writer)
        {
            base.OnSerialize(writer);
            writer.Write(dashDirection);
            writer.Write(attackPos);
        }

        public override void OnDeserialize(NetworkReader reader)
        {
            base.OnDeserialize(reader);
            dashDirection = reader.ReadVector3();
            attackPos = reader.ReadVector3();
        }

        public override void OnEnter()
        {
            base.OnEnter();
            NetworkedBodyAttachment networkedBodyAttachment = GetComponent<NetworkedBodyAttachment>();
            if (!networkedBodyAttachment || !networkedBodyAttachment.attachedBody)
                return;
            _attachedBody = networkedBodyAttachment.attachedBody;

            if (base.isAuthority)
            {
                IPhysMotor motor = _attachedBody.characterMotor ? _attachedBody.characterMotor : _attachedBody.GetComponent<IPhysMotor>();
                motor?.ApplyForceImpulse(new PhysForceInfo
                {
                    resetVelocity = true,
                    force = new Vector3(-dashDirection.x * 20, 20, -dashDirection.z * 20),
                    ignoreGroundStick = true,
                    massIsOne = true,
                });

                outer.SetNextStateToMain();
            }

            _dashSphereSearch.origin = Util.GetCorePosition(base.gameObject);
            _dashSphereSearch.mask = LayerIndex.entityPrecise.mask;
            _dashSphereSearch.radius = _attachedBody.radius + 12;
            _dashSphereSearch.RefreshCandidates();
            _dashSphereSearch.FilterCandidatesByHurtBoxTeam(TeamMask.GetUnprotectedTeams(_attachedBody.teamComponent.teamIndex));
            _dashSphereSearch.FilterCandidatesByDistinctHurtBoxEntities();
            _dashSphereSearch.GetHurtBoxes(_dashHurtBoxBuffer);
            _dashSphereSearch.ClearCandidates();

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

                    if (_attachedBody.isServer)
                    {
                        DamageInfo damageInfo = new DamageInfo();
                        damageInfo.damage = 0;
                        damageInfo.attacker = base.gameObject;
                        damageInfo.procCoefficient = 0;
                        damageInfo.position = attackPos;
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
                        }
                        else
                        {
                            damageInfo.force = dashDirection * 50;
                        }
                        hurtBox.healthComponent.TakeDamageForce(damageInfo);
                        GlobalEventManager.instance.OnHitEnemy(damageInfo, hurtBox.healthComponent.gameObject);
                        GlobalEventManager.instance.OnHitAll(damageInfo, hurtBox.healthComponent.gameObject);

                        if (hurtBox.healthComponent.TryGetComponent(out SetStateOnHurt attackerSetStateOnHurt) && attackerSetStateOnHurt.canBeStunned)
                        {
                            attackerSetStateOnHurt.SetStun(1);
                            Crowbar.HandleDelayedHit(_attachedBody.gameObject, hurtBox.gameObject);
                        }
                        ItemQualityCounts sprintArmor = _attachedBody.inventory.GetItemCountsEffective(ItemQualitiesContent.ItemQualityGroups.SprintArmor);
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
