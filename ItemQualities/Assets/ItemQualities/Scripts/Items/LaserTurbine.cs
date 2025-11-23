using EntityStates.LaserTurbine;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using RoR2;
using System;

namespace ItemQualities.Items
{
    static class LaserTurbine
    {
        [SystemInitializer]
        static void Init()
        {
            IL.RoR2.LaserTurbineController.OnOwnerKilledOtherServer += LaserTurbineController_OnOwnerKilledOtherServer;
        }

        public static float GetExplosionRadius(float baseExplosionRadius, CharacterBody attackerBody)
        {
            float explosionRadius = baseExplosionRadius;

            Inventory attackerInventory = attackerBody ? attackerBody.inventory : null;
            if (attackerInventory)
            {
                ItemQualityCounts laserTurbine = ItemQualitiesContent.ItemQualityGroups.LaserTurbine.GetItemCountsEffective(attackerInventory);
                if (laserTurbine.TotalQualityCount > 0)
                {
                    explosionRadius += (4f * laserTurbine.UncommonCount) +
                                       (7f * laserTurbine.RareCount) +
                                       (10f * laserTurbine.EpicCount) +
                                       (14f * laserTurbine.LegendaryCount);
                }
            }

            return explosionRadius;
        }

        static void LaserTurbineController_OnOwnerKilledOtherServer(ILContext il)
        {
            ILCursor c = new ILCursor(il);

            if (!c.TryFindNext(out ILCursor[] foundCursors,
                               x => x.MatchLdsfld<RechargeState>(nameof(RechargeState.killChargeDuration)),
                               x => x.MatchConvR4()))
            {
                Log.Error("Failed to find patch location");
                return;
            }

            c.Goto(foundCursors[1].Next, MoveType.After); // conv.r4

            c.Emit(OpCodes.Ldarg_0);
            c.EmitDelegate<Func<float, LaserTurbineController, float>>(getKillChargeDuration);

            static float getKillChargeDuration(float killChargeDuration, LaserTurbineController laserTurbineController)
            {
                if (laserTurbineController && laserTurbineController.cachedOwnerBody)
                {
                    ItemQualityCounts laserTurbine = ItemQualitiesContent.ItemQualityGroups.LaserTurbine.GetItemCountsEffective(laserTurbineController.cachedOwnerBody.inventory);
                    if (laserTurbine.TotalQualityCount > 0)
                    {
                        killChargeDuration += (1f * laserTurbine.UncommonCount) +
                                              (3f * laserTurbine.RareCount) +
                                              (5f * laserTurbine.EpicCount) +
                                              (7f * laserTurbine.LegendaryCount);
                    }
                }

                return killChargeDuration;
            }
        }
    }
}
