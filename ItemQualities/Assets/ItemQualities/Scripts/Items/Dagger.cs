using ItemQualities.Utilities.Extensions;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using RoR2;
using RoR2.Projectile;
using System;

namespace ItemQualities.Items
{
    internal static class Dagger
    {
        [SystemInitializer]
        private static void Init()
        {
            IL.RoR2.GlobalEventManager.OnCharacterDeath += GlobalEventManager_OnCharacterDeath;
        }

        private static void GlobalEventManager_OnCharacterDeath(ILContext il)
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
                    daggerSpawnCount += (2 * itemCounts.UncommonCount) +
                                        (5 * itemCounts.RareCount) +
                                        (8 * itemCounts.EpicCount) +
                                        (10 * itemCounts.LegendaryCount);
                }

                return daggerSpawnCount;
            }
        }
    }
}
