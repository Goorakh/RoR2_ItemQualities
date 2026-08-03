using HG;
using ItemQualities.Utilities;
using ItemQualities.Utilities.Extensions;
using RoR2;
using System.Collections.Generic;

namespace ItemQualities.Items
{
    internal static class ParentEgg
    {
        [SystemInitializer]
        private static void Init()
        {
            GlobalEventManager.onServerDamageDealt += OnServerDamageDealt;
            On.RoR2.GrandParentSunController.SearchForTargets += TeamFilterFix;
        }

        private static void TeamFilterFix(On.RoR2.GrandParentSunController.orig_SearchForTargets orig, GrandParentSunController self, List<HurtBox> dest)
        {
            self.bullseyeSearch.teamMaskFilter = TeamMask.AllExcept(self.teamFilter.teamIndex);
            orig(self, dest);
        }

        private static void OnServerDamageDealt(DamageReport report)
        {
            if (!report.attackerBody || !report.attackerBody.inventory || !report.victimBody)
                return;

            ItemQualityCounts parentEgg = report.attackerBody.inventory.GetItemCountsEffective(ItemQualitiesContent.ItemQualityGroups.ParentEgg);

            if (parentEgg.TotalQualityCount > 0)
            {
                if (RollUtil.CheckRoll(100 * report.damageInfo.procCoefficient, report.attackerMaster, report.damageInfo.procChainMask.HasProc(ProcType.SureProc)))
                {
                    float buffDuration =    (parentEgg.UncommonCount * 1f) +
                                            (parentEgg.RareCount * 1.5f) +
                                            (parentEgg.EpicCount * 2f) +
                                            (parentEgg.LegendaryCount * 2.5f);

                    BuffDef heatBuff = ItemQualitiesContent.BuffQualityGroups.ParentEggOverheat.GetBuffDef(parentEgg.HighestQuality);
                    report.victimBody.SetTimedBuffDurationIfPresent(heatBuff, buffDuration, true);
                    report.victimBody.AddTimedBuff(heatBuff, buffDuration);
                }
                if (allowSun(report.victimBody))
                {
                    ParentEggSunHandler parentEggSunHandler = report.victimBody.EnsureComponent<ParentEggSunHandler>();
                    parentEggSunHandler.owner = report.attackerBody;
                }
            }
        }

        public static bool allowSun(CharacterBody victim)
        {
            if (!victim || !victim.healthComponent || victim.healthComponent.health <= 0)
            {
                return false;
            }

            BuffQualityCounts parentEggOverheat = victim.GetBuffCounts(ItemQualitiesContent.BuffQualityGroups.ParentEggOverheat);
            if (parentEggOverheat.TotalQualityCount >= 15)
            {
                return true;
            }
            return false;
        }

        public static int SunRange(CharacterBody attacker)
        {
            ItemQualityCounts parentEgg = attacker.inventory.GetItemCountsEffective(ItemQualitiesContent.ItemQualityGroups.ParentEgg);
            return (parentEgg.UncommonCount * 20) +
                    (parentEgg.RareCount * 30) +
                    (parentEgg.EpicCount * 40) +
                    (parentEgg.LegendaryCount * 50);
        }
    }
}
