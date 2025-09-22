using ItemQualities.Utilities.Extensions;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using RoR2;
using System;

namespace ItemQualities.Items
{
    static class PersonalShield
    {
        [SystemInitializer]
        static void Init()
        {
            IL.RoR2.HealthComponent.ServerFixedUpdate += HealthComponent_ServerFixedUpdate;

            IL.RoR2.CharacterBody.OnOutOfDangerChanged += CharacterBody_OnOutOfDangerChanged;
        }

        static void HealthComponent_ServerFixedUpdate(ILContext il)
        {
            ILCursor c = new ILCursor(il);

            if (!c.TryGotoNext(MoveType.After,
                               x => x.MatchCallOrCallvirt(typeof(CharacterBody).GetProperty(nameof(CharacterBody.outOfDanger)).GetMethod)))
            {
                Log.Error("Failed to find patch location");
                return;
            }

            c.Emit(OpCodes.Ldarg_0);
            c.EmitDelegate<Func<bool, HealthComponent, bool>>(getShieldOutOfDanger);
            static bool getShieldOutOfDanger(bool outOfDanger, HealthComponent healthComponent)
            {
                if (healthComponent && healthComponent.TryGetComponent(out CharacterBodyExtraStatsTracker extraStatsTracker))
                    return extraStatsTracker.ShieldOutOfDanger;

                return outOfDanger;
            }
        }

        static void CharacterBody_OnOutOfDangerChanged(ILContext il)
        {
            ILCursor c = new ILCursor(il);

            if (!c.TryFindNext(out ILCursor[] foundCursors,
                               x => x.MatchLdstr("Play_item_proc_personal_shield_recharge"),
                               x => x.MatchCallOrCallvirt(typeof(Util), nameof(Util.PlaySound))))
            {
                Log.Error("Failed to find patch location");
                return;
            }

            c.Goto(foundCursors[1].Next, MoveType.Before);
            c.EmitSkipMethodCall(c => c.Emit(OpCodes.Ldc_I4_0));
        }
    }
}
