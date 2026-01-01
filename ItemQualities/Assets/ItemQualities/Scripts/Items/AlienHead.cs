using ItemQualities.Utilities;
using ItemQualities.Utilities.Extensions;
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

            CharacterBody attackerBody = deathReport?.attackerBody;
            if (!attackerBody || !attackerBody.inventory)
                return;

            ItemQualityCounts alienHead = attackerBody.inventory.GetItemCountsEffective(ItemQualitiesContent.ItemQualityGroups.AlienHead);
            if (alienHead.TotalQualityCount > 0)
            {
                float cooldownReductionChance = (15f * alienHead.UncommonCount) +
                                                (35f * alienHead.RareCount) +
                                                (75f * alienHead.EpicCount) +
                                                (100f * alienHead.LegendaryCount);

                float cooldownReduction = 1f * RollUtil.GetOverflowRoll(cooldownReductionChance, deathReport.attackerMaster, deathReport.damageInfo.procChainMask.HasProc(ProcType.SureProc));
                if (cooldownReduction > 0f)
                {
                    attackerBody.skillLocator.DeductCooldownFromAllSkillsServer(cooldownReduction);
                }
            }
        }
    }
}
