using ItemQualities.Utilities.Extensions;
using R2API;
using RoR2;

namespace ItemQualities.Items
{
    static class StrengthenBurn
    {
        [SystemInitializer]
        static void Init()
        {
            RecalculateStatsAPI.GetStatCoefficients += getStatCoefficients;

            GlobalEventManager.onServerDamageDealt += onServerDamageDealt;
        }

        static void getStatCoefficients(CharacterBody sender, RecalculateStatsAPI.StatHookEventArgs args)
        {
            if (!sender.inventory)
                return;

            ItemQualityCounts strengthenBurn = sender.inventory.GetItemCountsEffective(ItemQualitiesContent.ItemQualityGroups.StrengthenBurn);
            if (strengthenBurn.TotalQualityCount > 0)
            {
                args.critAdd += 5f;
            }
        }

        static void onServerDamageDealt(DamageReport damageReport)
        {
            if (damageReport?.damageInfo == null)
                return;

            if (damageReport.damageInfo.crit)
            {
                if (damageReport.victimBody && damageReport.attackerBody && damageReport.attackerMaster && damageReport.attackerMaster.inventory)
                {
                    ItemQualityCounts strengthenBurn = damageReport.attackerMaster.inventory.GetItemCountsEffective(ItemQualitiesContent.ItemQualityGroups.StrengthenBurn);

                    if (strengthenBurn.TotalQualityCount > 0)
                    {
                        float burnDamageCoefficient = (0.1f * strengthenBurn.UncommonCount) +
                                                      (0.2f * strengthenBurn.RareCount) +
                                                      (0.3f * strengthenBurn.EpicCount) +
                                                      (0.5f * strengthenBurn.LegendaryCount);

                        uint maxBurnStacks = 5;

                        InflictDotInfo burnDotInfo = new InflictDotInfo
                        {
                            attackerObject = damageReport.attacker,
                            victimObject = damageReport.victim.gameObject,
                            dotIndex = DotController.DotIndex.Burn,
                            totalDamage = burnDamageCoefficient * damageReport.attackerBody.damage,
                            damageMultiplier = 1f,
                            maxStacksFromAttacker = maxBurnStacks
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
}
