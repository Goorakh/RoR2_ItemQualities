using ItemQualities.Utilities.Extensions;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using R2API;
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
            ExecuteAPI.CalculateExecuteThresholdForViewer += calculateExecuteThreshold;

            IL.RoR2.GlobalEventManager.ProcessHitEnemy += GlobalEventManager_ProcessHitEnemy;
        }

        static void calculateExecuteThreshold(CharacterBody victimBody, CharacterBody viewerBody, ref float highestExecuteThreshold)
        {
            if (!victimBody || !victimBody.healthComponent || !viewerBody)
                return;

            if (victimBody.healthComponent.isInFrozenState)
            {
                highestExecuteThreshold = Mathf.Max(highestExecuteThreshold, GetFreezeExecuteThreshold(viewerBody));
            }
        }

        public static float GetFreezeExecuteThreshold(CharacterBody attackerBody)
        {
            return getFreezeExecuteThreshold(HealthComponent.frozenExecuteThreshold, attackerBody);
        }

        static float getFreezeExecuteThreshold(float defaultFreezeThreshold, CharacterBody attackerBody)
        {
            float freezeThreshold = defaultFreezeThreshold;

            Inventory inventory = attackerBody ? attackerBody.inventory : null;
            if (inventory)
            {
                ItemQualityCounts iceRing = inventory.GetItemCountsEffective(ItemQualitiesContent.ItemQualityGroups.IceRing);
                if (iceRing.TotalQualityCount > 0)
                {
                    float freezeThresholdReduction = (0.05f * iceRing.UncommonCount) +
                                                     (0.10f * iceRing.RareCount) +
                                                     (0.20f * iceRing.EpicCount) +
                                                     (0.40f * iceRing.LegendaryCount);

                    freezeThreshold = 1f - ((1f - freezeThreshold) / (1f + freezeThresholdReduction));
                }
            }

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
                    if (attackerInventory)
                    {
                        ItemQualityCounts iceRing = attackerInventory.GetItemCountsEffective(ItemQualitiesContent.ItemQualityGroups.IceRing);
                        if (iceRing.TotalQualityCount > 0)
                        {
                            iceBandDamageInfo.damageType |= DamageType.Freeze2s;
                        }
                    }
                }

                return iceBandDamageInfo;
            }
        }
    }
}
