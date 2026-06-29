using ItemQualities.Utilities.Extensions;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using RoR2;
using System;
using System.Runtime.CompilerServices;

namespace ItemQualities
{
    internal static class PickupHooks
    {
        public delegate void OnPickupEventDelegate(in PickupDef.GrantContext context);
        public static event OnPickupEventDelegate OnPickupGlobalServer;

        [SystemInitializer]
        private static void Init()
        {
            IL.RoR2.GenericPickupController.AttemptGrant += GenericPickupController_AttemptGrant;
        }

        private static void GenericPickupController_AttemptGrant(ILContext il)
        {
            if (!il.Method.TryFindParameter<CharacterBody>(out ParameterDefinition bodyParameter))
            {
                Log.Error("Failed to find body parameter");
                return;
            }

            ILCursor c = new ILCursor(il);

            /*
             *  // if (grantContext.shouldNotify)
             *  IL_0119: ldloc.s   V_5
             *  IL_011B: ldfld     bool RoR2.PickupDef/GrantContext::shouldNotify
             *  IL_0120: brfalse.s IL_012E
             */

            VariableDefinition grantContextVar = null;
            if (!c.TryGotoNext(MoveType.AfterLabel,
                               x => x.MatchLdloc<PickupDef.GrantContext>(il, out grantContextVar),
                               x => x.MatchLdfld<PickupDef.GrantContext>(nameof(PickupDef.GrantContext.shouldNotify)),
                               x => x.MatchBrfalse(out _)))
            {
                Log.Error("Failed to find patch location");
                return;
            }

            c.Emit(OpCodes.Ldloca, grantContextVar);
            c.EmitDelegate<OnPickupEventDelegate>(invokeOnPickupEvent);

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            static void invokeOnPickupEvent(in PickupDef.GrantContext context)
            {
                OnPickupGlobalServer?.Invoke(context);
            }
        }
    }
}
