using ItemQualities.Utilities.Extensions;
using R2API;
using RoR2;

namespace ItemQualities.Items
{
    internal static class ConstructBubble
    {
        [SystemInitializer]
        private static void Init()
        {
            GlobalEventManager.onServerDamageDealt += onServerDamageDealt;

            RecalculateStatsAPI.GetStatCoefficients += getStatCoefficients;
        }

        private static void onServerDamageDealt(DamageReport damageReport)
        {
            if (damageReport.victimBody && damageReport.victimBody.HasBuff(ItemQualitiesContent.Buffs.ConstructBubble))
            {
                damageReport.victimBody.RemoveBuff(ItemQualitiesContent.Buffs.ConstructBubble);

                if (damageReport.victimBody.inventory)
                {
                    ItemQualityCounts constructBubble = damageReport.victimBody.inventory.GetItemCountsEffective(ItemQualitiesContent.ItemQualityGroups.ConstructBubble);
                    if (constructBubble.TotalQualityCount > 0)
                    {
                        float cooldownDuration;
                        switch (constructBubble.HighestQuality)
                        {
                            case QualityTier.Uncommon:
                                cooldownDuration = 8f;
                                break;
                            case QualityTier.Rare:
                                cooldownDuration = 4f;
                                break;
                            case QualityTier.Epic:
                                cooldownDuration = 2f;
                                break;
                            case QualityTier.Legendary:
                                cooldownDuration = 1f;
                                break;
                            default:
                                Log.Warning($"Quality tier {constructBubble.HighestQuality} is not implemented");
                                cooldownDuration = 10f;
                                break;
                        }

                        damageReport.victimBody.AddTimedBuff(ItemQualitiesContent.Buffs.ConstructBubbleCooldown, cooldownDuration);
                    }
                }
            }
        }

        private static void getStatCoefficients(CharacterBody sender, RecalculateStatsAPI.StatHookEventArgs args)
        {
            if (sender.HasBuff(ItemQualitiesContent.Buffs.ConstructBubble))
            {
                args.armorAdd += 100f;
            }
        }
    }

    public sealed class ConstructBubbleQualityItemBehavior : QualityItemBodyBehavior
    {
        [ItemGroupAssociation(QualityItemBehaviorUsageFlags.Server)]
        private static ItemQualityGroup GetItemGroup() => ItemQualitiesContent.ItemQualityGroups.ConstructBubble;

        private void FixedUpdate()
        {
            if (!Body.HasBuff(ItemQualitiesContent.Buffs.ConstructBubble) && !Body.HasBuff(ItemQualitiesContent.Buffs.ConstructBubbleCooldown))
            {
                Body.AddBuff(ItemQualitiesContent.Buffs.ConstructBubble);
            }
        }

        private void OnDisable()
        {
            Body.RemoveBuff(ItemQualitiesContent.Buffs.ConstructBubble);
        }
    }
}
