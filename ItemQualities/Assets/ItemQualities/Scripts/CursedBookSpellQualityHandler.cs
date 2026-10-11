using ItemQualities.Utilities;
using ItemQualities.Utilities.Extensions;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using RoR2;
using System;

namespace ItemQualities
{
    internal static class CursedBookSpellQualityHandler
    {
        [InitDuringStartupPhase(GameInitPhase.PostProgressBar)]
        private static void Init()
        {
            IL.RoR2.Items.CursedBookBodyBehavior.AttemptUpgrade += CursedBookBodyBehavior_AttemptUpgrade;
        }

        private static void CursedBookBodyBehavior_AttemptUpgrade(ILContext il)
        {
            if (!il.Method.TryFindParameter(typeof(UniquePickup), "source", out ParameterDefinition sourceParameter))
            {
                Log.PatchError(il, "Failed to find source parameter");
                return;
            }

            ILCursor c = new ILCursor(il);

            /*
             *  // result = source.WithPickupIndex(weightedSelection.Evaluate(Run.instance.treasureRng.nextNormalizedFloat));
             *  IL_012B: ldarg.2
             *  IL_012C: ldarga.s  source
             *  IL_012E: ldloc.s   V_5
             *  IL_0130: call      class RoR2.Run RoR2.Run::get_instance()
             *  IL_0135: ldfld     class [HGCSharpUtils]Xoroshiro128Plus RoR2.Run::treasureRng
             *  IL_013A: callvirt  instance float32 [HGCSharpUtils]Xoroshiro128Plus::get_nextNormalizedFloat()
             *  IL_013F: callvirt  instance !0 class WeightedSelection`1<valuetype RoR2.PickupIndex>::Evaluate(float32)
             *  IL_0144: call      instance valuetype RoR2.UniquePickup RoR2.UniquePickup::WithPickupIndex(valuetype RoR2.PickupIndex)
             *  IL_0149: stobj     RoR2.UniquePickup
             */

            if (!c.TryGotoNext(MoveType.After,
                               x => x.MatchCallOrCallvirt(CommonReflectionCache.WeightedSelection<PickupIndex>.Evaluate)))
            {
                Log.PatchError(il, "Failed to find patch location");
                return;
            }

            c.Emit(OpCodes.Ldarg, sourceParameter);
            c.EmitDelegate<Func<PickupIndex, UniquePickup, PickupIndex>>(getRolledPickupIndex);

            static PickupIndex getRolledPickupIndex(PickupIndex rolledPickupIndex, UniquePickup sourcePickup)
            {
                QualityTier sourceQualityTier = QualityCatalog.GetQualityTier(sourcePickup.pickupIndex);

                QualityTier rolledQualityTier = QualityCatalog.GetQualityTier(rolledPickupIndex);
                if (sourceQualityTier > rolledQualityTier)
                {
                    rolledQualityTier = sourceQualityTier;
                    rolledPickupIndex = QualityCatalog.GetPickupIndexOfQuality(rolledPickupIndex, rolledQualityTier);
                }

                return rolledPickupIndex;
            }
        }
    }
}
