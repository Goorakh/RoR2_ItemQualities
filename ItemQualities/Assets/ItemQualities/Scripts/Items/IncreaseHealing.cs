using ItemQualities.Utilities.Extensions;
using RoR2;

namespace ItemQualities.Items
{
    internal static class IncreaseHealing
    {
        [SystemInitializer]
        private static void Init()
        {
            HealthComponent.onCharacterHealServer += onCharacterHealServer;
        }

        private static void onCharacterHealServer(HealthComponent healthComponent, float amount, ProcChainMask procChainMask)
        {
            if (healthComponent && healthComponent.body && healthComponent.body.inventory && healthComponent.body.TryGetComponentCached(out CharacterBodyExtraStatsTracker bodyStats))
            {
                ItemQualityCounts increaseHealing = healthComponent.body.inventory.GetItemCountsEffective(ItemQualitiesContent.ItemQualityGroups.IncreaseHealing);
                if (increaseHealing.TotalQualityCount > 0)
                {
                    float healFraction = amount / healthComponent.fullHealth;
                    float invincibilityCoeff =  (increaseHealing.UncommonCount * 2f) +
                                                (increaseHealing.RareCount * 4f) +
                                                (increaseHealing.EpicCount * 6f) +
                                                (increaseHealing.LegendaryCount * 8f);

                    bodyStats.IncreaseHealingStoredEnergy += invincibilityCoeff * healFraction;
                }
            }
        }
    }
}
