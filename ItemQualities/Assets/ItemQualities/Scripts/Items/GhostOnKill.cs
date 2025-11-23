using ItemQualities.Utilities;
using ItemQualities.Utilities.Extensions;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using RoR2;
using System;
using UnityEngine;

namespace ItemQualities.Items
{
    static class GhostOnKill
    {
        [SystemInitializer]
        static void Init()
        {
            IL.RoR2.GlobalEventManager.OnCharacterDeath += GlobalEventManager_OnCharacterDeath;

            IL.RoR2.Util.TryToCreateGhost += Util_TryToCreateGhost;
        }

        static void GlobalEventManager_OnCharacterDeath(ILContext il)
        {
            if (!il.Method.TryFindParameter<DamageReport>(out ParameterDefinition damageReportParameter))
            {
                Log.Error("Failed to find DamageReport parameter");
                return;
            }

            ILCursor c = new ILCursor(il);

            if (!c.TryFindNext(out ILCursor[] foundCursors,
                               x => x.MatchLdsfld(typeof(RoR2Content.Items), nameof(RoR2Content.Items.GhostOnKill)),
                               x => x.MatchLdcR4(7f),
                               x => ItemHooks.MatchCallLocalCheckRoll(x),
                               x => x.MatchBrfalse(out _),
                               x => x.MatchCallOrCallvirt(typeof(Util), nameof(Util.TryToCreateGhost)),
                               x => x.MatchPop()))
            {
                Log.Error("Failed to find patch location");
                return;
            }

            c.Goto(foundCursors[1].Next, MoveType.After); // ldc.r4 7

            VariableDefinition ghostSpawnChanceVar = il.AddVariable<float>();

            c.Emit(OpCodes.Ldarg, damageReportParameter);
            c.EmitDelegate<Func<float, DamageReport, float>>(getGhostSpawnChance);
            c.Emit(OpCodes.Dup);
            c.Emit(OpCodes.Stloc, ghostSpawnChanceVar);

            static float getGhostSpawnChance(float spawnChance, DamageReport damageReport)
            {
                Inventory attackerInventory = damageReport?.attackerBody ? damageReport.attackerBody.inventory : null;

                ItemQualityCounts ghostOnKill = ItemQualitiesContent.ItemQualityGroups.GhostOnKill.GetItemCountsEffective(attackerInventory);
                if (ghostOnKill.TotalQualityCount > 0)
                {
                    spawnChance += (5f * ghostOnKill.UncommonCount) +
                                   (10f * ghostOnKill.RareCount) +
                                   (30f * ghostOnKill.EpicCount) +
                                   (40f * ghostOnKill.LegendaryCount);
                }

                return spawnChance;
            }

            c.Goto(foundCursors[3].Next, MoveType.After); // brfalse

            VariableDefinition ghostSpawnCountVar = il.AddVariable<int>();

            c.Emit(OpCodes.Ldloc, ghostSpawnChanceVar);
            c.Emit(OpCodes.Ldarg, damageReportParameter);
            c.EmitDelegate<Func<float, DamageReport, int>>(getGhostSpawnCount);
            c.Emit(OpCodes.Stloc, ghostSpawnCountVar);

            static int getGhostSpawnCount(float spawnChance, DamageReport damageReport)
            {
                // We've already passed a roll check for a ghost, so add 1 guaranteed
                int spawnCount = 1;

                if (spawnChance > 100f)
                {
                    spawnCount += RollUtil.GetOverflowRoll(spawnChance - 100f, damageReport?.attackerMaster);
                }

                return spawnCount;
            }

            ILLabel spawnLoopCheckLabel = c.DefineLabel();

            c.Emit(OpCodes.Br, spawnLoopCheckLabel);

            ILLabel spawnGhostLabel = c.MarkLabel();

            c.Goto(foundCursors[5].Next, MoveType.After); // pop

            c.MarkLabel(spawnLoopCheckLabel);

            c.Emit(OpCodes.Ldloca, ghostSpawnCountVar);
            c.EmitDelegate<ShouldSpawnAnotherGhostDelegate>(shouldSpawnAnotherGhost);
            c.Emit(OpCodes.Brtrue, spawnGhostLabel);

            static bool shouldSpawnAnotherGhost(ref int ghostSpawnCount)
            {
                return ghostSpawnCount-- > 0;
            }
        }

        delegate bool ShouldSpawnAnotherGhostDelegate(ref int ghostSpawnCount);

        static void Util_TryToCreateGhost(ILContext il)
        {
            ILCursor c = new ILCursor(il);

            if (!c.TryGotoNext(MoveType.Before,
                               x => x.MatchStfld<MasterSummon>(nameof(MasterSummon.position))))
            {
                Log.Error("Failed to find patch location");
                return;
            }

            c.EmitDelegate<Func<Vector3, Vector3>>(getSpawnPosition);

            static Vector3 getSpawnPosition(Vector3 position)
            {
                Vector3 positionOffset = UnityEngine.Random.insideUnitSphere * 0.5f;
                positionOffset.y = Mathf.Abs(positionOffset.y) * 0.2f;
                return position + positionOffset;
            }
        }
    }
}
