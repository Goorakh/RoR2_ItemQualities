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

        float _updateMiniBossTimer = 60f;

        void FixedUpdate()
        {
            _updateMiniBossTimer += Time.fixedDeltaTime;

            ItemQualityCounts bossDamageBonus = Stacks;

            float markDuration =    bossDamageBonus.UncommonCount * 5 +
                                    bossDamageBonus.RareCount * 10 +
                                    bossDamageBonus.EpicCount * 15 +
                                    bossDamageBonus.LegendaryCount * 20 + 10;


            if (_updateMiniBossTimer >= 40)
            {
                _updateMiniBossTimer = 0f;
                CharacterBody miniboss = findBestMiniBoss();
                if (miniboss) {
                    MinibossManager minibossManager = miniboss.gameObject.AddComponent<MinibossManager>();
                    minibossManager.duration = markDuration;
                } else {
                    _updateMiniBossTimer = 39f;
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
                    highestHealthBody = getMiniBossOfTeam(teamIndex, Body, highestHealthBody);
                }
            }

            return highestHealthBody;
        }

        static CharacterBody getMiniBossOfTeam(TeamIndex teamIndex, CharacterBody playerBody, CharacterBody highestHealthBody)
        {
            foreach (TeamComponent teamComponent in TeamComponent.GetTeamMembers(teamIndex))
            {
                CharacterBody body = teamComponent.body;
                if (!body || !body.healthComponent || !body.healthComponent.alive || body.HasBuff(ItemQualitiesContent.Buffs.MiniBossCooldown) || body.HasBuff(ItemQualitiesContent.Buffs.MiniBossMarker))
                {
                    continue;
                }

                if (!highestHealthBody || body.healthComponent.fullCombinedHealth > highestHealthBody.healthComponent.fullCombinedHealth)
                {
                    if (!body.isBoss && Vector3.Distance(playerBody.transform.position, body.transform.position) < 250)
                    {
                        highestHealthBody = body;
                    }
                }
            }

            return highestHealthBody;
        }
    }

    public class MinibossManager : MonoBehaviour
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

        private void FixedUpdate()
        {
            _timer += Time.deltaTime;
            if (_timer > duration) {
                Destroy(this);
            }
        }

        private void OnDisable()
        {
            _body.RemoveBuff(ItemQualitiesContent.Buffs.MiniBossMarker);
            Destroy(_miniBossBodyAttachmentObj, 0.5f);
            _miniBossBodyAttachmentObj = null;
        }
    }
}
