using RoR2;
using UnityEngine;
using UnityEngine.Networking;

namespace ItemQualities.Items
{
    public class BossDamageBonusQualityItemBehavior : MonoBehaviour
    {
        CharacterBody _currentMiniBoss;
        GameObject _currentMiniBossAttachment;
        CharacterBody _body;

        float _updateMiniBossTimer = 60f;

        private void Awake()
        {
            _body = GetComponent<CharacterBody>();
        }

        void FixedUpdate()
        {
            if (!NetworkServer.active)
                return;
            _updateMiniBossTimer += Time.fixedDeltaTime;

            ItemQualityCounts bossDamageBonus = ItemQualitiesContent.ItemQualityGroups.BossDamageBonus.GetItemCountsEffective(_body.inventory);

            float markfrequency = bossDamageBonus.HighestQuality switch
            {
                QualityTier.Uncommon => 60f,
                QualityTier.Rare => 50f,
                QualityTier.Epic => 40f,
                QualityTier.Legendary => 20f,
                _ => 0f
            };

            if (_updateMiniBossTimer >= 10)
            {
                if (_currentMiniBoss)
                {
                    _currentMiniBoss.RemoveBuff(ItemQualitiesContent.Buffs.MiniBossMarker);
                    Destroy(_currentMiniBossAttachment, 0.5f);
                    _currentMiniBossAttachment = null;
                    _currentMiniBoss = null;
                }
            }
            if (_updateMiniBossTimer >= markfrequency)
            {
                _updateMiniBossTimer = 0f;
                setMiniBoss(findBestMiniBoss());
            }
        }

        CharacterBody findBestMiniBoss()
        {
            CharacterBody highestHealthBody = null;
            highestHealthBody = getMiniBossOfTeam(TeamIndex.Monster, highestHealthBody);
            highestHealthBody = getMiniBossOfTeam(TeamIndex.Void, highestHealthBody);
            highestHealthBody = getMiniBossOfTeam(TeamIndex.Lunar, highestHealthBody);
            return highestHealthBody;
        }

        CharacterBody getMiniBossOfTeam(TeamIndex teamIndex, CharacterBody highestHealthBody)
        {
            foreach (TeamComponent teamComponent in TeamComponent.GetTeamMembers(teamIndex))
            {
                CharacterBody body = teamComponent.body;
                if (!body || !body.healthComponent || !body.healthComponent.alive || body.HasBuff(ItemQualitiesContent.Buffs.MiniBossCooldown))
                    continue;

                if (!highestHealthBody || body.healthComponent.fullCombinedHealth > highestHealthBody.healthComponent.fullCombinedHealth)
                {
                    if (!body.isBoss)
                    {
                        highestHealthBody = body;
                    }
                }
            }
            return highestHealthBody;
        }

        void setMiniBoss(CharacterBody body)
        {
            _currentMiniBoss = body;

            if (_currentMiniBoss)
            {
                Log.Debug($"New miniboss: {Util.GetBestBodyName(_currentMiniBoss.gameObject)}");

                _currentMiniBoss.AddBuff(ItemQualitiesContent.Buffs.MiniBossMarker);
                _currentMiniBoss.AddTimedBuff(ItemQualitiesContent.Buffs.MiniBossCooldown, 90);

                GameObject miniBossBodyAttachmentObj = Instantiate(ItemQualitiesContent.NetworkedPrefabs.MiniBossBodyAttachment);

                NetworkedBodyAttachment miniBossAttachment = miniBossBodyAttachmentObj.GetComponent<NetworkedBodyAttachment>();
                miniBossAttachment.AttachToGameObjectAndSpawn(_currentMiniBoss.gameObject);

                _currentMiniBossAttachment = miniBossBodyAttachmentObj;
            }
        }
    }
}
