using RoR2;
using UnityEngine;
using UnityEngine.Networking;

namespace ItemQualities.Items
{
    internal static class HealOnCrit
    {
        public static GameObject swingTrailPrefab = null;

        [SystemInitializer(typeof(QualityCatalog))]
        private static void  Init()
        {
            GlobalEventManager.onServerDamageDealt += onServerDamageDealt;
        }

        private static void onServerDamageDealt(DamageReport report)
        {
            if (report == null || !report.attacker || !report.attackerBody || !report.victimBody || report.damageInfo == null)
                return;

            if (report.attackerBody.HasBuff(ItemQualitiesContent.Buffs.HealCritBoost) &&
            report.damageInfo.damageType.damageSource.HasFlag(DamageSource.Primary) &&
            Vector3.Distance(report.attackerBody.corePosition, report.damageInfo.position) <= 13)
            {
                report.attackerBody.RemoveBuff(ItemQualitiesContent.Buffs.HealCritBoost);
                InputBankTest inputBank = report.attacker.GetComponent<InputBankTest>();
                Vector3 lookDirection = inputBank ? inputBank.aimDirection : report.attacker.transform.forward;
                lookDirection = new Vector3(lookDirection.x, 0, lookDirection.z);
                GameObject scytheEffect = GameObject.Instantiate(ItemQualitiesContent.NetworkedPrefabs.ScytheEffect, report.attackerBody.corePosition, Quaternion.LookRotation(lookDirection, Vector3.up));
                GenericOwnership genericOwnership = scytheEffect.GetComponent<GenericOwnership>();
                genericOwnership.ownerObject = report.attacker;
                NetworkServer.Spawn(scytheEffect);
            }
        }
    }
}
