using EntityStates;
using ItemQualities.Utilities.Extensions;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using RoR2;
using System;
using UnityEngine;

namespace ItemQualities.Items
{
    internal static class JumpBoost
    {
        [SystemInitializer]
        private static void Init()
        {
            IL.EntityStates.GenericCharacterMain.ProcessJump_bool += GenericCharacterMain_ProcessJump_bool;
        }

        private static void GenericCharacterMain_ProcessJump_bool(ILContext il)
        {
            ILCursor c = new ILCursor(il);

            VariableDefinition isQuailJumpVar = null;
            if (!c.TryGotoNext(x => x.MatchLdstr("Prefabs/Effects/BoostJumpEffect")) ||
                !c.TryGotoPrev(x => x.MatchLdloc(typeof(bool), il, out isQuailJumpVar)))
            {
                Log.Error("Failed to find isQuailJump variable");
                return;
            }

            c.Goto(0);

            VariableDefinition someVar = null;
            VariableDefinition horizontalJumpVelocityScaleVar = null;
            if (!c.TryFindNext(out ILCursor[] foundCursors,
                               x => x.MatchLdsfld(typeof(RoR2Content.Items), nameof(RoR2Content.Items.JumpBoost)),
                               x => x.MatchStloc(isQuailJumpVar)) ||
                !c.TryGotoNext(MoveType.After,
                               x => x.MatchLdloc(typeof(float), il, out someVar),
                               x => x.MatchAdd(),
                               x => x.MatchLdloc(someVar),
                               x => x.MatchDiv(),
                               x => x.MatchStloc(typeof(float), il, out horizontalJumpVelocityScaleVar)))
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
                if (inventory)
                {
                    ItemQualityCounts jumpBoost = inventory.GetItemCountsEffective(ItemQualitiesContent.ItemQualityGroups.JumpBoost);

                    if (jumpBoost.TotalQualityCount > 0 &&
                        genericCharacterMain.TryGetComponentCached(out CharacterBodyExtraStatsTracker bodyExtraStats) &&
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
            c.Emit(OpCodes.Ldloc, isQuailJumpVar);
            c.EmitDelegate<Action<GenericCharacterMain, bool>>(onJump);

            static void onJump(GenericCharacterMain genericCharacterMain, bool isQuailJump)
            {
                if (isQuailJump && genericCharacterMain.TryGetComponentCached(out CharacterBodyExtraStatsTracker bodyExtraStats))
                {
                    bodyExtraStats.OnQuailJumpAuthority();
                }
            }
        }
    }
}
