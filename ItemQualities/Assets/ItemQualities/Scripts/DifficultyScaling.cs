using MonoMod.Cil;
using RoR2;
using System;

namespace ItemQualities
{
    internal static class DifficultyScaling
    {
        [SystemInitializer]
        private static void Init()
        {
            IL.RoR2.Run.RecalculateDifficultyCoefficentInternal += ApplyScalingValueChangesPatch;
            IL.RoR2.InfiniteTowerRun.RecalculateDifficultyCoefficentInternal += ApplyScalingValueChangesPatch;
        }

        private static void ApplyScalingValueChangesPatch(ILContext il)
        {
            ILCursor c = new ILCursor(il);

            int patchCount = 0;

            while (c.TryGotoNext(MoveType.After,
                                 x => x.MatchLdfld<DifficultyDef>(nameof(DifficultyDef.scalingValue))))
            {
                c.EmitDelegate<Func<float, float>>(getScalingValue);

                static float getScalingValue(float scalingValue)
                {
                    return scalingValue * Configs.General.DifficultyCoefficientMultiplier.Value;
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
