using ItemQualities.Utilities.Extensions;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using RoR2;
using System.Runtime.CompilerServices;

namespace ItemQualities
{
    internal static class BulletAttackExplicitTracerOriginPatch
    {
        // Magic number, negative so it cannot collide with any actual muzzle index
        public const int UseExplicitTracerOriginMuzzleIndex = -0xDEAD;

        [InitDuringStartupPhase(GameInitPhase.PostProgressBar)]
        private static void Init()
        {
            IL.RoR2.BulletAttack.FireSingle += BulletAttack_FireSingle;
        }

        private static void BulletAttack_FireSingle(ILContext il)
        {
            ILCursor c = new ILCursor(il);

            /*  // BulletAttack.pooledEffectData.SetChildLocatorTransformReference(this.weapon, args.muzzleIndex);
             *  IL_00B7: ldsfld    class RoR2.EffectData RoR2.BulletAttack::pooledEffectData
             *  IL_00BC: ldarg.0
             *  IL_00BD: ldfld     class [UnityEngine.CoreModule]UnityEngine.GameObject RoR2.BulletAttack::weapon
             *  IL_00C2: ldarg.1
             *  IL_00C3: ldfld     int32 RoR2.BulletAttack/FireSingleArgs::muzzleIndex
             *  IL_00C8: callvirt  instance void RoR2.EffectData::SetChildLocatorTransformReference(class [UnityEngine.CoreModule]UnityEngine.GameObject, int32)
             */

            ParameterDefinition fireArgsParameter = null;
            Instruction skipSetEffectMuzzleReferenceInstruction = null;
            if (!c.TryGotoNext(MoveType.Before,
                               x => x.MatchLdsfld<BulletAttack>(nameof(BulletAttack.pooledEffectData)),
                               x => x.MatchLdarg(0),
                               x => x.MatchLdfld<BulletAttack>(nameof(BulletAttack.weapon)),
                               x => x.MatchLdarg<BulletAttack.FireSingleArgs>(il, out fireArgsParameter),
                               x => x.MatchLdfld<BulletAttack.FireSingleArgs>(nameof(BulletAttack.FireSingleArgs.muzzleIndex)),
                               x => x.MatchCallOrCallvirt<EffectData>(nameof(EffectData.SetChildLocatorTransformReference)),
                               x => x.MatchAny(out skipSetEffectMuzzleReferenceInstruction)))
            {
                Log.Error("Failed to find patch location");
                return;
            }

            ILLabel skipSetEffectMuzzleReferenceLabel = c.DefineLabel();

            c.Emit(OpCodes.Ldarga, fireArgsParameter);
            c.EmitDelegate<ShouldAssignEffectMuzzleDelegate>(shouldAssignEffectMuzzle);
            c.Emit(OpCodes.Brfalse, skipSetEffectMuzzleReferenceLabel);

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            static bool shouldAssignEffectMuzzle(in BulletAttack.FireSingleArgs fireArgs)
            {
                return fireArgs.muzzleIndex != UseExplicitTracerOriginMuzzleIndex;
            }

            c.Goto(skipSetEffectMuzzleReferenceInstruction, MoveType.Before);
            c.MarkLabel(skipSetEffectMuzzleReferenceLabel);
        }

        private delegate bool ShouldAssignEffectMuzzleDelegate(in BulletAttack.FireSingleArgs fireArgs);
    }
}
