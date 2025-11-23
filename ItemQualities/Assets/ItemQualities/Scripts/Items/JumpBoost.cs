using EntityStates;
using ItemQualities.Utilities.Extensions;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using RoR2;
using System;
using UnityEngine;

namespace ItemQualities.Items
{
    static class JumpBoost
    {
        [SystemInitializer]
        static void Init()
        {
            IL.EntityStates.GenericCharacterMain.ProcessJump_bool += GenericCharacterMain_ProcessJump_bool;

            IL.RoR2.CharacterMotor.PreMove += ApplyAirControlModifiersPatch;
        }

        static void GenericCharacterMain_ProcessJump_bool(ILContext il)
        {
            ApplyAirControlModifiersPatch(il);

            ILCursor c = new ILCursor(il);

            int isQuailJumpVarIndex = -1;
            if (!c.TryGotoNext(x => x.MatchLdstr("Prefabs/Effects/BoostJumpEffect")) ||
                !c.TryGotoPrev(x => x.MatchLdloc(typeof(bool), il, out isQuailJumpVarIndex)))
            {
                Log.Error("Failed to find isQuailJump variable");
                return;
            }

            c.Goto(0);

            int someVarIndex = -1;
            int horizontalJumpVelocityScaleVarIndex = -1;
            if (!c.TryFindNext(out ILCursor[] foundCursors,
                               x => x.MatchLdsfld(typeof(RoR2Content.Items), nameof(RoR2Content.Items.JumpBoost)),
                               x => x.MatchStloc(isQuailJumpVarIndex)) ||
                !c.TryGotoNext(MoveType.After,
                               x => x.MatchLdloc(typeof(float), il, out someVarIndex),
                               x => x.MatchAdd(),
                               x => x.MatchLdloc(someVarIndex),
                               x => x.MatchDiv(),
                               x => x.MatchStloc(typeof(float), il, out horizontalJumpVelocityScaleVarIndex)))
            {
                Log.Error("Failed to find patch location");
                return;
            }

            // Move before stloc horizontalJumpVelocityScaleVarIndex
            c.Goto(c.Prev);

            c.Emit(OpCodes.Ldarg_0);
            c.EmitDelegate<Func<float, GenericCharacterMain, float>>(getHorizontalJumpVelocityScale);

            static float getHorizontalJumpVelocityScale(float horizontalJumpVelocityScale, GenericCharacterMain genericCharacterMain)
            {
                Inventory inventory = genericCharacterMain?.characterBody ? genericCharacterMain.characterBody.inventory : null;

                ItemQualityCounts jumpBoost = ItemQualitiesContent.ItemQualityGroups.JumpBoost.GetItemCountsEffective(inventory);
                if (jumpBoost.TotalQualityCount > 0 &&
                    genericCharacterMain.TryGetComponent(out CharacterBodyExtraStatsTracker bodyExtraStats) &&
                    bodyExtraStats.QuailJumpComboAuthority > 0)
                {
                    float velocityBoostPerJump = (0.20f * jumpBoost.UncommonCount) +
                                                 (0.40f * jumpBoost.RareCount) +
                                                 (0.70f * jumpBoost.EpicCount) +
                                                 (1.00f * jumpBoost.LegendaryCount);

                    int maxJumpCombo = 5 * jumpBoost.TotalQualityCount;

                    if (velocityBoostPerJump > 0f)
                    {
                        float velocityBoost = Mathf.Min(maxJumpCombo, bodyExtraStats.QuailJumpComboAuthority) * velocityBoostPerJump;

                        Log.Debug($"Quail velocity boost for {Util.GetBestBodyName(genericCharacterMain.gameObject)}: {velocityBoost}");

                        horizontalJumpVelocityScale += velocityBoost;
                    }
                }

                return horizontalJumpVelocityScale;
            }

            c.Goto(0);

            if (!c.TryGotoNext(MoveType.After,
                               x => x.MatchCallOrCallvirt<GenericCharacterMain>(nameof(GenericCharacterMain.ApplyJumpVelocity))))
            {
                Log.Error("Failed to find patch location");
                return;
            }

            c.Emit(OpCodes.Ldarg_0);
            c.Emit(OpCodes.Ldloc, isQuailJumpVarIndex);
            c.EmitDelegate<Action<GenericCharacterMain, bool>>(onJump);

            static void onJump(GenericCharacterMain genericCharacterMain, bool isQuailJump)
            {
                if (isQuailJump && genericCharacterMain.TryGetComponent(out CharacterBodyExtraStatsTracker bodyExtraStats))
                {
                    bodyExtraStats.OnQuailJumpAuthority();
                }
            }
        }

        static void ApplyAirControlModifiersPatch(ILContext il)
        {
            ILCursor c = new ILCursor(il);

            int patchCount = 0;

            while (c.TryGotoNext(MoveType.Before,
                                 x => x.MatchLdfld<CharacterMotor>(nameof(CharacterMotor.airControl))))
            {
                c.Emit(OpCodes.Dup);

                c.Index++;

                c.EmitDelegate<Func<CharacterMotor, float, float>>(getAirControl);

                static float getAirControl(CharacterMotor characterMotor, float airControl)
                {
                    if (characterMotor && characterMotor.TryGetComponent(out CharacterBodyExtraStatsTracker bodyExtraStats))
                    {
                        airControl += bodyExtraStats.AirControlBonus;
                    }

                    return airControl;
                }

                patchCount++;
            }
        }
    }
}
