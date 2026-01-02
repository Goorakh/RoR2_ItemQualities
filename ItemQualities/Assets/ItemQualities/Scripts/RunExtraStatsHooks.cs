using Mono.Cecil.Cil;
using MonoMod.Cil;
using RoR2;
using System;
using UnityEngine;

namespace ItemQualities
{
    static class RunExtraStatsHooks
    {
        [SystemInitializer]
        static void Init()
        {
            IL.RoR2.Run.RecalculateDifficultyCoefficentInternal += Run_RecalculateDifficultyCoefficentInternal;
            IL.RoR2.InfiniteTowerRun.RecalculateDifficultyCoefficentInternal += Run_RecalculateDifficultyCoefficentInternal;
        }

        static void Run_RecalculateDifficultyCoefficentInternal(ILContext il)
        {
            ILCursor c = new ILCursor(il);

            if (!c.TryGotoNext(MoveType.Before,
                               x => x.MatchCallOrCallvirt<Run>("set_" + nameof(Run.ambientLevel))))
            {
                Log.Error("Failed to find patch location");
                return;
            }

            c.Emit(OpCodes.Ldarg_0);
            c.EmitDelegate<Func<float, Run, float>>(getAmbientLevel);

            static float getAmbientLevel(float ambientLevel, Run run)
            {
                if (RunExtraStatsTracker.Instance)
                {
                    ambientLevel = Mathf.Max(1f, ambientLevel - RunExtraStatsTracker.Instance.AmbientLevelPenalty);
                }

                return ambientLevel;
            }
        }
    }
}
