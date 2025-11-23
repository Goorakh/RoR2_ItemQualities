using RoR2;
using RoR2.Orbs;
using UnityEngine;
using UnityEngine.Networking;

namespace ItemQualities.Items
{
    static class Infusion
    {
        [SystemInitializer]
        static void Init()
        {
            GlobalEventManager.onCharacterDeathGlobal += onCharacterDeathGlobal;
        }

        static void onCharacterDeathGlobal(DamageReport damageReport)
        {
            if (!NetworkServer.active)
                return;

            if (damageReport?.damageInfo == null)
                return;

            if (!damageReport.victimIsBoss && !damageReport.victimIsChampion)
                return;

            Vector3 victimPosition = damageReport.damageInfo.position;
            if (damageReport.victim)
            {
                victimPosition = damageReport.victim.transform.position;
            }

            if (damageReport.victimBody)
            {
                victimPosition = damageReport.victimBody.corePosition;
            }

            if (damageReport.attackerBody && damageReport.attackerMaster)
            {
                ItemQualityCounts infusion = ItemQualitiesContent.ItemQualityGroups.Infusion.GetItemCountsEffective(damageReport.attackerMaster.inventory);

                if (infusion.TotalQualityCount > 0)
                {
                    int infusionBonus = (10 * infusion.UncommonCount) +
                                        (30 * infusion.RareCount) +
                                        (50 * infusion.EpicCount) +
                                        (100 * infusion.LegendaryCount);

                    InfusionOrb infusionOrb = new InfusionOrb
                    {
                        origin = victimPosition,
                        target = Util.FindBodyMainHurtBox(damageReport.attackerBody),
                        maxHpValue = infusionBonus
                    };

                    OrbManager.instance.AddOrb(infusionOrb);
                }
            }
        }
    }
}
