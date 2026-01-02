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

        CharacterBody _currentMiniBoss;
        GameObject _currentMiniBossAttachment;

        float _updateMiniBossTimer = 60f;

        private void OnDisable()
        {
            unsetMiniboss();
        }

        void FixedUpdate()
        {
            _updateMiniBossTimer += Time.fixedDeltaTime;

            ItemQualityCounts bossDamageBonus = Stacks;

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
                unsetMiniboss();
            }
            if (_updateMiniBossTimer >= markfrequency)
            {
                _updateMiniBossTimer = 0f;
                setMiniBoss(findBestMiniBoss());
            }
        }

        void unsetMiniboss()
        {
            if (_currentMiniBoss)
            {
                _currentMiniBoss.RemoveBuff(ItemQualitiesContent.Buffs.MiniBossMarker);
                Destroy(_currentMiniBossAttachment, 0.5f);
                _currentMiniBossAttachment = null;
                _currentMiniBoss = null;
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
                    highestHealthBody = getMiniBossOfTeam(teamIndex, highestHealthBody);
                }
            }

            return highestHealthBody;
        }

        static CharacterBody getMiniBossOfTeam(TeamIndex teamIndex, CharacterBody highestHealthBody)
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
