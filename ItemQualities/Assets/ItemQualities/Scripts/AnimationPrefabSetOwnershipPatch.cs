using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using RoR2;
using System;
using UnityEngine;

namespace ItemQualities
{
    internal static class AnimationPrefabSetOwnershipPatch
    {
        [SystemInitializer]
        private static void Init()
        {
            IL.RoR2.AnimationEvents.CreatePrefab += AnimationEvents_CreatePrefab;
        }

        private static void AnimationEvents_CreatePrefab(ILContext il)
        {
            ILCursor c = new ILCursor(il);

            int patchCount = 0;

            while (c.TryGotoNext(MoveType.After,
                                 x => x.MatchCallOrCallvirt(out MethodReference method) && method?.Name?.StartsWith("<CreatePrefab>g__DoSpawnEffect|") == true))
            {
                c.Emit(OpCodes.Dup);
                c.Emit(OpCodes.Ldarg_0);
                c.EmitDelegate<Action<Transform, AnimationEvents>>(onPrefabSpawned);

                static void onPrefabSpawned(Transform prefabTransform, AnimationEvents animationEvents)
                {
                    if (prefabTransform && prefabTransform.TryGetComponent(out LocalEffectOwnership localEffectOwnership))
                    {
                        GameObject ownerObject;
                        if (animationEvents.bodyObject)
                        {
                            ownerObject = animationEvents.bodyObject;
                        }
                        else if (animationEvents.entityLocator)
                        {
                            ownerObject = animationEvents.entityLocator.entity;
                        }
                        else
                        {
                            ownerObject = animationEvents.gameObject;
                        }

                        localEffectOwnership.OwnerObject = ownerObject;
                    }
                }

                patchCount++;
            }

            if (patchCount == 0)
            {
                Log.Error("Failed to find patch location");
            }
            else
            {
                Log.Debug($"Found {patchCount} patch location(s)");
            }
        }
    }
}
