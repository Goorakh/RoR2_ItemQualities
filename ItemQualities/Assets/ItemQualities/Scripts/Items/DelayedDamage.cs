using ItemQualities.Utilities;
using ItemQualities.Utilities.Extensions;
using RoR2;

namespace ItemQualities.Items
{
    static class DelayedDamage
    {
        [SystemInitializer]
        static void Init()
        {
            GlobalEventManager.onCharacterDeathGlobal += onCharacterDeathGlobal;
            GlobalEventManager.onServerDamageDealt += onServerDamageDealt;
        }

        private static void onCharacterDeathGlobal(DamageReport report)
        {
            if (!report.victimBody)
                return;

            BuffQualityCounts delayedDamageDebuff = report.victimBody.GetBuffCounts(ItemQualitiesContent.BuffQualityGroups.DelayedDamageDebuff);

            int repeatCount = (delayedDamageDebuff.UncommonCount * 1) +
                              (delayedDamageDebuff.RareCount * 2) +
                              (delayedDamageDebuff.EpicCount * 3) +
                              (delayedDamageDebuff.LegendaryCount * 4);

            report.victimBody.RemoveAllQualityBuffs(ItemQualitiesContent.BuffQualityGroups.DelayedDamageDebuff);

            for (int i = 0; i < repeatCount; i++)
            {
                GlobalEventManager.instance.OnCharacterDeath(report);
            }
        }

        private static void onServerDamageDealt(DamageReport report)
        {
            if (!report.attackerBody || !report.attackerBody.inventory || !report.victimBody)
                return;

            ItemQualityCounts delayedDamage = report.attackerBody.inventory.GetItemCountsEffective(ItemQualitiesContent.ItemQualityGroups.DelayedDamage);

            if (delayedDamage.TotalQualityCount > 0)
            {
                float chance = (delayedDamage.UncommonCount * 1f) +
                               (delayedDamage.RareCount * 1.5f) +
                               (delayedDamage.EpicCount * 2f) +
                               (delayedDamage.LegendaryCount * 2.5f);

                if (RollUtil.CheckRoll(chance * report.damageInfo.procCoefficient, report.attackerMaster, report.damageInfo.procChainMask.HasProc(ProcType.SureProc)))
                {
                    report.victimBody.AddBuff(ItemQualitiesContent.BuffQualityGroups.DelayedDamageDebuff.GetBuffDef(delayedDamage.HighestQuality));
                }
            }
        }
    }
}
