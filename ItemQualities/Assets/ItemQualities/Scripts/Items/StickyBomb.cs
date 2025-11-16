using ItemQualities.Utilities;
using ItemQualities.Utilities.Extensions;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using RoR2;
using RoR2.Projectile;
using RoR2BepInExPack.GameAssetPathsBetter;
using System;
using UnityEngine;

namespace ItemQualities.Items
{
    static class StickyBomb
    {
        [SystemInitializer]
        static void Init()
        {
            AddressableUtil.LoadAssetAsync<GameObject>(RoR2_Base_StickyBomb.StickyBombGhost_prefab).OnSuccess(stickyBombGhost =>
            {
                if (stickyBombGhost.TryGetComponent(out ProjectileGhostController ghostController))
                {
                    ghostController.inheritScaleFromProjectile = true;
                }
            });

            IL.RoR2.GlobalEventManager.ProcessHitEnemy += GlobalEventManager_ProcessHitEnemy;
        }

        public static float GetStickyBombScaleMultiplier(CharacterBody ownerBody)
        {
            Inventory inventory = ownerBody ? ownerBody.inventory : null;

            ItemQualityCounts stickyBomb = ItemQualitiesContent.ItemQualityGroups.StickyBomb.GetItemCounts(inventory);

            float scaleMultiplier = 1f + (0.2f * stickyBomb.UncommonCount) +
                                         (0.5f * stickyBomb.RareCount) +
                                         (1.0f * stickyBomb.EpicCount) +
                                         (2.0f * stickyBomb.LegendaryCount);

            return scaleMultiplier;
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

                ItemQualityCounts stickyBomb = ItemQualitiesContent.ItemQualityGroups.StickyBomb.GetItemCounts(attackerInventory);

                damageCoefficient += (0.5f * stickyBomb.UncommonCount) +
                                     (0.8f * stickyBomb.RareCount) +
                                     (1.2f * stickyBomb.EpicCount) +
                                     (2.0f * stickyBomb.LegendaryCount);

                return damageCoefficient;
            }
        }
    }
}
