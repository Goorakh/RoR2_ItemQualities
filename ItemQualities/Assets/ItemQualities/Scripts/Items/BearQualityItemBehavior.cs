using ItemQualities.Utilities.Extensions;
using RoR2;

namespace ItemQualities.Items
{
    public sealed class BearQualityItemBehavior : QualityItemBodyBehavior
    {
        [ItemGroupAssociation(QualityItemBehaviorUsageFlags.Server)]
        static ItemQualityGroup GetItemGroup()
        {
            return ItemQualitiesContent.ItemQualityGroups.Bear;
        }

        CharacterBodyExtraStatsTracker _bodyExtraStats;

        protected override void Awake()
        {
            base.Awake();
            _bodyExtraStats = this.GetComponentCached<CharacterBodyExtraStatsTracker>();
        }

        void OnEnable()
        {
            _bodyExtraStats.OnIncomingDamageServer += onIncomingDamageServer;
        }

        void OnDisable()
        {
            _bodyExtraStats.OnIncomingDamageServer -= onIncomingDamageServer;
        }

        void onIncomingDamageServer(DamageInfo damageInfo)
        {
            if (damageInfo.rejected)
            {
                bool isInvincible = Body.HasBuff(RoR2Content.Buffs.Immune) ||
                                    Body.HasBuff(DLC2Content.Buffs.SojournVehicle);

                if (!isInvincible || damageInfo.IsParried())
                {
                    float damageFraction = damageInfo.damage / Body.healthComponent.fullCombinedHealth;

                    float invincibilityDurationPerPercentDamage = (0.01f * Stacks.UncommonCount) +
                                                                  (0.05f * Stacks.RareCount) +
                                                                  (0.15f * Stacks.EpicCount) +
                                                                  (0.25f * Stacks.LegendaryCount);

                    float invincibilityDuration = damageFraction * 100f * invincibilityDurationPerPercentDamage;
                    if (invincibilityDuration >= 1f / 30f)
                    {
                        Body.AddTimedBuff(RoR2Content.Buffs.Immune, invincibilityDuration);
                    }
                }
            }
        }
    }
}
