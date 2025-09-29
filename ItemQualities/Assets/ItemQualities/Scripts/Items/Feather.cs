using R2API;
using RoR2;
using System;

namespace ItemQualities.Items
{
    static class Feather
    {
        [SystemInitializer]
        static void Init()
        {
            RecalculateStatsAPI.GetStatCoefficients += getStatCoefficients;

            On.RoR2.HealthComponent.TakeDamage += HealthComponent_TakeDamage;
        }

        static void getStatCoefficients(CharacterBody sender, RecalculateStatsAPI.StatHookEventArgs args)
        {
            if (!sender)
                return;

            ItemQualityCounts feather = ItemQualitiesContent.ItemQualityGroups.Feather.GetItemCounts(sender.inventory);

            args.jumpPowerMultAdd += (0.10f * feather.UncommonCount) +
                                     (0.15f * feather.RareCount) +
                                     (0.25f * feather.EpicCount) +
                                     (0.45f * feather.LegendaryCount);
        }

        static void HealthComponent_TakeDamage(On.RoR2.HealthComponent.orig_TakeDamage orig, HealthComponent self, DamageInfo damageInfo)
        {
            try
            {
                if ((damageInfo.damageType & DamageType.FallDamage) != 0)
                {
                    CharacterBody body = self ? self.body : null;

                    ItemQualityCounts feather = ItemQualitiesContent.ItemQualityGroups.Feather.GetItemCounts(body ? body.inventory : null);

                    float fallDamageReduction = (5f * feather.UncommonCount) +
                                                (10f * feather.RareCount) +
                                                (15f * feather.EpicCount) +
                                                (25f * feather.LegendaryCount);

                    damageInfo.damage *= 1f - Util.ConvertAmplificationPercentageIntoReductionNormalized(fallDamageReduction / 100f);
                }
            }
            catch (Exception e)
            {
                Log.Error_NoCallerPrefix(e);
            }

            orig(self, damageInfo);
        }
    }
}
