using ItemQualities.ModCompatibility;
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
                CharacterBody miniboss = findBestMiniBoss();
                if (miniboss)
                {
                    ref readonly ItemQualityCounts bossDamageBonus = ref Stacks;

                    float markDuration = 10 + (bossDamageBonus.UncommonCount * 5) +
                                              (bossDamageBonus.RareCount * 10) +
                                              (bossDamageBonus.EpicCount * 15) +
                                              (bossDamageBonus.LegendaryCount * 20);

                    MinibossController minibossManager = miniboss.gameObject.AddComponent<MinibossController>();
                    minibossManager.duration = markDuration;

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

        sealed class MinibossController : MonoBehaviour
        {
            public float duration;

            CharacterBody _body;
            GameObject _miniBossBodyAttachmentObj;
            float _timer;

            private void Awake()
            {
                _body = GetComponent<CharacterBody>();

                Log.Debug($"New miniboss: {Util.GetBestBodyName(gameObject)}");

                _body.AddBuff(ItemQualitiesContent.Buffs.MiniBossMarker);
                _body.AddTimedBuff(ItemQualitiesContent.Buffs.MiniBossCooldown, 90);

                _miniBossBodyAttachmentObj = Instantiate(ItemQualitiesContent.NetworkedPrefabs.MiniBossBodyAttachment);

                NetworkedBodyAttachment miniBossAttachment = _miniBossBodyAttachmentObj.GetComponent<NetworkedBodyAttachment>();
                miniBossAttachment.AttachToGameObjectAndSpawn(gameObject);
            }

            private void OnDestroy()
            {
                _body.RemoveBuff(ItemQualitiesContent.Buffs.MiniBossMarker);
                Destroy(_miniBossBodyAttachmentObj);
                _miniBossBodyAttachmentObj = null;
            }

            private void FixedUpdate()
            {
                _timer += Time.fixedDeltaTime;

                if (_timer > duration || !_body || !_body.HasBuff(ItemQualitiesContent.Buffs.MiniBossMarker))
                {
                    Destroy(this);
                }
            }
        }
    }
}
