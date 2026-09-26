using ItemQualities.Utilities;
using ItemQualities.Utilities.Extensions;
using RoR2;
using RoR2BepInExPack.GameAssetPaths.Version_1_39_0;
using System.Collections.Generic;
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
            AddressableUtil.LoadAssetAsync<Material>(RoR2_Base_Common_VFX.matGhostEffect_mat).OnSuccess(ghostMat =>
            {
                if (!ItemQualitiesContent.NetworkedPrefabs.ScytheEffect.TryGetComponent(out ChildLocator childLocator))
                    return;
                if (!childLocator.TryFindChild("mdlScytheMesh", out Transform scytheMesh))
                    return;
                if (!scytheMesh.TryGetComponent(out MeshRenderer meshRenderer))
                    return;
                List<Material> materials = new List<Material>
                {
                    ghostMat
                };
                meshRenderer.SetSharedMaterials(materials);
            });

            AddressableUtil.LoadAssetAsync<Mesh>(RoR2_Base_HealOnCrit.mdlScythe_fbx).OnSuccess(scytheDisplay =>
            {
                if (!ItemQualitiesContent.NetworkedPrefabs.ScytheEffect.TryGetComponent(out ChildLocator childLocator))
                    return;
                if (!childLocator.TryFindChild("mdlScytheMesh", out Transform scytheMesh))
                    return;
                if (!scytheMesh.TryGetComponent(out MeshFilter meshFilter))
                    return;

                meshFilter.mesh = scytheDisplay;
            });

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
