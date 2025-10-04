using ItemQualities.Utilities.Extensions;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using RoR2;
using System;
using UnityEngine;

namespace ItemQualities.Items
{
    static class IceRing
    {
        [SystemInitializer]
        static void Init()
        {
            IL.RoR2.GlobalEventManager.ProcessHitEnemy += GlobalEventManager_ProcessHitEnemy;

            IL.RoR2.HealthComponent.TakeDamageProcess += HealthComponent_TakeDamageProcess;
        }

        public static float GetFreezeExecuteThreshold(CharacterBody attackerBody)
        {
            return getFreezeExecuteThreshold(HealthComponent.frozenExecuteThreshold, attackerBody);
        }

        static float getFreezeExecuteThreshold(float defaultFreezeThreshold, CharacterBody attackerBody)
        {
            Inventory inventory = attackerBody ? attackerBody.inventory : null;

            ItemQualityCounts iceRing = ItemQualitiesContent.ItemQualityGroups.IceRing.GetItemCounts(inventory);

            float freezeThreshold = defaultFreezeThreshold;

            float freezeThresholdReduction = (0.05f * iceRing.UncommonCount) +
                                             (0.10f * iceRing.RareCount) +
                                             (0.20f * iceRing.EpicCount) +
                                             (0.40f * iceRing.LegendaryCount);

            freezeThreshold = 1f - ((1f - freezeThreshold) / (1f + freezeThresholdReduction));

            return freezeThreshold;
        }

        static void GlobalEventManager_ProcessHitEnemy(ILContext il)
        {
            ILCursor c = new ILCursor(il);

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

            if (!c.TryFindNext(out ILCursor[] foundCursors,
                               x => x.MatchLdsfld(typeof(RoR2Content.Items), nameof(RoR2Content.Items.IceRing)),
                               x => x.MatchLdstr("Prefabs/Effects/ImpactEffects/IceRingExplosion"),
                               x => x.MatchCallOrCallvirt<HealthComponent>(nameof(HealthComponent.TakeDamage))))
            {
                Log.Error("Failed to find patch location");
                return;
            }

            c.Goto(foundCursors[2].Next, MoveType.Before);

            c.Emit(OpCodes.Ldarg, damageInfoParameter);
            c.EmitDelegate<Func<DamageInfo, DamageInfo, DamageInfo>>(getIceBandDamageInfo);

            static DamageInfo getIceBandDamageInfo(DamageInfo iceBandDamageInfo, DamageInfo procDamageInfo)
            {
                if (iceBandDamageInfo != null)
                {
                    CharacterBody attackerBody = procDamageInfo?.attacker ? procDamageInfo.attacker.GetComponent<CharacterBody>() : null;
                    Inventory attackerInventory = attackerBody ? attackerBody.inventory : null;

                    ItemQualityCounts iceRing = ItemQualitiesContent.ItemQualityGroups.IceRing.GetItemCounts(attackerInventory);
                    if (iceRing.TotalQualityCount > 0)
                    {
                        iceBandDamageInfo.damageType |= DamageType.Freeze2s;
                    }
                }

                return iceBandDamageInfo;
            }
        }

        static void HealthComponent_TakeDamageProcess(ILContext il)
        {
            ILCursor c = new ILCursor(il);

            if (!il.Method.TryFindParameter<DamageInfo>(out ParameterDefinition damageInfoParameter))
            {
                Log.Error("Failed to find DamageInfo parameter");
                return;
            }

            if (!c.TryGotoNext(x => x.MatchCallOrCallvirt<HealthComponent>("get_" + nameof(HealthComponent.isInFrozenState))))
            {
                Log.Error("Failed to find patch location");
                return;
            }

            int patchCount = 0;

            while (c.TryGotoNext(MoveType.After,
                                 x => x.MatchLdcR4(HealthComponent.frozenExecuteThreshold)))
            {
                c.Emit(OpCodes.Ldarg, damageInfoParameter);
                c.EmitDelegate<Func<float, DamageInfo, float>>(getAttackerFrozenExecuteThreshold);

                static float getAttackerFrozenExecuteThreshold(float executeThreshold, DamageInfo attackerDamageInfo)
                {
                    CharacterBody attackerBody = attackerDamageInfo?.attacker ? attackerDamageInfo.attacker.GetComponent<CharacterBody>() : null;
                    return getFreezeExecuteThreshold(executeThreshold, attackerBody);
                }

                patchCount++;
            }

            if (patchCount == 0)
            {
                Log.Error("Failed to find freeze execute threshold patch location");
            }
            else if (patchCount != 2)
            {
                Log.Warning($"Unexpected freeze execute threshold patch count: {patchCount}, investigate/adjust expected value");
            }
            else
            {
                Log.Debug($"Found {patchCount} freeze execute threshold patch location(s)");
            }
        }
    }
}
