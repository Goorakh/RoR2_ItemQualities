using ItemQualities.Utilities.Extensions;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.Utils;
using RoR2;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace ItemQualities.Items
{
    static class Medkit
    {
        [SystemInitializer]
        static void Init()
        {
            IL.RoR2.HealthComponent.UpdateLastHitTime += HealthComponent_UpdateLastHitTime;

            IL.RoR2.CharacterBody.RemoveBuff_BuffIndex += CharacterBody_RemoveBuff_BuffIndex;
        }

        static void CharacterBody_RemoveBuff_BuffIndex(ILContext il)
        {
            ILCursor c = new ILCursor(il);

            if (!c.TryFindNext(out ILCursor[] foundCursors,
                               x => x.MatchLdsfld(typeof(RoR2Content.Buffs), nameof(RoR2Content.Buffs.MedkitHeal)),
                               x => x.MatchCallOrCallvirt<HealthComponent>(nameof(HealthComponent.Heal))))
            {
                Log.Error("Failed to find patch location");
                return;
            }

            c.Goto(foundCursors[1].Next, MoveType.Before);

            MethodReference healMethod = (MethodReference)c.Next.Operand;

            List<VariableDefinition> tempHealMethodParameterVars = new List<VariableDefinition>();
            for (int i = healMethod.Parameters.Count - 1; i >= 0; i--)
            {
                ParameterDefinition parameter = healMethod.Parameters[i];
                if (parameter.ParameterType.Is(typeof(float)))
                    break;

                tempHealMethodParameterVars.Add(il.AddVariable(parameter.ParameterType));
            }

            tempHealMethodParameterVars.Reverse();

            for (int i = tempHealMethodParameterVars.Count - 1; i >= 0; i--)
            {
                c.Emit(OpCodes.Stloc, tempHealMethodParameterVars[i]);
            }

            c.Emit(OpCodes.Ldarg_0);
            c.EmitDelegate<Func<float, CharacterBody, float>>(getHealAmount);

            static float getHealAmount(float healAmount, CharacterBody body)
            {
                if (body && body.inventory)
                {
                    ItemQualityCounts medkit = body.inventory.GetItemCountsEffective(ItemQualitiesContent.ItemQualityGroups.Medkit);
                    if (medkit.TotalQualityCount > 0 && body.TryGetComponentCached(out CharacterBodyExtraStatsTracker bodyExtraStats))
                    {
                        float timeSinceLastHit = bodyExtraStats.CurrentMedkitProcTimeSinceLastHit;
                        bodyExtraStats.CurrentMedkitProcTimeSinceLastHit = 0f;
                        if (timeSinceLastHit > 0f)
                        {
                            float healingIncreasePerSecond = (0.01f * medkit.UncommonCount) +
                                                             (0.02f * medkit.RareCount) +
                                                             (0.04f * medkit.EpicCount) +
                                                             (0.08f * medkit.LegendaryCount);

                            float maxHealingIncrease = (1.0f * medkit.UncommonCount) +
                                                       (1.5f * medkit.RareCount) +
                                                       (2.5f * medkit.EpicCount) +
                                                       (4.0f * medkit.LegendaryCount);

                            float healingMultiplier = 1f + Mathf.Min(maxHealingIncrease, healingIncreasePerSecond * timeSinceLastHit);

                            Log.Debug($"Time since last hit: {timeSinceLastHit}, multiplier: {healingMultiplier}");

                            healAmount *= healingMultiplier;
                        }
                    }
                }

                return healAmount;
            }

            for (int i = 0; i < tempHealMethodParameterVars.Count; i++)
            {
                c.Emit(OpCodes.Ldloc, tempHealMethodParameterVars[i]);
            }
        }

        static void HealthComponent_UpdateLastHitTime(ILContext il)
        {
            ILCursor c = new ILCursor(il);

            if (!c.TryFindNext(out ILCursor[] foundCursors,
                               x => x.MatchLdfld<HealthComponent.ItemCounts>(nameof(HealthComponent.ItemCounts.medkit)),
                               x => x.MatchLdsfld(typeof(RoR2Content.Buffs), nameof(RoR2Content.Buffs.MedkitHeal)),
                               x => x.MatchCallOrCallvirt<CharacterBody>(nameof(CharacterBody.AddTimedBuff))))
            {
                Log.Error("Failed to find patch location");
                return;
            }

            c.Goto(foundCursors[2].Next, MoveType.After);

            c.Emit(OpCodes.Ldarg_0);
            c.EmitDelegate<Action<HealthComponent>>(onMedkitProc);

            static void onMedkitProc(HealthComponent healthComponent)
            {
                if (healthComponent && healthComponent.TryGetComponentCached(out CharacterBodyExtraStatsTracker bodyExtraStats))
                {
                    Run.FixedTimeStamp lastHitTime = healthComponent.lastHitTime;
                    if (lastHitTime.isInfinity && healthComponent.body)
                    {
                        lastHitTime = healthComponent.body.localStartTime;
                    }

                    float timeSinceLastHit = lastHitTime.timeSince;
                    bodyExtraStats.CurrentMedkitProcTimeSinceLastHit = timeSinceLastHit;
                }
            }
        }
    }
}
