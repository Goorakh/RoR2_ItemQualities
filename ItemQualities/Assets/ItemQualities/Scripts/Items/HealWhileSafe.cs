using Mono.Cecil.Cil;
using MonoMod.Cil;
using RoR2;
using System;

namespace ItemQualities.Items
{
    static class HealWhileSafe
    {
        [SystemInitializer]
        static void Init()
        {
            IL.RoR2.CharacterBody.RecalculateStats += CharacterBody_RecalculateStats;
            IL.RoR2.SnailAnimator.FixedUpdate += SnailAnimator_FixedUpdate;
        }

        static void CharacterBody_RecalculateStats(ILContext il)
        {
            ILCursor c = new ILCursor(il);

            if (!ItemHooks.TryFindNextItemCountVariable(c, typeof(RoR2Content.Items), nameof(RoR2Content.Items.HealWhileSafe), out VariableDefinition healWhileSafeItemCountVar))
            {
                Log.Error("Failed to find HealWhileSafe itemCount local");
                return;
            }

            if (c.TryGotoNext(MoveType.Before,
                              x => x.MatchLdloc(healWhileSafeItemCountVar.Index)) &&
                c.TryGotoPrev(MoveType.After,
                              x => x.MatchCallOrCallvirt(typeof(CharacterBody).GetProperty(nameof(CharacterBody.outOfDanger)).GetMethod)))
            {
                c.Emit(OpCodes.Ldarg_0);
                c.EmitDelegate<Func<bool, CharacterBody, bool>>(getOutOfDangerForSlug);

                static bool getOutOfDangerForSlug(bool outOfDanger, CharacterBody body)
                {
                    if (body && body.TryGetComponent(out CharacterBodyExtraStatsTracker extraStatsTracker))
                        return extraStatsTracker.SlugActive;

                    return outOfDanger;
                }
            }
            else
            {
                Log.Error("Failed to find patch location");
            }
        }

        static void SnailAnimator_FixedUpdate(ILContext il)
        {
            ILCursor c = new ILCursor(il);

            if (!c.TryGotoNext(MoveType.After,
                               x => x.MatchCallOrCallvirt(typeof(CharacterBody).GetProperty(nameof(CharacterBody.outOfDanger)).GetMethod)))
            {
                Log.Error("Failed to find patch location");
                return;
            }

            c.Emit(OpCodes.Ldarg_0);
            c.EmitDelegate<Func<bool, SnailAnimator, bool>>(getOutOfDangerForSlug);

            static bool getOutOfDangerForSlug(bool outOfDanger, SnailAnimator snailAnimator)
            {
                if (snailAnimator &&
                    snailAnimator.characterModel &&
                    snailAnimator.characterModel.body &&
                    snailAnimator.characterModel.body.TryGetComponent(out CharacterBodyExtraStatsTracker extraStatsTracker))
                {
                    return extraStatsTracker.SlugActive;
                }

                return outOfDanger;
            }
        }
    }
}
