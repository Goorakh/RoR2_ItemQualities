using HG.Coroutines;
using ItemQualities.Utilities;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using RoR2;
using RoR2.Projectile;
using RoR2BepInExPack.GameAssetPathsBetter;
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace ItemQualities.Items
{
    static class PrimarySkillShuriken
    {
        [SystemInitializer]
        static IEnumerator Init()
        {
            IL.RoR2.PrimarySkillShurikenBehavior.FixedUpdate += PrimarySkillShurikenBehavior_FixedUpdate;

            AsyncOperationHandle<GameObject> shurikenLoad = AddressableUtil.LoadAssetAsync<GameObject>(RoR2_DLC1_PrimarySkillShuriken.ShurikenProjectile_prefab);
            AsyncOperationHandle<GameObject> shurikenGhostLoad = AddressableUtil.LoadAssetAsync<GameObject>(RoR2_DLC1_PrimarySkillShuriken.ShurikenGhost_prefab);

            ParallelCoroutine loadCoroutine = new ParallelCoroutine();
            loadCoroutine.Add(shurikenLoad);
            loadCoroutine.Add(shurikenGhostLoad);

            yield return loadCoroutine;

            if (shurikenLoad.Status != AsyncOperationStatus.Succeeded || !shurikenLoad.Result)
            {
                Log.Error($"Failed to load Shuriken prefab: {shurikenLoad.OperationException}");
                yield break;
            }

            if (shurikenGhostLoad.Status != AsyncOperationStatus.Succeeded || !shurikenGhostLoad.Result)
            {
                Log.Error($"Failed to load ShurikenGhost prefab: {shurikenGhostLoad.OperationException}");
                yield break;
            }

            GameObject shuriken = shurikenLoad.Result;
            shuriken.AddComponent<ShurikenProjectileController>();

            GameObject shurikenGhost = shurikenGhostLoad.Result;
            ProjectileGhostController shurikenGhostController = shurikenGhost.GetComponent<ProjectileGhostController>();
            shurikenGhostController.inheritScaleFromProjectile = true;
        }

        public static float GetTotalReloadTime(CharacterBody body)
        {
            return getTotalReloadTime(PrimarySkillShurikenBehavior.totalReloadTime, body);
        }

        static float getTotalReloadTime(float totalReloadTime, CharacterBody body)
        {
            Inventory inventory = body ? body.inventory : null;

            ItemQualityCounts primarySkillShuriken = ItemQualitiesContent.ItemQualityGroups.PrimarySkillShuriken.GetItemCountsEffective(inventory);

            if (primarySkillShuriken.TotalQualityCount > 0)
            {
                float reloadTimeReduction = (0.1f * primarySkillShuriken.UncommonCount) +
                                            (0.3f * primarySkillShuriken.RareCount) +
                                            (0.5f * primarySkillShuriken.EpicCount) +
                                            (1.0f * primarySkillShuriken.LegendaryCount);

                if (reloadTimeReduction > 0f)
                {
                    totalReloadTime /= 1f + reloadTimeReduction;
                }
            }

            return totalReloadTime;
        }

        static void PrimarySkillShurikenBehavior_FixedUpdate(ILContext il)
        {
            ILCursor c = new ILCursor(il);

            if (!c.TryGotoNext(MoveType.After,
                               x => x.MatchLdcR4(PrimarySkillShurikenBehavior.totalReloadTime)))
            {
                Log.Error("Failed to find patch location");
                return;
            }

            c.Emit(OpCodes.Ldarg_0);
            c.EmitDelegate<Func<float, PrimarySkillShurikenBehavior, float>>(getTotalShurikenReloadTime);

            static float getTotalShurikenReloadTime(float totalReloadTime, PrimarySkillShurikenBehavior shurikenBehavior)
            {
                return getTotalReloadTime(totalReloadTime, shurikenBehavior ? shurikenBehavior.body : null);
            }
        }
    }
}
