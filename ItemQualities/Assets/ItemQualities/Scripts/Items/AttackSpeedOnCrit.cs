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

            ItemQualityCounts attackSpeedOnCrit = ItemQualitiesContent.ItemQualityGroups.AttackSpeedOnCrit.GetItemCounts(sender.inventory);
            BuffQualityCounts attackSpeedOnCritBuff = ItemQualitiesContent.BuffQualityGroups.AttackSpeedOnCrit.GetBuffCounts(sender);

            float attackSpeedPerBuff = (0.05f * attackSpeedOnCrit.UncommonCount) +
                                       (0.10f * attackSpeedOnCrit.RareCount) +
                                       (0.15f * attackSpeedOnCrit.EpicCount) +
                                       (0.25f * attackSpeedOnCrit.LegendaryCount);

            args.attackSpeedMultAdd += attackSpeedPerBuff * attackSpeedOnCritBuff.TotalQualityCount;
        }

        static void onCharacterDeathGlobal(DamageReport damageReport)
        {
            if (!NetworkServer.active || damageReport?.damageInfo == null)
                return;

            if (damageReport.damageInfo.crit && damageReport.attackerMaster && damageReport.attackerBody)
            {
                QualityTier highestAttackSpeedOnCritQuality = ItemQualitiesContent.ItemQualityGroups.AttackSpeedOnCrit.GetHighestQualityInInventory(damageReport.attackerMaster.inventory);

                BuffIndex qualityAttackSpeedOnCritBuffIndex = ItemQualitiesContent.BuffQualityGroups.AttackSpeedOnCrit.GetBuffIndex(highestAttackSpeedOnCritQuality);

                ItemQualityCounts attackSpeedOnCrit = ItemQualitiesContent.ItemQualityGroups.AttackSpeedOnCrit.GetItemCounts(damageReport.attackerMaster.inventory);

                int maxStacks = (6 * attackSpeedOnCrit.UncommonCount) +
                                (9 * attackSpeedOnCrit.RareCount) +
                                (12 * attackSpeedOnCrit.EpicCount) +
                                (15 * attackSpeedOnCrit.LegendaryCount);

                if (damageReport.attackerBody.GetBuffCount(qualityAttackSpeedOnCritBuffIndex) < maxStacks)
                {
                    damageReport.attackerBody.AddBuff(qualityAttackSpeedOnCritBuffIndex);
                }
            }
        }
    }
}
