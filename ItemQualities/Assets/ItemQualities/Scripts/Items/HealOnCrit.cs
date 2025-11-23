using R2API;
using RoR2;
using System;

namespace ItemQualities.Items
{
    static class HealOnCrit
    {
        [SystemInitializer]
        static void Init()
        {
            RecalculateStatsAPI.GetStatCoefficients += getStatCoefficients;
        }

        static void getStatCoefficients(CharacterBody sender, RecalculateStatsAPI.StatHookEventArgs args)
        {
            if (!sender)
                return;

            if (sender.HasBuff(ItemQualitiesContent.Buffs.HealCritBoost))
            {
                QualityTier qualityTier = ItemQualitiesContent.ItemQualityGroups.HealOnCrit.GetItemCountsEffective(sender.inventory).HighestQuality;

                float crit = 0f;
                switch (qualityTier)
                {
                    case QualityTier.None:
                    case QualityTier.Uncommon:
                        crit = 20f;
                        break;
                    case QualityTier.Rare:
                        crit = 30f;
                        break;
                    case QualityTier.Epic:
                        crit = 40f;
                        break;
                    case QualityTier.Legendary:
                        crit = 50f;
                        break;
                    default:
                        Log.Error($"Quality tier {qualityTier} is not implemented");
                        break;
                }

                if (crit > 0)
                {
                    args.critAdd += crit;
                }
            }
        }
    }
}
