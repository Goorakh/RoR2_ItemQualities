using HG;
using ItemQualities.Utilities;
using ItemQualities.Utilities.Extensions;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using RoR2;
using RoR2BepInExPack.GameAssetPathsBetter;
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace ItemQualities.Items
{
    static class ElementalRingVoid
    {
        [SystemInitializer]
        static IEnumerator Init()
        {
            IL.RoR2.GlobalEventManager.ProcessHitEnemy += GlobalEventManager_ProcessHitEnemy;

            AsyncOperationHandle<GameObject> blackHoleProjectileLoad = AddressableUtil.LoadAssetAsync<GameObject>(RoR2_DLC1_ElementalRingVoid.ElementalRingVoidBlackHole_prefab);
            blackHoleProjectileLoad.OnSuccess(blackHoleProjectile =>
            {
                ParticleSystem[] particleSystems = blackHoleProjectile.GetComponentsInChildren<ParticleSystem>(true);
                foreach (ParticleSystem particleSystem in particleSystems)
                {
                    ParticleSystem.MainModule main = particleSystem.main;
                    main.scalingMode = ParticleSystemScalingMode.Hierarchy;
                }

                blackHoleProjectile.EnsureComponent<ElementalRingVoidBlackHoleProjectileController>();
            });

            return blackHoleProjectileLoad;
        }

        static void GlobalEventManager_ProcessHitEnemy(ILContext il)
        {
            if (!il.Method.TryFindParameter<DamageInfo>(out ParameterDefinition damageInfoParameter))
            {
                Log.Error("Failed to find DamageInfo parameter");
                return;
            }

            ILCursor c = new ILCursor(il);

            if (!c.TryFindNext(out ILCursor[] foundCursors,
                               x => x.MatchLdsfld(typeof(DLC1Content.Items), nameof(DLC1Content.Items.ElementalRingVoid)),
                               x => x.MatchLdcR4(20f),
                               x => x.MatchCallOrCallvirt(typeof(Util), nameof(Util.OnHitProcDamage))))
            {
                Log.Error("Failed to find patch location");
                return;
            }

            c.Goto(foundCursors[1].Next, MoveType.After); // ldc.r4 20

            c.Emit(OpCodes.Ldarg, damageInfoParameter);
            c.EmitDelegate<Func<float, DamageInfo, float>>(getCooldown);

            static float getCooldown(float cooldown, DamageInfo damageInfo)
            {
                CharacterBody attackerBody = damageInfo?.attacker ? damageInfo.attacker.GetComponent<CharacterBody>() : null;
                Inventory attackerInventory = attackerBody ? attackerBody.inventory : null;

                ItemQualityCounts elementalRingVoid = ItemQualitiesContent.ItemQualityGroups.ElementalRingVoid.GetItemCounts(attackerInventory);
                if (elementalRingVoid.TotalQualityCount > 0)
                {
                    switch (elementalRingVoid.HighestQuality)
                    {
                        case QualityTier.Uncommon:
                            cooldown = Mathf.Min(cooldown, 18f);
                            break;
                        case QualityTier.Rare:
                            cooldown = Mathf.Min(cooldown, 15f);
                            break;
                        case QualityTier.Epic:
                            cooldown = Mathf.Min(cooldown, 12f);
                            break;
                        case QualityTier.Legendary:
                            cooldown = Mathf.Min(cooldown, 10f);
                            break;
                        default:
                            Log.Error($"Quality tier {elementalRingVoid.HighestQuality} is not implemented");
                            break;
                    }
                }

                return cooldown;
            }

            c.Goto(foundCursors[2].Next, MoveType.Before); // call Util.OnHitProcDamage

            c.Emit(OpCodes.Ldarg, damageInfoParameter);
            c.EmitDelegate<Func<float, DamageInfo, float>>(getDamageCoefficient);

            static float getDamageCoefficient(float damageCoefficient, DamageInfo damageInfo)
            {
                CharacterBody attackerBody = damageInfo?.attacker ? damageInfo.attacker.GetComponent<CharacterBody>() : null;
                Inventory attackerInventory = attackerBody ? attackerBody.inventory : null;

                ItemQualityCounts elementalRingVoid = ItemQualitiesContent.ItemQualityGroups.ElementalRingVoid.GetItemCounts(attackerInventory);
                if (elementalRingVoid.TotalQualityCount > 0)
                {
                    damageCoefficient += (0.50f * elementalRingVoid.UncommonCount) +
                                         (0.75f * elementalRingVoid.RareCount) +
                                         (1.00f * elementalRingVoid.EpicCount) +
                                         (2.00f * elementalRingVoid.LegendaryCount);
                }

                return damageCoefficient;
            }
        }
    }
}
