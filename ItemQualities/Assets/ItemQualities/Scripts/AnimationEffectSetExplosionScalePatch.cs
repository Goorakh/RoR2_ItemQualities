using ItemQualities.Items;
using ItemQualities.Utilities.Extensions;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using RoR2;
using System;
using UnityEngine;

namespace ItemQualities
{
    static class AnimationEffectSetExplosionScalePatch
    {
        const int ExplosionInfoBitOffset = 8;
        const int ExplosionInfoBitMask = 0xFF << ExplosionInfoBitOffset;

        public static void SetEncodedExplosionIndex(AnimationEvent evnt, ExplosionInfoIndex explosionInfoIndex)
        {
            evnt.intParameter = EncodeExplosionIndex(evnt.intParameter, explosionInfoIndex);
        }

        public static int EncodeExplosionIndex(int intParameter, ExplosionInfoIndex explosionInfoIndex)
        {
            if ((int)explosionInfoIndex >= byte.MaxValue)
            {
                Log.Error($"Cannot encode explosion index larger than 255 ({explosionInfoIndex})");
                return intParameter;
            }

            return (intParameter & ~ExplosionInfoBitMask) | ((((int)explosionInfoIndex + 1) << ExplosionInfoBitOffset) & ExplosionInfoBitMask);
        }

        public static ExplosionInfoIndex GetExplosionIndex(AnimationEvent evnt)
        {
            int intParameter = evnt.intParameter;
            DecodeExplosionIndex(ref intParameter, out ExplosionInfoIndex explosionInfoIndex);
            evnt.intParameter = intParameter;

            return explosionInfoIndex;
        }

        public static void DecodeExplosionIndex(ref int intParameter, out ExplosionInfoIndex explosionInfoIndex)
        {
            explosionInfoIndex = (ExplosionInfoIndex)((intParameter & ExplosionInfoBitMask) >> ExplosionInfoBitOffset) - 1;
            if (explosionInfoIndex < ExplosionInfoIndex.None || (int)explosionInfoIndex > ExplosionInfoCatalog.ExplosionInfoDefCount)
            {
                Log.Error($"Out of bounds explosion index encoded in intParameter, likely data overlap (param={intParameter}, explosionIndex={explosionInfoIndex})");
                explosionInfoIndex = ExplosionInfoIndex.None;
            }

            intParameter &= ~ExplosionInfoBitMask;
        }

        [SystemInitializer]
        static void Init()
        {
            IL.RoR2.AnimationEvents.CreateEffect += AnimationEvents_CreateEffect;
        }

        static void AnimationEvents_CreateEffect(ILContext il)
        {
            if (!il.Method.TryFindParameter<AnimationEvent>(out ParameterDefinition animationEventParameter))
            {
                Log.Error("Failed to find AnimationEvent parameter");
                return;
            }

            ILCursor c = new ILCursor(il);

            VariableDefinition explosionInfoIndexVar = il.AddVariable<ExplosionInfoIndex>();

            c.Emit(OpCodes.Ldarg, animationEventParameter);
            c.EmitDelegate<Func<AnimationEvent, ExplosionInfoIndex>>(GetExplosionIndex);
            c.Emit(OpCodes.Stloc, explosionInfoIndexVar);

            VariableDefinition effectDataVar = null;
            if (!c.TryGotoNext(MoveType.After,
                               x => x.MatchNewobj<EffectData>(),
                               x => x.MatchStloc(typeof(EffectData), il, out effectDataVar)))
            {
                Log.Error("Failed to find patch location");
                return;
            }

            c.Emit(OpCodes.Ldarg_0);
            c.Emit(OpCodes.Ldloc, effectDataVar);
            c.Emit(OpCodes.Ldloc, explosionInfoIndexVar);
            c.EmitDelegate<Action<AnimationEvents, EffectData, ExplosionInfoIndex>>(tryApplyExplosionScale);

            static void tryApplyExplosionScale(AnimationEvents self, EffectData effectData, ExplosionInfoIndex explosionInfoIndex)
            {
                if (explosionInfoIndex == ExplosionInfoIndex.None)
                    return;

                float baseRadius = ExplosionInfoCatalog.GetExplosionInfoDef(explosionInfoIndex).GetDefaultRange();

                GameObject bodyObject = self ? self.bodyObject : null;
                CharacterBody body = bodyObject ? bodyObject.GetComponent<CharacterBody>() : null;

                effectData.scale = body ? ExplodeOnDeath.GetExplosionRadius(baseRadius, body) : baseRadius;
            }
        }
    }
}
