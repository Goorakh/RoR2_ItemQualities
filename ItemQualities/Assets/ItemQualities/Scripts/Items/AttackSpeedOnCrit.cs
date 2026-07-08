using ItemQualities.Utilities.Extensions;
using R2API;
using RoR2;
using UnityEngine.Networking;

namespace ItemQualities.Items
{
    internal static class AttackSpeedOnCrit
    {
        [SystemInitializer]
        private static void Init()
        {
            RecalculateStatsAPI.GetStatCoefficients += getStatCoefficients;

            GlobalEventManager.onCharacterDeathGlobal += onCharacterDeathGlobal;
        }

        private static void getStatCoefficients(CharacterBody sender, RecalculateStatsAPI.StatHookEventArgs args)
        {
            if (!sender || !sender.inventory)
                return;

            ItemQualityCounts attackSpeedOnCrit = sender.inventory.GetItemCountsEffective(ItemQualitiesContent.ItemQualityGroups.AttackSpeedOnCrit);
            BuffQualityCounts attackSpeedOnCritBuff = sender.GetBuffCounts(ItemQualitiesContent.BuffQualityGroups.AttackSpeedOnCrit);

            float attackSpeedPerBuff = (0.01f * attackSpeedOnCrit.UncommonCount) +
                                       (0.02f * attackSpeedOnCrit.RareCount) +
                                       (0.03f * attackSpeedOnCrit.EpicCount) +
                                       (0.05f * attackSpeedOnCrit.LegendaryCount);

            args.attackSpeedMultAdd += attackSpeedPerBuff * attackSpeedOnCritBuff.TotalQualityCount;
        }

        private static void onCharacterDeathGlobal(DamageReport damageReport)
        {
            if (!NetworkServer.active || damageReport?.damageInfo == null)
                return;

            if (damageReport.damageInfo.crit && damageReport.attackerBody && damageReport.attackerBody.inventory)
            {
                ItemQualityCounts attackSpeedOnCrit = damageReport.attackerBody.inventory.GetItemCountsEffective(ItemQualitiesContent.ItemQualityGroups.AttackSpeedOnCrit);

                if (attackSpeedOnCrit.TotalQualityCount > 0)
                {
                    QualityTier highestAttackSpeedOnCritQuality = attackSpeedOnCrit.HighestQuality;

                    BuffIndex qualityAttackSpeedOnCritBuffIndex = ItemQualitiesContent.BuffQualityGroups.AttackSpeedOnCrit.GetBuffIndex(highestAttackSpeedOnCritQuality);

                    int maxStacks = (40 * attackSpeedOnCrit.UncommonCount) +
                                    (45 * attackSpeedOnCrit.RareCount) +
                                    (60 * attackSpeedOnCrit.EpicCount) +
                                    (75 * attackSpeedOnCrit.LegendaryCount);

                    if (damageReport.attackerBody.GetBuffCount(qualityAttackSpeedOnCritBuffIndex) < maxStacks)
                    {
                        damageReport.attackerBody.AddBuff(qualityAttackSpeedOnCritBuffIndex);
                    }
                }
            }
        }
    }
}
