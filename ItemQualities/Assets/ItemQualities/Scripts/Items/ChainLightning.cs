using ItemQualities.Utilities.Extensions;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using RoR2;
using RoR2.Orbs;
using System;
using UnityEngine;

namespace ItemQualities.Items
{
    static class ChainLightning
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

            if (!il.Method.TryFindParameter<GameObject>("victim", out ParameterDefinition victimParameter))
            {
                Log.Error("Failed to find victim parameter");
                return;
            }

            ILCursor c = new ILCursor(il);

            if (!c.TryFindNext(out ILCursor[] foundCursors,
                               x => x.MatchLdsfld(typeof(RoR2Content.Items), nameof(RoR2Content.Items.ChainLightning)),
                               x => x.MatchNewobj<LightningOrb>(),
                               x => x.MatchCallOrCallvirt<OrbManager>(nameof(OrbManager.AddOrb))))
            {
                Log.Error("Failed to find patch location");
                return;
            }

            c.Goto(foundCursors[2].Next, MoveType.After);

            c.Emit(OpCodes.Ldarg, damageInfoParameter);
            c.Emit(OpCodes.Ldarg, victimParameter);
            c.EmitDelegate<Action<DamageInfo, GameObject>>(handleQualityUkuleleProc);

            static void handleQualityUkuleleProc(DamageInfo damageInfo, GameObject victim)
            {
                if (damageInfo == null || !victim)
                    return;

                CharacterBody attackerBody = damageInfo.attacker ? damageInfo.attacker.GetComponent<CharacterBody>() : null;
                Inventory attackerInventory = attackerBody ? attackerBody.inventory : null;

                ItemQualityCounts chainLightning = ItemQualitiesContent.ItemQualityGroups.ChainLightning.GetItemCounts(attackerInventory);
                if (chainLightning.TotalQualityCount > 0)
                {
                    int arcCount = (3 * chainLightning.UncommonCount) +
                                   (5 * chainLightning.RareCount) +
                                   (8 * chainLightning.EpicCount) +
                                   (12 * chainLightning.LegendaryCount);

                    if (arcCount > 0)
                    {
                        ChainLightningArcController.AddToBody(victim, damageInfo.attacker, damageInfo.crit, ChainLightningArcController.FireInterval * arcCount);
                    }
                }
            }
        }
    }
}
