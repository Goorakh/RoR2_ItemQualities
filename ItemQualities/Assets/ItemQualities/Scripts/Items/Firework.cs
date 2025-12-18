using HG.Coroutines;
using ItemQualities.ContentManagement;
using ItemQualities.Utilities;
using ItemQualities.Utilities.Extensions;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using R2API;
using RoR2;
using RoR2.Projectile;
using RoR2BepInExPack.GameAssetPathsBetter;
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace ItemQualities.Items
{
    static class Firework
    {
        static GameObject _fireworkBigProjectilePrefab;

        [ContentInitializer]
        static IEnumerator LoadContent(ContentIntializerArgs args)
        {
            AsyncOperationHandle<GameObject> fireworkProjectileLoad = AddressableUtil.LoadTempAssetAsync<GameObject>(RoR2_Base_Firework.FireworkProjectile_prefab);
            AsyncOperationHandle<GameObject> fireworkGhostLoad = AddressableUtil.LoadTempAssetAsync<GameObject>(RoR2_Base_Firework.FireworkGhost_prefab);
            AsyncOperationHandle<GameObject> explodeEffectLoad = AddressableUtil.LoadAssetAsync<GameObject>(RoR2_Base_Common_VFX.OmniExplosionVFXQuick_prefab);

            ParallelProgressCoroutine prefabsLoadCoroutine = new ParallelProgressCoroutine(args.ProgressReceiver);
            prefabsLoadCoroutine.Add(fireworkProjectileLoad);
            prefabsLoadCoroutine.Add(fireworkGhostLoad);
            prefabsLoadCoroutine.Add(explodeEffectLoad);

            yield return prefabsLoadCoroutine;

            if (fireworkProjectileLoad.Status != AsyncOperationStatus.Succeeded || !fireworkProjectileLoad.Result)
            {
                Log.Error($"Failed to load firework projectile prefab: {fireworkProjectileLoad.OperationException}");
                yield break;
            }

            if (fireworkGhostLoad.Status != AsyncOperationStatus.Succeeded || !fireworkGhostLoad.Result)
            {
                Log.Error($"Failed to load firework projectile ghost prefab: {fireworkGhostLoad.OperationException}");
                yield break;
            }

            GameObject fireworkBigGhost = fireworkGhostLoad.Result.InstantiateClone("FireworkBigGhost", false);
            Transform fireworkBigGhostModelRoot = fireworkBigGhost.transform.Find("mdlFireworkProjectile");
            if (fireworkBigGhostModelRoot)
            {
                fireworkBigGhostModelRoot.localScale *= 3f;
            }
            else
            {
                Log.Warning("Failed to find firework model root");
            }

            GameObject fireworkBigPrefab = fireworkProjectileLoad.Result.InstantiateClone("FireworkBigProjectile");

            ProjectileController missileBigProjectileController = fireworkBigPrefab.GetComponent<ProjectileController>();
            missileBigProjectileController.ghostPrefab = fireworkBigGhost;

            MissileController fireworkBigMissileController = fireworkBigPrefab.GetComponent<MissileController>();
            fireworkBigMissileController.giveupTimer = 20f;
            fireworkBigMissileController.deathTimer = 30f;
            fireworkBigMissileController.maxSeekDistance = 150f;

            QuaternionPID fireworkBigQuaternionPID = fireworkBigPrefab.GetComponent<QuaternionPID>();
            fireworkBigQuaternionPID.PID = new Vector3(10f, 0.3f, 0f);

            ProjectileImpactExplosion fireworkBigImpactExplosion = fireworkBigPrefab.GetComponent<ProjectileImpactExplosion>();
            fireworkBigImpactExplosion.blastRadius = 7.5f;

            if (explodeEffectLoad.Status == AsyncOperationStatus.Succeeded && explodeEffectLoad.Result)
            {
                fireworkBigImpactExplosion.impactEffect = explodeEffectLoad.Result;
            }

            _fireworkBigProjectilePrefab = fireworkBigPrefab;
            args.ContentPack.projectilePrefabs.Add(fireworkBigPrefab);
        }

        [SystemInitializer]
        static void Init()
        {
            IL.RoR2.FireworkLauncher.FireMissile += FireworkLauncher_FireMissile;
        }

        static void FireworkLauncher_FireMissile(ILContext il)
        {
            ILCursor c = new ILCursor(il);

            VariableDefinition shouldFireLargeFireworkVar = il.AddVariable<bool>();

            c.Emit(OpCodes.Ldarg_0);
            c.EmitDelegate<Func<FireworkLauncher, bool>>(getShouldFireLargeFirework);
            c.Emit(OpCodes.Stloc, shouldFireLargeFireworkVar);

            static bool getShouldFireLargeFirework(FireworkLauncher fireworkLauncher)
            {
                GameObject owner = fireworkLauncher ? fireworkLauncher.owner : null;
                CharacterBody ownerBody = owner ? owner.GetComponent<CharacterBody>() : null;
                Inventory ownerInventory = ownerBody ? ownerBody.inventory : null;

                float largeFireworkChance = 0f;
                if (ownerInventory)
                {
                    ItemQualityCounts firework = ItemQualitiesContent.ItemQualityGroups.Firework.GetItemCountsEffective(ownerInventory);

                    largeFireworkChance = (10f * firework.UncommonCount) +
                                          (20f * firework.RareCount) +
                                          (40f * firework.EpicCount) +
                                          (60f * firework.LegendaryCount);
                }

                return Util.CheckRoll(largeFireworkChance, ownerBody ? ownerBody.master : null);
            }

            int patchCount = 0;

            while (c.TryGotoNext(MoveType.After,
                                 x => x.MatchLdfld<FireworkLauncher>(nameof(FireworkLauncher.projectilePrefab))))
            {
                c.Emit(OpCodes.Ldloc, shouldFireLargeFireworkVar);
                c.EmitDelegate<Func<GameObject, bool, GameObject>>(getProjectilePrefab);

                static GameObject getProjectilePrefab(GameObject projectilePrefab, bool shouldFireLargeFirework)
                {
                    if (shouldFireLargeFirework && _fireworkBigProjectilePrefab)
                    {
                        projectilePrefab = _fireworkBigProjectilePrefab;
                    }

                    return projectilePrefab;
                }

                patchCount++;
            }

            if (patchCount == 0)
            {
                Log.Error("Failed to find projectile prefab patch location");
            }
            else
            {
                Log.Debug($"Found {patchCount} projectile prefab patch location(s)");
            }

            patchCount = 0;

            c.Index = 0;

            while (c.TryGotoNext(MoveType.After,
                                 x => x.MatchLdfld<FireworkLauncher>(nameof(FireworkLauncher.damageCoefficient))))
            {
                c.Emit(OpCodes.Ldloc, shouldFireLargeFireworkVar);
                c.EmitDelegate<Func<float, bool, float>>(getDamageCoefficient);

                static float getDamageCoefficient(float damageCoefficient, bool shouldFireLargeFirework)
                {
                    if (shouldFireLargeFirework)
                    {
                        damageCoefficient = 5f;
                    }

                    return damageCoefficient;
                }

                patchCount++;
            }

            if (patchCount == 0)
            {
                Log.Error("Failed to find damage coefficient patch location");
            }
            else
            {
                Log.Debug($"Found {patchCount} damage coefficient patch location(s)");
            }
        }
    }
}
