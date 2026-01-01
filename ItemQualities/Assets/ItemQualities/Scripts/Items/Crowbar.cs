using ItemQualities.Utilities.Extensions;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using RoR2;
using System;

namespace ItemQualities.Items
{
    static class Crowbar
    {
        [SystemInitializer]
        static void Init()
        {
            IL.RoR2.HealthComponent.TakeDamageProcess += HealthComponent_TakeDamageProcess;
        }

        static void HealthComponent_TakeDamageProcess(ILContext il)
        {
            ILCursor c = new ILCursor(il);

            if (!il.Method.TryFindParameter<DamageInfo>(out ParameterDefinition damageInfoParameter))
            {
                Log.Error("Failed to find DamageInfo parameter");
                return;
            }

            if (!c.TryFindNext(out ILCursor[] foundCursors,
                               x => x.MatchLdsfld(typeof(RoR2Content.Items), nameof(RoR2Content.Items.Crowbar)),
                               x => x.MatchCallOrCallvirt<Inventory>(nameof(Inventory.GetItemCountEffective))))
            {
                Log.Error("Failed to find crowbar item check location");
                return;
            }

            c.Goto(foundCursors[1].Next, MoveType.After);

            if (!c.TryGotoPrev(MoveType.After,
                               x => x.MatchLdcR4(0.9f)))
            {
                Log.Error("Failed to find patch location");
            }

            c.Emit(OpCodes.Ldarg, damageInfoParameter);
            c.EmitDelegate<Func<float, DamageInfo, float>>(getCrowbarThreshold);

            static float getCrowbarThreshold(float origCrowbarThreshold, DamageInfo damageInfo)
            {
                if (damageInfo != null && damageInfo.attacker && damageInfo.attacker.TryGetComponentCached(out CharacterBodyExtraStatsTracker extraStatsTracker))
                    return extraStatsTracker.CrowbarMinHealthFraction;

                return origCrowbarThreshold;
            }
        }
    }
}
