using HG;
using ItemQualities;
using ItemQualities.Items;
using ItemQualities.Utilities.Extensions;
using RoR2;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

namespace EntityStates.SprintArmorDash
{
    public sealed class SprintArmorDashBounce : EntityState
    {
        static readonly SphereSearch _dashSphereSearch = new SphereSearch();

        static EffectIndex _hitEffectIndex = EffectIndex.Invalid;

        CharacterBody _attachedBody;

        [NonSerialized]
        public Vector3 dashDirection;

        [NonSerialized]
        public Vector3 attackPos;

        [SystemInitializer(typeof(EffectCatalogUtils))]
        static void Init()
        {
            _hitEffectIndex = EffectCatalogUtils.FindEffectIndex("OmniImpactVFX");
            if (_hitEffectIndex == EffectIndex.Invalid)
            {
                Log.Warning("Failed to find hit effect index");
            }
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

            if (isAuthority)
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

            if (NetworkServer.active)
            {
                using var _ = ListPool<HurtBox>.RentCollection(out List<HurtBox> hurtBoxes);

                _dashSphereSearch.origin = characterBody.corePosition;
                _dashSphereSearch.mask = LayerIndex.entityPrecise.mask;
                _dashSphereSearch.radius = _attachedBody.radius + 12;
                _dashSphereSearch.RefreshCandidates();
                _dashSphereSearch.FilterCandidatesByHurtBoxTeam(TeamMask.GetUnprotectedTeams(_attachedBody.teamComponent.teamIndex));
                _dashSphereSearch.FilterCandidatesByDistinctHurtBoxEntities();
                _dashSphereSearch.GetHurtBoxes(hurtBoxes);
                _dashSphereSearch.ClearCandidates();

                foreach (HurtBox hurtBox in hurtBoxes)
                {
                    if (hurtBox && hurtBox.healthComponent && hurtBox.healthComponent.body)
                    {
                        if (_hitEffectIndex != EffectIndex.Invalid)
                        {
                            EffectData effectData = new EffectData
                            {
                                origin = hurtBox.transform.position
                            };

                            effectData.SetHurtBoxReference(hurtBox);

                            EffectManager.SpawnEffect(_hitEffectIndex, effectData, true);
                        }

                        DamageInfo damageInfo = new DamageInfo
                        {
                            damage = 0,
                            attacker = _attachedBody.gameObject,
                            inflictor = gameObject,
                            procCoefficient = 0,
                            position = attackPos,
                            crit = _attachedBody.RollCrit(),
                            inflictedHurtbox = hurtBox,
                            canRejectForce = false,
                            physForceFlags = PhysForceFlags.ignoreGroundStick | PhysForceFlags.disableAirControlUntilCollision | PhysForceFlags.massIsOne | PhysForceFlags.resetVelocity,
                        };

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

                        if (hurtBox.healthComponent.TryGetComponent(out SetStateOnHurt attackerSetStateOnHurt) &&
                            attackerSetStateOnHurt.canBeStunned)
                        {
                            attackerSetStateOnHurt.SetStun(1);
                            Crowbar.HandleDelayedHit(_attachedBody.gameObject, hurtBox.gameObject);
                        }

                        ItemQualityCounts sprintArmor = _attachedBody.inventory.GetItemCountsEffective(ItemQualitiesContent.ItemQualityGroups.SprintArmor);

                        float weakenDuration = (sprintArmor.UncommonCount * 3) +
                                               (sprintArmor.RareCount * 6) +
                                               (sprintArmor.EpicCount * 9) +
                                               (sprintArmor.LegendaryCount * 12);

                        hurtBox.healthComponent.body.AddTimedBuff(ItemQualitiesContent.Buffs.SprintArmorWeaken, weakenDuration);
                    }
                }
            }
        }
    }
}
