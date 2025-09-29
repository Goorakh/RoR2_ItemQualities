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

            Inventory.onInventoryChangedGlobal += onInventoryChangedGlobal;

            GlobalEventManager.onCharacterDeathGlobal += onCharacterDeathGlobal;
        }

        static void getStatCoefficients(CharacterBody sender, RecalculateStatsAPI.StatHookEventArgs args)
        {
            if (!sender)
                return;

            ItemQualityCounts moveSpeedOnKill = ItemQualitiesContent.ItemQualityGroups.MoveSpeedOnKill.GetItemCounts(sender.inventory);
            BuffQualityCounts killMoveSpeedBuff = ItemQualitiesContent.BuffQualityGroups.KillMoveSpeed.GetBuffCounts(sender);

            float moveSpeedPerBuff = (0.02f * moveSpeedOnKill.UncommonCount) +
                                     (0.03f * moveSpeedOnKill.RareCount) +
                                     (0.04f * moveSpeedOnKill.EpicCount) +
                                     (0.05f * moveSpeedOnKill.LegendaryCount);

            args.moveSpeedMultAdd += moveSpeedPerBuff * killMoveSpeedBuff.TotalQualityCount;
        }

        static void onInventoryChangedGlobal(Inventory inventory)
        {
            if (!inventory.TryGetComponent(out CharacterMaster master))
                return;

            CharacterBody body = master.GetBody();
            if (!body)
                return;

            QualityTier highestMoveSpeedOnKillQuality = ItemQualitiesContent.ItemQualityGroups.MoveSpeedOnKill.GetHighestQualityInInventory(inventory);
            BuffIndex killMoveSpeedTargetBuffIndex = highestMoveSpeedOnKillQuality > QualityTier.None ? ItemQualitiesContent.BuffQualityGroups.KillMoveSpeed.GetBuffIndex(highestMoveSpeedOnKillQuality) : BuffIndex.None;

            for (QualityTier qualityTier = 0; qualityTier < QualityTier.Count; qualityTier++)
            {
                if (qualityTier != highestMoveSpeedOnKillQuality)
                {
                    BuffIndex killMoveSpeedBuffIndex = ItemQualitiesContent.BuffQualityGroups.KillMoveSpeed.GetBuffIndex(qualityTier);

                    for (int i = body.GetBuffCount(killMoveSpeedBuffIndex); i > 0; i--)
                    {
                        body.RemoveBuff(killMoveSpeedBuffIndex);

                        if (killMoveSpeedTargetBuffIndex != BuffIndex.None)
                        {
                            body.AddBuff(killMoveSpeedTargetBuffIndex);
                        }
                    }
                }
            }
        }

        static void onCharacterDeathGlobal(DamageReport damageReport)
        {
            if (!NetworkServer.active || damageReport == null)
                return;

            if (damageReport.attackerMaster && damageReport.attackerBody)
            {
                QualityTier highestMoveSpeedOnKillQuality = ItemQualitiesContent.ItemQualityGroups.MoveSpeedOnKill.GetHighestQualityInInventory(damageReport.attackerMaster.inventory);

                BuffIndex qualityKillMoveSpeedBuffIndex = ItemQualitiesContent.BuffQualityGroups.KillMoveSpeed.GetBuffIndex(highestMoveSpeedOnKillQuality);

                ItemQualityCounts moveSpeedOnKill = ItemQualitiesContent.ItemQualityGroups.MoveSpeedOnKill.GetItemCounts(damageReport.attackerMaster.inventory);

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
