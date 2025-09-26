using ItemQualities.Utilities.Extensions;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using RoR2;
using RoR2.Projectile;
using System;
using UnityEngine;

namespace ItemQualities.Items
{
    static class StickyBomb
    {
        [SystemInitializer]
        static void Init()
        {
            IL.RoR2.GlobalEventManager.ProcessHitEnemy += GlobalEventManager_ProcessHitEnemy;
        }

        public static float ModifyStickyBombFuse(float baseFuse, CharacterBody ownerBody)
        {
            float fuse = baseFuse;

            Inventory inventory = ownerBody ? ownerBody.inventory : null;

            ItemQualityCounts stickyBomb = default;
            if (inventory)
            {
                stickyBomb = ItemQualitiesContent.ItemQualityGroups.StickyBomb.GetItemCounts(inventory);
            }

            float fuseSpeedIncrease = (0.10f * stickyBomb.UncommonCount) + // 10%
                                      (0.25f * stickyBomb.RareCount) +     // 20%
                                      (0.50f * stickyBomb.EpicCount) +     // 30%
                                      (1.00f * stickyBomb.LegendaryCount); // 50%

            if (fuseSpeedIncrease > 0)
            {
                fuse /= 1f + fuseSpeedIncrease;
            }

            return fuse;
        }

        static void GlobalEventManager_ProcessHitEnemy(ILContext il)
        {
            ILCursor c = new ILCursor(il);

            if (!il.Method.TryFindParameter<DamageInfo>(out ParameterDefinition damageInfoParameter))
            {
                Log.Error("Failed to find DamageInfo parameter");
                return;
            }

            if (!c.TryFindNext(out ILCursor[] foundCursors,
                               x => x.MatchLdsfld(typeof(RoR2Content.Items), nameof(RoR2Content.Items.StickyBomb)),
                               x => x.MatchCallOrCallvirt(typeof(Util), nameof(Util.OnHitProcDamage)),
                               x => x.MatchCallOrCallvirt<ProjectileManager>(nameof(ProjectileManager.FireProjectileWithoutDamageType))))
            {
                Log.Error("Failed to find patch location");
                return;
            }

            c.Goto(foundCursors[1].Next, MoveType.Before);

            c.Emit(OpCodes.Ldarg, damageInfoParameter);
            c.EmitDelegate<Func<float, DamageInfo, float>>(getStickyBombDamageCoefficient);

            static float getStickyBombDamageCoefficient(float damageCoefficient, DamageInfo damageInfo)
            {
                GameObject attacker = damageInfo?.attacker;
                CharacterBody attackerBody = attacker ? attacker.GetComponent<CharacterBody>() : null;
                Inventory attackerInventory = attackerBody ? attackerBody.inventory : null;

                ItemQualityCounts stickyBomb = default;
                if (attackerInventory)
                {
                    stickyBomb = ItemQualitiesContent.ItemQualityGroups.StickyBomb.GetItemCounts(attackerInventory);
                }

                damageCoefficient += (0.1f * stickyBomb.UncommonCount) +
                                     (0.3f * stickyBomb.RareCount) +
                                     (0.6f * stickyBomb.EpicCount) +
                                     (1.0f * stickyBomb.LegendaryCount);

                return damageCoefficient;
            }

            c.Goto(foundCursors[2].Next, MoveType.Before);
        }
    }
}
