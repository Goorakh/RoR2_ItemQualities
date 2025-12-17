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

            if (damageReport.attackerBody && damageReport.attackerMaster)
            {
                if (damageReport.victimIsBoss || damageReport.victimIsChampion)
                {
                    Vector3 victimPosition = damageReport.damageInfo.position;
                    if (damageReport.victimBody)
                    {
                        victimPosition = damageReport.victimBody.corePosition;
                    }

                    ItemQualityCounts infusion = ItemQualitiesContent.ItemQualityGroups.Infusion.GetItemCountsEffective(damageReport.attackerMaster.inventory);

                    if (infusion.TotalQualityCount > 0)
                    {
                        int infusionBonus = (5 * infusion.UncommonCount) +
                                            (15 * infusion.RareCount) +
                                            (30 * infusion.EpicCount) +
                                            (50 * infusion.LegendaryCount);

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
}
