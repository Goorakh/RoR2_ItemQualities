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
            if (healthComponent && healthComponent.body && healthComponent.body.inventory)
            {
                ItemQualityCounts increaseHealing = healthComponent.body.inventory.GetItemCountsEffective(ItemQualitiesContent.ItemQualityGroups.IncreaseHealing);
                if (increaseHealing.TotalQualityCount > 0)
                {
                    float healFraction = amount / healthComponent.fullHealth;

                    float invincibilityDurationPerFullHeal = (increaseHealing.UncommonCount * 2f) +
                                                             (increaseHealing.RareCount * 5f) +
                                                             (increaseHealing.EpicCount * 10f) +
                                                             (increaseHealing.LegendaryCount * 15f);

                    float invincibilityDuration = invincibilityDurationPerFullHeal * healFraction;
                    if (invincibilityDuration >= 1f / 60f)
                    {
                        healthComponent.body.AddTimedBuff(RoR2Content.Buffs.Immune, invincibilityDuration);
                    }
                }
            }
        }
    }
}
