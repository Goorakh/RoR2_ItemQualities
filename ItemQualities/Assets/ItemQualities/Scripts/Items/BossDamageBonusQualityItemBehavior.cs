using HG;
using ItemQualities.ModCompatibility;
using ItemQualities.Utilities.Extensions;
using RoR2;
using UnityEngine;

namespace ItemQualities.Items
{
    public sealed class BossDamageBonusQualityItemBehavior : QualityItemBodyBehavior
    {
        [ItemGroupAssociation(QualityItemBehaviorUsageFlags.Server)]
        static ItemQualityGroup GetItemGroup()
        {
            return ItemQualitiesContent.ItemQualityGroups.BossDamageBonus;
        }

        float _updateMiniBossTimer = 0f;

        void FixedUpdate()
        {
            _updateMiniBossTimer -= Time.fixedDeltaTime;

            if (_updateMiniBossTimer <= 0f)
            {
                CharacterBody minibossBody = findBestMiniBoss();
                if (minibossBody)
                {
                    ref readonly ItemQualityCounts bossDamageBonus = ref Stacks;

                    float markDuration = 10 + (bossDamageBonus.UncommonCount * 5) +
                                              (bossDamageBonus.RareCount * 10) +
                                              (bossDamageBonus.EpicCount * 15) +
                                              (bossDamageBonus.LegendaryCount * 20);

                    minibossBody.AddTimedBuff(ItemQualitiesContent.Buffs.MiniBossMarker, markDuration);
                    minibossBody.AddTimedBuff(ItemQualitiesContent.Buffs.MiniBossCooldown, 90);

                    minibossBody.gameObject.EnsureComponent<MinibossController>();

                    Log.Debug($"New miniboss: {Util.GetBestBodyName(minibossBody.gameObject)}");

                    _updateMiniBossTimer = 40f;
                }
                else
                {
                    // Retry in 1 second if we failed to find any miniboss to avoid wasting the cooldown
                    _updateMiniBossTimer = 1f;
                }
            }
        }

        CharacterBody findBestMiniBoss()
        {
            CharacterBody highestHealthBody = null;
            TeamMask teamMask = TeamMask.allButNeutral;
            teamMask.RemoveTeam(Body.teamComponent.teamIndex);

            for (TeamIndex teamIndex = 0; (int)teamIndex < TeamsAPICompat.TeamsCount; teamIndex++)
            {
                if (teamMask.HasTeam(teamIndex))
                {
                    foreach (TeamComponent teamComponent in TeamComponent.GetTeamMembers(teamIndex))
                    {
                        CharacterBody body = teamComponent.body;
                        if (!body || !body.healthComponent || !body.healthComponent.alive)
                            continue;

                        if (body.isBoss)
                            continue;

                        if (body.HasBuff(ItemQualitiesContent.Buffs.MiniBossCooldown) || body.HasBuff(ItemQualitiesContent.Buffs.MiniBossMarker))
                            continue;

                        // Exclude masterless stuff, this gets rid of damageable things that arent necessarily enemies, like projectiles or scorcher puddles
                        if ((body.bodyFlags & CharacterBody.BodyFlags.Masterless) != 0 || !body.master)
                            continue;

                        float sqrDistance = (Body.corePosition - body.corePosition).sqrMagnitude;
                        if (sqrDistance >= 250f * 250f)
                            continue;

                        if (!highestHealthBody || body.healthComponent.fullCombinedHealth > highestHealthBody.healthComponent.fullCombinedHealth)
                        {
                            highestHealthBody = body;
                        }
                    }
                }
            }

            return highestHealthBody;
        }

        sealed class MinibossController : MonoBehaviour, IOnTakeDamageServerReceiver
        {
            CharacterBody _body;
            GameObject _miniBossBodyAttachmentObj;

            void Awake()
            {
                _body = GetComponent<CharacterBody>();
                
                _miniBossBodyAttachmentObj = Instantiate(ItemQualitiesContent.NetworkedPrefabs.MiniBossBodyAttachment);

                NetworkedBodyAttachment miniBossAttachment = _miniBossBodyAttachmentObj.GetComponent<NetworkedBodyAttachment>();
                miniBossAttachment.AttachToGameObjectAndSpawn(gameObject);
            }

            void OnEnable()
            {
                if (_body && _body.healthComponent)
                {
                    _body.healthComponent.AddOnTakeDamageServerReceiver(this);
                }
            }

            void OnDisable()
            {
                if (_body && _body.healthComponent)
                {
                    _body.healthComponent.RemoveOnTakeDamageServerReceiver(this);
                }
            }

            void OnDestroy()
            {
                if (_body)
                {
                    _body.ClearTimedBuffs(ItemQualitiesContent.Buffs.MiniBossMarker);
                }

                Destroy(_miniBossBodyAttachmentObj);
                _miniBossBodyAttachmentObj = null;
            }

            void FixedUpdate()
            {
                if (!_body ||
                    !_body.healthComponent ||
                    !_body.healthComponent.alive ||
                    !_body.HasBuff(ItemQualitiesContent.Buffs.MiniBossMarker))
                {
                    Destroy(this);
                }
            }

            void IOnTakeDamageServerReceiver.OnTakeDamageServer(DamageReport damageReport)
            {
                // If the miniboss is almost killed by someone with qapr, allow the duration to extend slightly.
                // Missing the kill by less than a second after focusing it feels bad.
                if (damageReport.attackerMaster &&
                    damageReport.attackerMaster.inventory.GetItemCountsEffective(ItemQualitiesContent.ItemQualityGroups.BossDamageBonus).TotalQualityCount > 0 &&
                    _body.healthComponent.combinedHealthFraction < 0.1f)
                {
                    _body.ExtendTimedBuffIfPresent(ItemQualitiesContent.Buffs.MiniBossMarker, 1f, 3f);
                }
            }
        }
    }
}
