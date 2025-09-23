using ItemQualities.Utilities.Extensions;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.Utils;
using RoR2;
using System;
using System.Collections.Generic;

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
                Inventory inventory = body ? body.inventory : null;

                if (inventory)
                {
                    ItemQualityCounts medkit = ItemQualitiesContent.ItemQualityGroups.Medkit.GetItemCounts(inventory);

                    float doubleHealChance = Util.ConvertAmplificationPercentageIntoReductionPercentage(amplificationPercentage:
                        (10f * medkit.UncommonCount) +
                        (20f * medkit.RareCount) +
                        (50f * medkit.EpicCount) +
                        (75f * medkit.LegendaryCount));

                    if (Util.CheckRoll(doubleHealChance, body ? body.master : null))
                    {
                        healAmount *= 2f;
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
                               x => x.MatchLdcR4(2f)))
            {
                Log.Error("Failed to find patch location");
                return;
            }

            c.Goto(foundCursors[2].Next, MoveType.After);

            c.Emit(OpCodes.Ldarg_0);
            c.EmitDelegate<Func<float, HealthComponent, float>>(getHealDelay);

            static float getHealDelay(float healDelay, HealthComponent healthComponent)
            {
                CharacterBody body = healthComponent ? healthComponent.body : null;
                Inventory inventory = body ? body.inventory : null;

                if (inventory)
                {
                    ItemQualityCounts medkit = ItemQualitiesContent.ItemQualityGroups.Medkit.GetItemCounts(inventory);

                    float healDelayReduction = Util.ConvertAmplificationPercentageIntoReductionNormalized(amplificationNormal:
                        (0.15f * medkit.UncommonCount) +
                        (0.25f * medkit.RareCount) +
                        (0.40f * medkit.EpicCount) +
                        (0.60f * medkit.LegendaryCount));

                    Log.Debug($"Reduced heal delay: {healDelay} -> {healDelay * (1f - healDelayReduction)}");

                    healDelay *= (1f - healDelayReduction);
                }

                return healDelay;
            }
        }
    }
}
