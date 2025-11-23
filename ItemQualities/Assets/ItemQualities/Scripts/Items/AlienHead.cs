using ItemQualities.Utilities;
using RoR2;
using UnityEngine.Networking;

namespace ItemQualities.Items
{
    static class AlienHead
    {
        [SystemInitializer]
        static void Init()
        {
            GlobalEventManager.onCharacterDeathGlobal += onCharacterDeathGlobal;
        }

        static void onCharacterDeathGlobal(DamageReport deathReport)
        {
            if (!NetworkServer.active)
                return;

            if (!deathReport?.attackerBody)
                return;

            ItemQualityCounts alienHead = ItemQualitiesContent.ItemQualityGroups.AlienHead.GetItemCountsEffective(deathReport.attackerBody.inventory);
            if (alienHead.TotalQualityCount > 0)
            {
                float cooldownReductionChance = (15f * alienHead.UncommonCount) +
                                                (35f * alienHead.RareCount) +
                                                (75f * alienHead.EpicCount) +
                                                (100f * alienHead.LegendaryCount);

                float cooldownReduction = 1f * RollUtil.GetOverflowRoll(cooldownReductionChance, deathReport.attackerMaster);
                if (cooldownReduction > 0f)
                {
                    deathReport.attackerBody.skillLocator.DeductCooldownFromAllSkillsServer(cooldownReduction);
                }
            }
        }
    }
}
