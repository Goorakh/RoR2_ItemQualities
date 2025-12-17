using R2API;
using RoR2;
using UnityEngine.Networking;

namespace ItemQualities.Items
{
    static class AttackSpeedOnCrit
    {
        [SystemInitializer]
        static void Init()
        {
            RecalculateStatsAPI.GetStatCoefficients += getStatCoefficients;

            GlobalEventManager.onCharacterDeathGlobal += onCharacterDeathGlobal;
        }

        static void getStatCoefficients(CharacterBody sender, RecalculateStatsAPI.StatHookEventArgs args)
        {
            if (!sender)
                return;

            ItemQualityCounts attackSpeedOnCrit = ItemQualitiesContent.ItemQualityGroups.AttackSpeedOnCrit.GetItemCountsEffective(sender.inventory);
            BuffQualityCounts attackSpeedOnCritBuff = ItemQualitiesContent.BuffQualityGroups.AttackSpeedOnCrit.GetBuffCounts(sender);

            float attackSpeedPerBuff = (0.01f * attackSpeedOnCrit.UncommonCount) +
                                       (0.02f * attackSpeedOnCrit.RareCount) +
                                       (0.03f * attackSpeedOnCrit.EpicCount) +
                                       (0.05f * attackSpeedOnCrit.LegendaryCount);

            args.attackSpeedMultAdd += attackSpeedPerBuff * attackSpeedOnCritBuff.TotalQualityCount;
        }

        static void onCharacterDeathGlobal(DamageReport damageReport)
        {
            if (!NetworkServer.active || damageReport?.damageInfo == null)
                return;

            if (damageReport.damageInfo.crit && damageReport.attackerMaster && damageReport.attackerBody)
            {
                ItemQualityCounts attackSpeedOnCrit = ItemQualitiesContent.ItemQualityGroups.AttackSpeedOnCrit.GetItemCountsEffective(damageReport.attackerMaster.inventory);

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
