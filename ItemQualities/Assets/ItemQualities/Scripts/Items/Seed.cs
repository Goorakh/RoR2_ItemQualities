using ItemQualities.Utilities.Extensions;
using RoR2;

namespace ItemQualities.Items
{
    static class Seed
    {
        [SystemInitializer]
        static void Init()
        {
            GlobalEventManager.onServerDamageDealt += onServerDamageDealt;
        }

        static void onServerDamageDealt(DamageReport damageReport)
        {
            if (!damageReport.attackerBody || !damageReport.attackerBody.inventory)
                return;

            if (damageReport.damageInfo.procCoefficient > 0 && !damageReport.damageInfo.procChainMask.HasProc(ProcType.HealOnHit))
            {
                ItemQualityCounts seed = damageReport.attackerBody.inventory.GetItemCountsEffective(ItemQualitiesContent.ItemQualityGroups.Seed);
                if (seed.TotalQualityCount > 0)
                {
                    ProcChainMask procChainMask = damageReport.damageInfo.procChainMask;
                    procChainMask.AddProc(ProcType.HealOnHit);

                    float healthCoefficientOfDamage = (0.01f * seed.UncommonCount) +
                                                      (0.03f * seed.RareCount) +
                                                      (0.06f * seed.EpicCount) +
                                                      (0.10f * seed.LegendaryCount);

                    damageReport.attackerBody.healthComponent.Heal(healthCoefficientOfDamage * damageReport.damageDealt * damageReport.damageInfo.procCoefficient, procChainMask);
                }
            }
        }
    }
}
