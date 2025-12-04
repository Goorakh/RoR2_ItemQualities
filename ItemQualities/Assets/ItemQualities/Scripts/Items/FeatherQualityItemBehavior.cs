using RoR2;
using UnityEngine;

namespace ItemQualities.Items
{
    public sealed class FeatherQualityItemBehavior : MonoBehaviour
    {
        CharacterBody _body;
        CharacterBodyExtraStatsTracker _bodyStats;

        void Awake()
        {
            _body = GetComponent<CharacterBody>();
            _bodyStats = GetComponent<CharacterBodyExtraStatsTracker>();
        }

        void OnEnable()
        {
            GlobalEventManager.onCharacterDeathGlobal += onCharacterDeathGlobal;
            _bodyStats.OnHitGroundServer += onHitGroundServer;
        }

        void OnDisable()
        {
            GlobalEventManager.onCharacterDeathGlobal -= onCharacterDeathGlobal;
            _bodyStats.OnHitGroundServer -= onHitGroundServer;
        }

        void onCharacterDeathGlobal(DamageReport report)
        {
            if (report.attackerBody != _body || (_body.characterMotor && _body.characterMotor.isGrounded))
                return;

            ItemQualityCounts feather = ItemQualitiesContent.ItemQualityGroups.Feather.GetItemCountsEffective(_body.inventory);

            int maxJumps = (feather.UncommonCount * 2) +
                           (feather.RareCount * 4) +
                           (feather.EpicCount * 6) +
                           (feather.LegendaryCount * 8);

            if (report.attackerBody.GetBuffCount(ItemQualitiesContent.Buffs.FeatherExtraJumps) < maxJumps)
            {
                report.attackerBody.AddBuff(ItemQualitiesContent.Buffs.FeatherExtraJumps);
            }
        }

        void onHitGroundServer(CharacterMotor.HitGroundInfo info)
        {
            _body.SetBuffCount(ItemQualitiesContent.Buffs.FeatherExtraJumps.buffIndex, 0);
        }
    }
}
