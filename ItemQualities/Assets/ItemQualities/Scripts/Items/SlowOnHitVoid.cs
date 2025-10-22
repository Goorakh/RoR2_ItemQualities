using R2API;
using RoR2;
using UnityEngine;
using UnityEngine.Networking;

namespace ItemQualities.Items
{
    static class SlowOnHitVoid
    {
        static DeployableSlot _rootAreaDeployableSlot = DeployableSlot.None;

        [SystemInitializer]
        static void Init()
        {
            _rootAreaDeployableSlot = DeployableAPI.RegisterDeployableSlot(getRootAreaLimit);

            GlobalEventManager.onCharacterDeathGlobal += onCharacterDeathGlobal;
        }

        static int getRootAreaLimit(CharacterMaster self, int deployableCountMultiplier)
        {
            return 2;
        }

        static void onCharacterDeathGlobal(DamageReport deathReport)
        {
            if (deathReport?.damageInfo == null)
                return;

            if (!NetworkServer.active)
                return;

            Inventory attackerInventory = deathReport.attackerBody ? deathReport.attackerBody.inventory : null;
            if (!attackerInventory)
                return;

            ItemQualityCounts slowOnHitVoid = ItemQualitiesContent.ItemQualityGroups.SlowOnHitVoid.GetItemCounts(attackerInventory);

            if (slowOnHitVoid.TotalQualityCount > 0 && deathReport.victimBody && deathReport.victimBody.HasBuff(RoR2Content.Buffs.Nullified))
            {
                float rootRadius = (4f * slowOnHitVoid.UncommonCount) +
                                   (7f * slowOnHitVoid.RareCount) +
                                   (10f * slowOnHitVoid.EpicCount) +
                                   (15f * slowOnHitVoid.LegendaryCount);

                Vector3 rootAreaPosition = deathReport.victimBody.footPosition;
                if (Physics.Raycast(new Ray(rootAreaPosition, Vector3.down), out RaycastHit hit, 100f, LayerIndex.world.mask))
                {
                    rootAreaPosition = hit.point;
                }

                GameObject rootAreaObj = GameObject.Instantiate(ItemQualitiesContent.NetworkedPrefabs.SlowOnHitRootArea, rootAreaPosition, Quaternion.identity);

                BuffWard rootWard = rootAreaObj.GetComponent<BuffWard>();
                rootWard.Networkradius = rootRadius;

                if (deathReport.attackerMaster)
                {
                    Deployable deployable = rootAreaObj.GetComponent<Deployable>();
                    deathReport.attackerMaster.AddDeployable(deployable, _rootAreaDeployableSlot);
                }

                NetworkServer.Spawn(rootAreaObj);
            }
        }
    }
}
