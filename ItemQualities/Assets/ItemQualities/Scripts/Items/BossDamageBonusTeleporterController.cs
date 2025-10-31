using RoR2;
using UnityEngine;
using UnityEngine.Networking;

namespace ItemQualities.Items
{
    public class BossDamageBonusTeleporterController : MonoBehaviour
    {
        TeleporterInteraction _teleporterInteraction;

        CharacterBody _currentMiniBoss;
        GameObject _currentMiniBossAttachment;

        float _updateMiniBossTimer = 0f;

        void Awake()
        {
            _teleporterInteraction = GetComponent<TeleporterInteraction>();
        }

        void FixedUpdate()
        {
            if (!NetworkServer.active)
                return;

            _updateMiniBossTimer -= Time.fixedDeltaTime;
            if (_updateMiniBossTimer <= 0f)
            {
                _updateMiniBossTimer = 0.2f;

                updateMiniBoss();
            }
        }

        void updateMiniBoss()
        {
            if (!_teleporterInteraction)
                return;

            TeamIndex chargingTeam = TeamIndex.Player;
            if (_teleporterInteraction.holdoutZoneController)
            {
                chargingTeam = _teleporterInteraction.holdoutZoneController.chargingTeam;
            }

            ItemQualityCounts teamBossDamageBonus = ItemQualitiesContent.ItemQualityGroups.BossDamageBonus.GetTeamItemCounts(chargingTeam, true, false);
            if (teamBossDamageBonus.TotalQualityCount == 0)
                return;

            if (!_teleporterInteraction.isCharged &&
                _teleporterInteraction.bossGroup &&
                _teleporterInteraction.bossGroup.combatSquad &&
                _teleporterInteraction.bossGroup.combatSquad.defeatedServer)
            {
                if (!_currentMiniBoss ||
                    !_currentMiniBoss.healthComponent ||
                    !_currentMiniBoss.healthComponent.alive ||
                    !_currentMiniBoss.HasBuff(ItemQualitiesContent.Buffs.MiniBossMarker))
                {
                    setMiniBoss(findBestMiniBoss());
                }
            }
        }

        CharacterBody findBestMiniBoss()
        {
            TeamIndex bossTeamIndex = TeamIndex.Monster;
            if (_teleporterInteraction.bossDirector)
            {
                bossTeamIndex = _teleporterInteraction.bossDirector.teamIndex;
            }
            else if (_teleporterInteraction.bonusDirector)
            {
                bossTeamIndex = _teleporterInteraction.bonusDirector.teamIndex;
            }

            CharacterBody highestHealthBody = null;
            foreach (TeamComponent teamComponent in TeamComponent.GetTeamMembers(bossTeamIndex))
            {
                CharacterBody body = teamComponent.body;
                if (!body || !body.healthComponent || !body.healthComponent.alive)
                    continue;

                if (!highestHealthBody || body.healthComponent.fullCombinedHealth > highestHealthBody.healthComponent.fullCombinedHealth)
                {
                    if (isBodyValidForMiniBoss(body))
                    {
                        highestHealthBody = body;
                    }
                }
            }

            return highestHealthBody;
        }

        bool isBodyValidForMiniBoss(CharacterBody body)
        {
            return !body.isBoss && (!_teleporterInteraction.holdoutZoneController || _teleporterInteraction.holdoutZoneController.IsBodyInChargingRadius(body));
        }

        void setMiniBoss(CharacterBody body)
        {
            if (_currentMiniBoss == body)
                return;

            if (_currentMiniBoss)
            {
                _currentMiniBoss.RemoveBuff(ItemQualitiesContent.Buffs.MiniBossMarker);
                Destroy(_currentMiniBossAttachment, 0.5f);
                _currentMiniBossAttachment = null;
            }

            _currentMiniBoss = body;

            if (_currentMiniBoss)
            {
                Log.Debug($"New miniboss: {Util.GetBestBodyName(_currentMiniBoss.gameObject)}");

                _currentMiniBoss.AddBuff(ItemQualitiesContent.Buffs.MiniBossMarker);

                GameObject miniBossBodyAttachmentObj = Instantiate(ItemQualitiesContent.NetworkedPrefabs.MiniBossBodyAttachment);

                NetworkedBodyAttachment miniBossAttachment = miniBossBodyAttachmentObj.GetComponent<NetworkedBodyAttachment>();
                miniBossAttachment.AttachToGameObjectAndSpawn(_currentMiniBoss.gameObject);

                _currentMiniBossAttachment = miniBossBodyAttachmentObj;
            }
        }
    }
}
