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

            float healingThreshold = 0f;
            switch (healOnCrit.HighestQuality)
            {
                case QualityTier.Uncommon:
                    healingThreshold = 2000f;
                    break;
                case QualityTier.Rare:
                    healingThreshold = 1500f;
                    break;
                case QualityTier.Epic:
                    healingThreshold = 700f;
                    break;
                case QualityTier.Legendary:
                    healingThreshold = 400f;
                    break;
                default:
                    Log.Error($"Quality tier {healOnCrit.HighestQuality} is not implemented");
                    break;
            }

            float buffDuration = (4f * healOnCrit.UncommonCount) +
                                 (6f * healOnCrit.RareCount) +
                                 (8f * healOnCrit.EpicCount) +
                                 (10f * healOnCrit.LegendaryCount);

            if (healingThreshold > 0 && _accumulatedHealing >= healingThreshold)
            {
                _accumulatedHealing %= healingThreshold;
                _body.AddTimedBuff(ItemQualitiesContent.Buffs.HealCritBoost, buffDuration);
            }
        }
    }
}
