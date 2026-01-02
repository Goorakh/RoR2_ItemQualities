using RoR2;

namespace ItemQualities.Items
{
    public sealed class HealOnCritQualityItemBehavior : QualityItemBodyBehavior
    {
        [ItemGroupAssociation(QualityItemBehaviorUsageFlags.Server)]
        static ItemQualityGroup GetItemGroup()
        {
            return ItemQualitiesContent.ItemQualityGroups.HealOnCrit;
        }

        float _accumulatedHealing;

        void OnEnable()
        {
            HealthComponent.onCharacterHealServer += onCharacterHealServer;
        }

        void OnDisable()
        {
            HealthComponent.onCharacterHealServer -= onCharacterHealServer;
        }

        void onCharacterHealServer(HealthComponent healthComponent, float amount, ProcChainMask procChainMask)
        {
            if (!healthComponent || healthComponent != Body.healthComponent)
                return;

            _accumulatedHealing += amount;
            updateAccumulatedHealing();
        }

        protected override void OnStacksChanged()
        {
            base.OnStacksChanged();

            updateAccumulatedHealing();
        }

        void updateAccumulatedHealing()
        {
            ItemQualityCounts healOnCrit = Stacks;
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

            float healingThreshold = healingThresholdFraction * Body.healthComponent.fullHealth;

            if (healingThreshold > 0 && _accumulatedHealing >= healingThreshold)
            {
                float buffDuration = (4f * healOnCrit.UncommonCount) +
                                     (6f * healOnCrit.RareCount) +
                                     (8f * healOnCrit.EpicCount) +
                                     (10f * healOnCrit.LegendaryCount);

                _accumulatedHealing %= healingThreshold;
                Body.AddTimedBuff(ItemQualitiesContent.Buffs.HealCritBoost, buffDuration);
            }
        }
    }
}
