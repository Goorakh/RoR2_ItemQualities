using RoR2;

namespace ItemQualities.Items
{
    static class StrengthenBurn
    {
        [SystemInitializer]
        static void Init()
        {
            IL.RoR2.StrengthenBurnUtils.CheckDotForUpgrade += ItemHooks.CombineGroupedItemCountsPatch;

            GlobalEventManager.onServerDamageDealt += onServerDamageDealt;
        }

        static void onServerDamageDealt(DamageReport damageReport)
        {
            if (damageReport?.damageInfo == null)
                return;

            if (damageReport.victimBody && damageReport.attackerBody && damageReport.attackerMaster && damageReport.attackerMaster.inventory)
            {
                if (damageReport.damageInfo.crit)
                {
                    ItemQualityCounts strengthenBurn = ItemQualitiesContent.ItemQualityGroups.StrengthenBurn.GetItemCounts(damageReport.attackerMaster.inventory);

                    float burnDamageCoefficient = (0.2f * strengthenBurn.UncommonCount) +
                                                  (0.5f * strengthenBurn.RareCount) +
                                                  (0.8f * strengthenBurn.EpicCount) +
                                                  (1.0f * strengthenBurn.LegendaryCount);

                    int maxBurnStacks = (5 * strengthenBurn.UncommonCount) +
                                        (10 * strengthenBurn.RareCount) +
                                        (15 * strengthenBurn.EpicCount) +
                                        (20 * strengthenBurn.LegendaryCount);

                    InflictDotInfo burnDotInfo = new InflictDotInfo
                    {
                        attackerObject = damageReport.attacker,
                        victimObject = damageReport.victim.gameObject,
                        dotIndex = DotController.DotIndex.Burn,
                        totalDamage = burnDamageCoefficient * damageReport.attackerBody.damage,
                        damageMultiplier = 1f,
                        maxStacksFromAttacker = (uint)maxBurnStacks
                    };

                    if (damageReport.attackerMaster && damageReport.attackerMaster.inventory)
                    {
                        StrengthenBurnUtils.CheckDotForUpgrade(damageReport.attackerMaster.inventory, ref burnDotInfo);
                    }

                    DotController.InflictDot(ref burnDotInfo);
                }
            }
        }
    }
}
