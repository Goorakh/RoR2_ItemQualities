using HG;
using ItemQualities.Orbs;
using R2API;
using RoR2;
using RoR2.Orbs;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

namespace ItemQualities.Items
{
    static class CritGlassesVoid
    {
        [SystemInitializer]
        static void Init()
        {
            GlobalEventManager.onServerCharacterExecuted += onServerCharacterExecuted;
        }

        static void onServerCharacterExecuted(DamageReport damageReport, float executedHealth)
        {
            if (!NetworkServer.active || damageReport?.damageInfo == null)
                return;

            if ((damageReport.damageInfo.damageType & DamageType.VoidDeath) == 0 ||
                damageReport.damageInfo.procChainMask.HasModdedProc(VoidDeathOrb.VoidDeathOrbProcType))
            {
                return;
            }

            if (!damageReport.attackerBody)
                return;

            ItemQualityCounts critGlassesVoid = ItemQualitiesContent.ItemQualityGroups.CritGlassesVoid.GetItemCountsEffective(damageReport.attackerBody.inventory);
            if (critGlassesVoid.TotalQualityCount <= 0)
                return;

            float searchRadius = (15f * critGlassesVoid.UncommonCount) +
                                 (25f * critGlassesVoid.RareCount) +
                                 (35f * critGlassesVoid.EpicCount) +
                                 (50f * critGlassesVoid.LegendaryCount);

            if (searchRadius > 0)
            {
                SphereSearch sphereSearch = new SphereSearch
                {
                    origin = damageReport.damageInfo.position,
                    radius = ExplodeOnDeath.GetExplosionRadius(searchRadius, damageReport.attackerBody),
                    mask = LayerIndex.entityPrecise.mask,
                    queryTriggerInteraction = QueryTriggerInteraction.Ignore,
                };

                sphereSearch.RefreshCandidates();
                sphereSearch.FilterCandidatesByHurtBoxTeam(TeamMask.GetEnemyTeams(damageReport.attackerTeamIndex));
                sphereSearch.FilterCandidatesByDistinctHurtBoxEntities();

                List<HurtBox> targetHurtBoxes = ListPool<HurtBox>.RentCollection();
                try
                {
                    sphereSearch.GetHurtBoxes(targetHurtBoxes);

                    foreach (HurtBox hurtBox in targetHurtBoxes)
                    {
                        if (hurtBox &&
                            hurtBox.healthComponent &&
                            hurtBox.healthComponent.alive &&
                            hurtBox.healthComponent.gameObject != damageReport.attacker &&
                            hurtBox.healthComponent != damageReport.victim &&
                            hurtBox.healthComponent.body &&
                            !hurtBox.healthComponent.body.isBoss &&
                            (hurtBox.healthComponent.body.bodyFlags & CharacterBody.BodyFlags.ImmuneToVoidDeath) == 0)
                        {
                            VoidDeathOrb orb = new VoidDeathOrb
                            {
                                target = hurtBox,
                                origin = damageReport.victimBody ? damageReport.victimBody.corePosition : damageReport.damageInfo.position,
                                Attacker = damageReport.attacker,
                            };

                            OrbManager.instance.AddOrb(orb);
                        }
                    }
                }
                finally
                {
                    targetHurtBoxes = ListPool<HurtBox>.ReturnCollection(targetHurtBoxes);
                }
            }
        }
    }
}
