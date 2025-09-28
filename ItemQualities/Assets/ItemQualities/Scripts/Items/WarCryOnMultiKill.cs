using Mono.Cecil.Cil;
using MonoMod.Cil;
using RoR2;
using System;

namespace ItemQualities.Items
{
    static class WarCryOnMultiKill
    {
        [SystemInitializer]
        static void Init()
        {
            IL.RoR2.CharacterBody.AddMultiKill += CharacterBody_AddMultiKill;
        }

        static void CharacterBody_AddMultiKill(ILContext il)
        {
            ILCursor c = new ILCursor(il);

            if (!c.TryFindNext(out ILCursor[] foundCursors,
                               x => x.MatchLdsfld(typeof(RoR2Content.Items), nameof(RoR2Content.Items.WarCryOnMultiKill)),
                               x => x.MatchCallOrCallvirt<CharacterBody>("get_" + nameof(CharacterBody.multiKillCount))))
            {
                Log.Error("Failed to find patch location");
                return;
            }

            c.Goto(foundCursors[1].Next, MoveType.After);

            c.Emit(OpCodes.Ldarg_0);
            c.EmitDelegate<Func<int, CharacterBody, int>>(getMultiKillCount);
            
            static int getMultiKillCount(int multiKillCount, CharacterBody body)
            {
                if (body && body.TryGetComponent(out CharacterBodyExtraStatsTracker bodyExtraStatsTracker))
                {
                    multiKillCount = bodyExtraStatsTracker.WarCryOnMultiKill_MultiKillCount;
                }

                return multiKillCount;
            }
        }
    }
}
