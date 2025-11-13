using MonoMod.Cil;
using RoR2;
using System;

namespace ItemQualities
{
    static class DifficultyScaling
    {
        [SystemInitializer]
        static void Init()
        {
            IL.RoR2.Run.RecalculateDifficultyCoefficentInternal += ApplyScalingValueChangesPatch;
            IL.RoR2.InfiniteTowerRun.RecalculateDifficultyCoefficentInternal += ApplyScalingValueChangesPatch;
        }

        static void ApplyScalingValueChangesPatch(ILContext il)
        {
            ILCursor c = new ILCursor(il);

            int patchCount = 0;

            while (c.TryGotoNext(MoveType.After,
                                 x => x.MatchLdfld<DifficultyDef>(nameof(DifficultyDef.scalingValue))))
            {
                c.EmitDelegate<Func<float, float>>(getScalingValue);

                static float getScalingValue(float scalingValue)
                {
                    if (Configs.General.EnableDifficultyModifications.Value)
                    {
                        scalingValue *= 1.25f;
                    }

                    return scalingValue;
                }

                patchCount++;
            }

            if (patchCount == 0)
            {
                Log.Error($"Failed to find patch location for {il.Method.FullName}");
            }
            else
            {
                Log.Debug($"Found {patchCount} patch location(s) for {il.Method.FullName}");
            }
        }
    }
}
