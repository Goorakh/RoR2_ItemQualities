using ItemQualities.Utilities.Extensions;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using RoR2;
using RoR2.Projectile;
using System;

namespace ItemQualities.Items
{
    static class Dagger
    {
        [SystemInitializer]
        static void Init()
        {
            IL.RoR2.GlobalEventManager.OnCharacterDeath += GlobalEventManager_OnCharacterDeath;
        }

        static void GlobalEventManager_OnCharacterDeath(ILContext il)
        {
            ILCursor c = new ILCursor(il);

            if (!il.Method.TryFindParameter<DamageReport>(out ParameterDefinition damageReportParameter))
            {
                Log.Error("Failed to find DamageReport parameter");
                return;
            }

            if (!c.TryFindNext(out ILCursor[] foundCursors,
                               x => x.MatchLdsfld(typeof(RoR2Content.Items), nameof(RoR2Content.Items.Dagger)),
                               x => x.MatchCallOrCallvirt<ProjectileManager>(nameof(ProjectileManager.FireProjectileWithoutDamageType)),
                               x => x.MatchLdcI4(3)))
            {
                Log.Error("Failed to find patch location");
                return;
            }

            c.Goto(foundCursors[2].Next, MoveType.After);

            c.Emit(OpCodes.Ldarg, damageReportParameter);
            c.EmitDelegate<Func<int, DamageReport, int>>(modifyDaggerSpawnCount);

            static int modifyDaggerSpawnCount(int daggerSpawnCount, DamageReport damageReport)
            {
                CharacterMaster attackerMaster = damageReport?.attackerMaster;
                Inventory attackerInventory = attackerMaster ? attackerMaster.inventory : null;

                if (attackerInventory)
                {
                    ItemQualityCounts itemCounts = attackerInventory.GetItemCountsEffective(ItemQualitiesContent.ItemQualityGroups.Dagger);
                    daggerSpawnCount += (1 * itemCounts.UncommonCount) +
                                        (2 * itemCounts.RareCount) +
                                        (5 * itemCounts.EpicCount) +
                                        (7 * itemCounts.LegendaryCount);
                }

                return daggerSpawnCount;
            }
        }
    }
}
