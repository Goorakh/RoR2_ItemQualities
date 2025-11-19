using ItemQualities.Utilities.Extensions;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using RoR2;
using System;
using UnityEngine;

namespace ItemQualities.Items
{
    static class ItemDropChanceOnKill
    {
        static BasicPickupDropTable _sonorousQualityDropTable;

        [SystemInitializer]
        static void Init()
        {
            IL.RoR2.GlobalEventManager.OnCharacterDeath += GlobalEventManager_OnCharacterDeath;
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
                               x => x.MatchLdsfld(typeof(DLC2Content.Items), nameof(DLC2Content.Items.ItemDropChanceOnKill)),
                               x => x.MatchBle(out _)))
            {
                Log.Error("Failed to find patch location");
                return;
            }

            c.Goto(foundCursors[1].Next, MoveType.After); // ble

            VariableDefinition qualityDropTableVar = il.AddVariable<PickupDropTable>();

            c.Emit(OpCodes.Ldarg, damageReportParameter);
            c.EmitDelegate<Func<DamageReport, PickupDropTable>>(tryGetQualityDropTable);
            c.Emit(OpCodes.Stloc, qualityDropTableVar);

            static PickupDropTable tryGetQualityDropTable(DamageReport damageReport)
            {
                Inventory attackerInventory = damageReport?.attackerBody ? damageReport.attackerBody.inventory : null;

                ItemQualityCounts itemDropChanceOnKill = ItemQualitiesContent.ItemQualityGroups.ItemDropChanceOnKill.GetItemCountsEffective(attackerInventory);
                if (itemDropChanceOnKill.TotalQualityCount <= 0)
                    return null;

                if (GlobalEventManager.CommonAssets.dtSonorousEchoPath is not BasicPickupDropTable sonorousDropTable)
                {
                    Log.Error("GlobalEventManager.CommonAssets.dtSonorousEchoPath is not of type BasicPickupDropTable");
                    return null;
                }

                if (!_sonorousQualityDropTable)
                {
                    _sonorousQualityDropTable = ScriptableObject.Instantiate(sonorousDropTable);
                    _sonorousQualityDropTable.name = sonorousDropTable.name + "Quality";
                }
                else
                {
                    sonorousDropTable.ShallowCopy(ref _sonorousQualityDropTable);
                }

                BasicPickupDropTable overrideDropTable = _sonorousQualityDropTable;

                float tierWeightBoost = (1f * itemDropChanceOnKill.UncommonCount) +
                                        (2f * itemDropChanceOnKill.RareCount) +
                                        (4f * itemDropChanceOnKill.EpicCount) +
                                        (8f * itemDropChanceOnKill.LegendaryCount);

                if (tierWeightBoost > 0f)
                {
                    overrideDropTable.tier2Weight *= 1f + tierWeightBoost;
                    overrideDropTable.tier3Weight *= Mathf.Pow(1f + tierWeightBoost, 2f);
                }

                if (Run.instance)
                {
                    overrideDropTable.Regenerate(Run.instance);
                }

                return overrideDropTable;
            }

            int dropTablePatchCount = 0;

            while (c.TryGotoNext(MoveType.After,
                                 x => x.MatchLdsfld(typeof(GlobalEventManager.CommonAssets), nameof(GlobalEventManager.CommonAssets.dtSonorousEchoPath))))
            {
                c.Emit(OpCodes.Ldloc, qualityDropTableVar);
                c.EmitDelegate<Func<PickupDropTable, PickupDropTable, PickupDropTable>>(getDropTable);

                static PickupDropTable getDropTable(PickupDropTable baseDropTable, PickupDropTable overrideDropTable)
                {
                    return overrideDropTable ? overrideDropTable : baseDropTable;
                }

                dropTablePatchCount++;
            }

            if (dropTablePatchCount == 0)
            {
                Log.Error("Failed to find drop table patch location");
            }
            else
            {
                Log.Debug($"Found {dropTablePatchCount} drop table patch location(s)");
            }
        }
    }
}
