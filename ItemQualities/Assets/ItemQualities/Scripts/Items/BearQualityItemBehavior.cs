using ItemQualities.Items;
using ItemQualities.Utilities.Extensions;
using RoR2;
using System;

namespace ItemQualities.Assets.ItemQualities.Scripts.Items
{
    public sealed class BearQualityItemBehavior : QualityItemBodyBehavior, IOnDamageRejectedServerReceiver
    {
        [ItemGroupAssociation(QualityItemBehaviorUsageFlags.Server)]
        private static ItemQualityGroup GetItemGroup() => ItemQualitiesContent.ItemQualityGroups.Bear;

        private void OnEnable()
        {
            if (Body && Body.healthComponent)
            {
                Body.healthComponent.AddOnDamageRejectedServerReceiver(this);
            }
        }

        private void OnDisable()
        {
            if (!ReferenceEquals(Body, null) && !ReferenceEquals(Body.healthComponent, null))
            {
                Body.healthComponent.RemoveOnDamageRejectedServerReceiver(this);
            }
        }

        public void OnDamageRejectedServer(DamageInfo damageInfo)
        {
            ref readonly ItemQualityCounts bear = ref Stacks;
            if (bear.TotalQualityCount > 0 && damageInfo.rejected)
            {
                bool isInvincible = Body.HasBuff(RoR2Content.Buffs.Immune) ||
                                    Body.HasBuff(DLC2Content.Buffs.SojournVehicle) ||
                                    Body.HasBuff(RoR2Content.Buffs.HiddenInvincibility);

                if (!isInvincible || damageInfo.IsParried())
                {
                    float damageFraction = damageInfo.damage / Body.healthComponent.fullCombinedHealth;

                    float invincibilityDurationPerPercentDamage = (0.02f * bear.UncommonCount) +
                                                                  (0.05f * bear.RareCount) +
                                                                  (0.1f * bear.EpicCount) +
                                                                  (0.15f * bear.LegendaryCount);

                    int maxDuration = (3 * bear.UncommonCount) +
                                      (6 * bear.RareCount) +
                                      (9 * bear.EpicCount) +
                                      (12 * bear.LegendaryCount);

                    float invincibilityDuration = Math.Min(damageFraction * 100f * invincibilityDurationPerPercentDamage, maxDuration);
                    if (invincibilityDuration >= 1f / 30f)
                    {
                        Body.AddTimedBuff(RoR2Content.Buffs.Immune, invincibilityDuration);
                    }
                }
            }
        }
    }
}
