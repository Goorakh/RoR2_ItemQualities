using ItemQualities.Orbs;
using ItemQualities.Utilities.Extensions;
using RoR2;
using RoR2.Orbs;
using UnityEngine;
using UnityEngine.Networking;

namespace ItemQualities.Items
{
    static class HealWhileSafe
    {
        [SystemInitializer]
        static void Init()
        {
            GlobalEventManager.onCharacterDeathGlobal += onCharacterDeathGlobal;
        }

        static void onCharacterDeathGlobal(DamageReport damageReport)
        {
            if (!NetworkServer.active || damageReport?.damageInfo == null)
                return;

            if (!damageReport.attackerBody || !damageReport.attackerBody.outOfDanger || !damageReport.attackerBody.inventory)
                return;

            ItemQualityCounts slug = damageReport.attackerBody.inventory.GetItemCountsEffective(ItemQualitiesContent.ItemQualityGroups.HealWhileSafe);
            if (slug.TotalQualityCount == 0)
                return;

            int healthPerKill = (2 * slug.UncommonCount) +
                                (4 * slug.RareCount) +
                                (8 * slug.EpicCount) +
                                (12 * slug.LegendaryCount);

            int maxHealthIncrease = healthPerKill * 50;

            int currentHealthIncrease = damageReport.attackerBody.GetBuffCount(ItemQualitiesContent.Buffs.SlugHealth);

            int healthToAdd = Mathf.Min(healthPerKill, maxHealthIncrease - currentHealthIncrease);
            if (healthToAdd > 0)
            {
                SlugOrb slugOrb = new SlugOrb
                {
                    target = damageReport.attackerBody.mainHurtBox,
                    origin = damageReport.victimBody ? damageReport.victimBody.corePosition : damageReport.damageInfo.position,
                    SlugBuffCount = healthToAdd
                };

                OrbManager.instance.AddOrb(slugOrb);
            }
        }
    }
}
