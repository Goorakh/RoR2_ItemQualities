using ItemQualities.Utilities;
using ItemQualities.Utilities.Extensions;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using RoR2;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;

namespace ItemQualities.Items
{
    static class FreeChest
    {
        [SystemInitializer]
        static void Init()
        {
            IL.RoR2.SceneDirector.PopulateScene += SceneDirector_PopulateScene;
        }

        static void SceneDirector_PopulateScene(ILContext il)
        {
            MethodInfo masterEnumeratorCurrentGetter = typeof(IEnumerator<CharacterMaster>).GetProperty(nameof(IEnumerator.Current))?.GetMethod;
            if (masterEnumeratorCurrentGetter == null)
            {
                Log.Error("Failed to find CharacterMaster enumerator Current getter");
                return;
            }

            ILCursor c = new ILCursor(il);

            int freeChestSpawnCountLocalIndex = -1;
            if (!c.TryFindNext(out ILCursor[] foundCursors,
                               x => x.MatchLdsfld(typeof(DLC1Content.Items), nameof(DLC1Content.Items.FreeChest)),
                               x => x.MatchStloc(typeof(int), il, out freeChestSpawnCountLocalIndex)))
            {
                Log.Error("Failed to find patch location");
                return;
            }

            c.Goto(foundCursors[0].Next, MoveType.Before);
            if (!c.TryGotoPrev(MoveType.After, x => x.MatchCallOrCallvirt(masterEnumeratorCurrentGetter)))
            {
                Log.Error("Failed to find master iterator variable");
                return;
            }

            VariableDefinition masterVar = il.AddVariable<CharacterMaster>();
            c.Emit(OpCodes.Stloc, masterVar);
            c.Emit(OpCodes.Ldloc, masterVar);

            c.Goto(foundCursors[1].Next, MoveType.After);

            c.Emit(OpCodes.Ldloc, freeChestSpawnCountLocalIndex);

            c.Emit(OpCodes.Ldloc, masterVar);
            c.EmitDelegate<Func<CharacterMaster, int>>(getExtraFreeChestSpawnCount);

            c.Emit(OpCodes.Add);
            c.Emit(OpCodes.Stloc, freeChestSpawnCountLocalIndex);

            static int getExtraFreeChestSpawnCount(CharacterMaster master)
            {
                Inventory inventory = master ? master.inventory : null;

                int extraSpawnCount = 0;

                if (inventory)
                {
                    ItemQualityCounts freeChest = inventory.GetItemCountsEffective(ItemQualitiesContent.ItemQualityGroups.FreeChest);
                    if (freeChest.TotalQualityCount > 0)
                    {
                        float extraSpawnChance = (50f * freeChest.UncommonCount) +
                                                 (75f * freeChest.RareCount) +
                                                 (100f * freeChest.EpicCount) +
                                                 (150f * freeChest.LegendaryCount);

                        extraSpawnCount += RollUtil.GetOverflowRoll(extraSpawnChance, master, false);

                        Log.Debug($"Extra spawn count from {Util.GetBestMasterName(master)}: {extraSpawnCount}");
                    }
                }

                return extraSpawnCount;
            }
        }
    }
}
