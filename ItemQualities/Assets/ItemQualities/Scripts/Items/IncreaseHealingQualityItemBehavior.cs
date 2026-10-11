using RoR2;
using UnityEngine;
using ItemQualities.Utilities.Extensions;

namespace ItemQualities.Items
{
    public class IncreaseHealingQualityBehavior : QualityItemBodyBehavior
    {
        [ItemGroupAssociation(QualityItemBehaviorUsageFlags.Server)]
        private static ItemQualityGroup GetItemGroup() => ItemQualitiesContent.ItemQualityGroups.IncreaseHealing;

        private float _healTimer;
        private CharacterBodyExtraStatsTracker _bodyStats;

        private void Start()
        {
            _bodyStats = Body.GetComponentCached<CharacterBodyExtraStatsTracker>();
        }

        private void FixedUpdate()
        {
            if (Body.HasBuff(RoR2Content.Buffs.Immune) || !_bodyStats)
                return;

            _healTimer -= Time.fixedDeltaTime;
            
            if (_healTimer < 2)
            {
                _bodyStats.IncreaseHealingPreImmunityIndicator = true;

                if (_healTimer < 0)
                {
                    _healTimer = 10;

                    _bodyStats.IncreaseHealingPreImmunityIndicator = false;
                    if (_bodyStats.IncreaseHealingStoredEnergy >= 1f / 60f)
                    {
                        Body.AddTimedBuff(RoR2Content.Buffs.Immune, _bodyStats.IncreaseHealingStoredEnergy);
                        _bodyStats.IncreaseHealingStoredEnergy = 0;
                    }
                }
            }
        }
    }
}
