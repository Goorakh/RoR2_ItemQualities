using ItemQualities.Utilities.Extensions;
using R2API;
using RoR2;

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
            if (!sender.inventory)
                return;

            ItemQualityCounts healOnCrit = sender.inventory.GetItemCountsEffective(ItemQualitiesContent.ItemQualityGroups.HealOnCrit);
            if (healOnCrit.TotalQualityCount > 0 && sender.HasBuff(ItemQualitiesContent.Buffs.HealCritBoost))
            {
                float crit;
                switch (healOnCrit.HighestQuality)
                {
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
                        Log.Error($"Quality tier {healOnCrit.HighestQuality} is not implemented");
                        crit = 0f;
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
