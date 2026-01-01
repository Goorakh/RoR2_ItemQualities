using ItemQualities.Utilities.Extensions;
using RoR2;
using UnityEngine.Networking;

namespace ItemQualities.Items
{
    static class PermanentDebuffOnHit
    {
        [SystemInitializer]
        static void Init()
        {
            On.RoR2.HealthComponent.TakeDamageProcess += HealthComponent_TakeDamageProcess;
        }

        static void HealthComponent_TakeDamageProcess(On.RoR2.HealthComponent.orig_TakeDamageProcess orig, HealthComponent self, DamageInfo damageInfo)
        {
            orig(self, damageInfo);

            if (!NetworkServer.active)
                return;

            if (!damageInfo.attacker)
                return;

            CharacterBody attackerBody = damageInfo.attacker.GetComponent<CharacterBody>();
            if (!attackerBody)
                return;

            if (damageInfo.damage <= 0 || damageInfo.procCoefficient <= 0)
                return;

            CharacterMaster attackerMaster = attackerBody.master;
            if (!attackerMaster || !attackerMaster.inventory)
                return;

            ItemQualityCounts permanentDebuffOnHit = attackerMaster.inventory.GetItemCountsEffective(ItemQualitiesContent.ItemQualityGroups.PermanentDebuffOnHit);

            int extraBuffCount = (permanentDebuffOnHit.UncommonCount * 1) +
                                 (permanentDebuffOnHit.RareCount * 2) +
                                 (permanentDebuffOnHit.EpicCount * 3) +
                                 (permanentDebuffOnHit.LegendaryCount * 5);

            extraBuffCount *= (int)((damageInfo.damage / attackerBody.damage) / 4);

            for (int i = 0; i < extraBuffCount; i++)
            {
                self.body.AddBuff(DLC1Content.Buffs.PermanentDebuff);
            }
        }
    }
}
