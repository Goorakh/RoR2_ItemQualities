using ItemQualities.Utilities.Extensions;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using RoR2;
using RoR2.Orbs;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

namespace ItemQualities.Items
{
    static class BounceNearby
    {
        [SystemInitializer]
        static void Init()
        {
            IL.RoR2.GlobalEventManager.ProcessHitEnemy += GlobalEventManager_ProcessHitEnemy;
        }

        static void GlobalEventManager_ProcessHitEnemy(ILContext il)
        {
            if (!il.Method.TryFindParameter<DamageInfo>(out ParameterDefinition damageInfoParameter))
            {
                Log.Error("Failed to find DamageInfo parameter");
                return;
            }

            ILCursor c = new ILCursor(il);

            int foundTargetsListVar = -1;
            if (!c.TryFindNext(out ILCursor[] foundCursors,
                               x => x.MatchLdsfld(typeof(RoR2Content.Items), nameof(RoR2Content.Items.BounceNearby)),
                               x => x.MatchStloc(typeof(List<HurtBox>), il, out foundTargetsListVar),
                               x => x.MatchCallOrCallvirt<BounceOrb>(nameof(BounceOrb.SearchForTargets))))
            {
                Log.Error("Failed to find patch location");
                return;
            }

            c.Goto(foundCursors[2].Next, MoveType.After); // call BounceOrb.SearchForTargets

            c.Emit(OpCodes.Ldarg, damageInfoParameter);
            c.Emit(OpCodes.Ldloc, foundTargetsListVar);
            c.EmitDelegate<Action<DamageInfo, List<HurtBox>>>(tryProcQualityHook);
            
            static void tryProcQualityHook(DamageInfo damageInfo, List<HurtBox> foundTargets)
            {
                if (!NetworkServer.active)
                    return;

                if (foundTargets.Count > 0)
                {
                    CharacterBody attackerBody = damageInfo?.attacker ? damageInfo.attacker.GetComponent<CharacterBody>() : null;
                    Inventory attackerInventory = attackerBody ? attackerBody.inventory : null;

                    ItemQualityCounts bounceNearby = ItemQualitiesContent.ItemQualityGroups.BounceNearby.GetItemCountsEffective(attackerInventory);
                    if (bounceNearby.TotalQualityCount > 0)
                    {
                        float forceDuration = (1f * bounceNearby.UncommonCount) +
                                              (2f * bounceNearby.RareCount) +
                                              (4f * bounceNearby.EpicCount) +
                                              (6f * bounceNearby.LegendaryCount);

                        GameObject delayedForceObj = GameObject.Instantiate(ItemQualitiesContent.NetworkedPrefabs.MeatHookDelayedForce, damageInfo.position, Quaternion.identity);

                        TeamFilter teamFilter = delayedForceObj.GetComponent<TeamFilter>();
                        teamFilter.teamIndex = attackerBody.teamComponent.teamIndex;

                        DestroyOnTimer destroyOnTimer = delayedForceObj.GetComponent<DestroyOnTimer>();
                        destroyOnTimer.duration = forceDuration;

                        NetworkServer.Spawn(delayedForceObj);
                    }
                }
            }
        }
    }
}
