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
            if (!NetworkServer.active) return;
            CharacterBody attackerBody = damageInfo.attacker.GetComponent<CharacterBody>();
            if (damageInfo.damage <= 0 || !attackerBody) return;
            CharacterMaster characterMaster = attackerBody.master;
            if (!characterMaster || !characterMaster.inventory || damageInfo.procCoefficient <= 0f) return;

            ItemQualityCounts PermanentDebuffOnHit = ItemQualitiesContent.ItemQualityGroups.PermanentDebuffOnHit.GetItemCounts(characterMaster.inventory);
            int total = PermanentDebuffOnHit.UncommonCount +
                        PermanentDebuffOnHit.RareCount * 2 +
                        PermanentDebuffOnHit.EpicCount * 3 +
                        PermanentDebuffOnHit.LegendaryCount * 5;

            total *= (int)(damageInfo.damage / attackerBody.damage / 4);
            self.body.SetBuffCount(DLC1Content.Buffs.PermanentDebuff.buffIndex, self.body.buffs[(int)DLC1Content.Buffs.PermanentDebuff.buffIndex] + total);
        }
    }
}
