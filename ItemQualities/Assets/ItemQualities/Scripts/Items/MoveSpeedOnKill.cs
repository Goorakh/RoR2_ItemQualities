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

            float moveSpeedPerBuff = (0.005f * moveSpeedOnKill.UncommonCount) +
                                     (0.006f * moveSpeedOnKill.RareCount) +
                                     (0.007f * moveSpeedOnKill.EpicCount) +
                                     (0.010f * moveSpeedOnKill.LegendaryCount);

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

                    int maxStacks = (100 * moveSpeedOnKill.UncommonCount) +
                                    (150 * moveSpeedOnKill.RareCount) +
                                    (200 * moveSpeedOnKill.EpicCount) +
                                    (250 * moveSpeedOnKill.LegendaryCount);

                    if (damageReport.attackerBody.GetBuffCount(qualityKillMoveSpeedBuffIndex) < maxStacks)
                    {
                        damageReport.attackerBody.AddBuff(qualityKillMoveSpeedBuffIndex);
                    }
                }
            }
        }
    }
}
