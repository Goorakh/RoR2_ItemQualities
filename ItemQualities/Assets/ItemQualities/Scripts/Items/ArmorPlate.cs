using R2API;
using RoR2;
using UnityEngine.Networking;

namespace ItemQualities.Items
{
    static class ArmorPlate
    {
        [SystemInitializer]
        static void Init()
        {
            RecalculateStatsAPI.GetStatCoefficients += getStatCoefficients;

            GlobalEventManager.onServerDamageDealt += onServerDamageDealt;
        }

        static void getStatCoefficients(CharacterBody sender, RecalculateStatsAPI.StatHookEventArgs args)
        {
            if (!sender)
                return;

            if (ItemQualitiesContent.BuffQualityGroups.ArmorPlateBuff.GetBuffCounts(sender).TotalCount > 0)
            {
                args.armorAdd += 75f;
            }
        }

        static void onServerDamageDealt(DamageReport damageReport)
        {
            if (!NetworkServer.active || damageReport?.damageInfo == null)
                return;

            if (damageReport.damageDealt <= 0f || damageReport.damageInfo.rejected)
                return;

            CharacterBody victimBody = damageReport.victimBody;
            if (!victimBody)
                return;

            ItemQualityCounts armorPlate = ItemQualitiesContent.ItemQualityGroups.ArmorPlate.GetItemCounts(victimBody.inventory);
            if (armorPlate.TotalQualityCount > 0)
            {
                QualityTier armorPlateQuality = armorPlate.HighestQuality;

                victimBody.AddBuff(ItemQualitiesContent.BuffQualityGroups.ArmorPlateBuildup.GetBuffIndex(armorPlateQuality));

                if (ItemQualitiesContent.BuffQualityGroups.ArmorPlateBuildup.GetBuffCounts(victimBody).TotalCount >= 15)
                {
                    ItemQualitiesContent.BuffQualityGroups.ArmorPlateBuildup.EnsureBuffQualities(victimBody, QualityTier.None);

                    float buffDuration = (3f * armorPlate.UncommonCount) +
                                         (6f * armorPlate.RareCount) +
                                         (9f * armorPlate.EpicCount) +
                                         (12f * armorPlate.LegendaryCount);

                    victimBody.AddTimedBuff(ItemQualitiesContent.BuffQualityGroups.ArmorPlateBuff.GetBuffIndex(armorPlateQuality), buffDuration);
                }
            }
        }
    }
}
