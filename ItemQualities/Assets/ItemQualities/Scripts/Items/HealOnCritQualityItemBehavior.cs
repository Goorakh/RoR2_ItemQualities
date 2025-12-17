using RoR2;
using UnityEngine;

namespace ItemQualities.Items
{
    public class HealOnCritQualityItemBehavior : MonoBehaviour
    {
        CharacterBody _body;

        float _accumulatedHealing;

        void Awake()
        {
            _body = GetComponent<CharacterBody>();
        }

        void OnEnable()
        {
            HealthComponent.onCharacterHealServer += onCharacterHealServer;
            _body.onInventoryChanged += onInventoryChanged;
        }

        void OnDisable()
        {
            HealthComponent.onCharacterHealServer -= onCharacterHealServer;
            _body.onInventoryChanged -= onInventoryChanged;
        }

        void onCharacterHealServer(HealthComponent healthComponent, float amount, ProcChainMask procChainMask)
        {
            if (!healthComponent || healthComponent != _body.healthComponent)
                return;

            _accumulatedHealing += amount;
            updateAccumulatedHealing();
        }

        void onInventoryChanged()
        {
            updateAccumulatedHealing();
        }

        void updateAccumulatedHealing()
        {
            ItemQualityCounts healOnCrit = ItemQualitiesContent.ItemQualityGroups.HealOnCrit.GetItemCountsEffective(_body.inventory);
            if (healOnCrit.TotalQualityCount == 0)
                return;

            float healingThresholdFraction;
            switch (healOnCrit.HighestQuality)
            {
                case QualityTier.Uncommon:
                    healingThresholdFraction = 2f;
                    break;
                case QualityTier.Rare:
                    healingThresholdFraction = 1f;
                    break;
                case QualityTier.Epic:
                    healingThresholdFraction = 0.5f;
                    break;
                case QualityTier.Legendary:
                    healingThresholdFraction = 0.2f;
                    break;
                default:
                    Log.Error($"Quality tier {healOnCrit.HighestQuality} is not implemented");
                    healingThresholdFraction = 0f;
                    break;
            }

            float healingThreshold = healingThresholdFraction * _body.healthComponent.fullHealth;

            if (healingThreshold > 0 && _accumulatedHealing >= healingThreshold)
            {
                float buffDuration = (4f * healOnCrit.UncommonCount) +
                                     (6f * healOnCrit.RareCount) +
                                     (8f * healOnCrit.EpicCount) +
                                     (10f * healOnCrit.LegendaryCount);

                _accumulatedHealing %= healingThreshold;
                _body.AddTimedBuff(ItemQualitiesContent.Buffs.HealCritBoost, buffDuration);
            }
        }
    }
}
