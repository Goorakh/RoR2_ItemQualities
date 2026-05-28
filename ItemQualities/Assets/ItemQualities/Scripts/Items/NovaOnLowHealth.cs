using ItemQualities.Utilities.Extensions;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using RoR2;
using System;

namespace ItemQualities.Items
{
    internal static class NovaOnLowHealth
    {
        [SystemInitializer]
        private static void Init()
        {
            IL.EntityStates.VagrantNovaItem.ReadyState.FixedUpdate += ReadyState_FixedUpdate;
            IL.EntityStates.VagrantNovaItem.ReadyState.OnDamaged += ReadyState_OnDamaged;
        }

        private static void ReadyState_FixedUpdate(ILContext il)
        {
            ILCursor c = new ILCursor(il);

            int patchCount = 0;

            while (c.TryGotoNext(MoveType.Before,
                                 x => x.MatchCallOrCallvirt(typeof(HealthComponent).GetProperty(nameof(HealthComponent.isHealthLow)).GetMethod)))
            {
                c.Emit(OpCodes.Dup);
                c.Index++;

                c.EmitDelegate<Func<HealthComponent, bool, bool>>(isUnderStealthKitThreshold);

                static bool isUnderStealthKitThreshold(HealthComponent healthComponent, bool isHealthLow)
                {
                    if (healthComponent && healthComponent.TryGetComponentCached(out CharacterBodyExtraStatsTracker extraStatsTracker))
                    {
                        isHealthLow = healthComponent.IsHealthBelowThreshold(extraStatsTracker.GenesisLoopActivationThreshold);
                    }

                    return isHealthLow;
                }

                patchCount++;
            }

            if (patchCount == 0)
            {
                Log.Error("Failed to find patch location");
            }
            else
            {
                Log.Debug($"Found {patchCount} patch location(s)");
            }
        }

        private static void ReadyState_OnDamaged(ILContext il)
        {
            if (!il.Method.TryFindParameter<DamageReport>(out ParameterDefinition damageReportParam))
            {
                Log.Error("Failed to find DamageReport parameter");
                return;
            }

            ILCursor c = new ILCursor(il);

            int patchCount = 0;

            while (c.TryGotoNext(MoveType.After,
                                 x => x.MatchLdarg(damageReportParam),
                                 x => x.MatchLdfld<DamageReport>(nameof(DamageReport.hitLowHealth))))
            {
                c.Emit(OpCodes.Ldarg, damageReportParam);
                c.EmitDelegate<Func<bool, DamageReport, bool>>(isUnderNovaThreshold);

                static bool isUnderNovaThreshold(bool hitLowHealth, DamageReport damageReport)
                {
                    if (damageReport?.victim && damageReport.victim.TryGetComponentCached(out CharacterBodyExtraStatsTracker extraStatsTracker))
                    {
                        hitLowHealth = damageReport.victim.IsHealthBelowThreshold(extraStatsTracker.GenesisLoopActivationThreshold);
                    }

                    return hitLowHealth;
                }

                patchCount++;
            }

            if (patchCount == 0)
            {
                Log.Error("Failed to find patch location");
            }
            else
            {
                Log.Debug($"Found {patchCount} patch location(s)");
            }
        }
    }
}
