using R2API;
using RoR2;
using UnityEngine.Networking;

namespace ItemQualities.Items
{
    static class MoveSpeedOnKill
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

            ItemQualityCounts moveSpeedOnKill = ItemQualitiesContent.ItemQualityGroups.MoveSpeedOnKill.GetItemCountsEffective(sender.inventory);
            BuffQualityCounts killMoveSpeedBuff = ItemQualitiesContent.BuffQualityGroups.KillMoveSpeed.GetBuffCounts(sender);

            float moveSpeedPerBuff = (0.02f * moveSpeedOnKill.UncommonCount) +
                                     (0.03f * moveSpeedOnKill.RareCount) +
                                     (0.04f * moveSpeedOnKill.EpicCount) +
                                     (0.05f * moveSpeedOnKill.LegendaryCount);

            args.moveSpeedMultAdd += moveSpeedPerBuff * killMoveSpeedBuff.TotalQualityCount;
        }

        static void onCharacterDeathGlobal(DamageReport damageReport)
        {
            if (!NetworkServer.active || damageReport == null)
                return;

            if (damageReport.attackerMaster && damageReport.attackerBody)
            {
                ItemQualityCounts moveSpeedOnKill = ItemQualitiesContent.ItemQualityGroups.MoveSpeedOnKill.GetItemCountsEffective(damageReport.attackerMaster.inventory);

                if (moveSpeedOnKill.TotalQualityCount > 0)
                {
                    BuffIndex qualityKillMoveSpeedBuffIndex = ItemQualitiesContent.BuffQualityGroups.KillMoveSpeed.GetBuffIndex(moveSpeedOnKill.HighestQuality);

                    int maxStacks = (7 * moveSpeedOnKill.UncommonCount) +
                                    (15 * moveSpeedOnKill.RareCount) +
                                    (20 * moveSpeedOnKill.EpicCount) +
                                    (25 * moveSpeedOnKill.LegendaryCount);

                    if (damageReport.attackerBody.GetBuffCount(qualityKillMoveSpeedBuffIndex) < maxStacks)
                    {
                        damageReport.attackerBody.AddBuff(qualityKillMoveSpeedBuffIndex);
                    }
                }
            }
        }
    }
}
