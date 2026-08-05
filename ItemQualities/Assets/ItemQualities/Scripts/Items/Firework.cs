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
    internal static class Firework
    {
        private static GameObject _fireworkBigProjectilePrefab;

        public static float GetFireworkScaleMultiplier(CharacterBody ownerBody)
        {
            float scaleMultiplier = 1f;
            
            if (ownerBody && ownerBody.inventory)
            {
                ItemQualityCounts firework = ownerBody.inventory.GetItemCountsEffective(ItemQualitiesContent.ItemQualityGroups.Firework);

                scaleMultiplier += (firework.UncommonCount * 1f) +
                                   (firework.RareCount * 2f) +
                                   (firework.EpicCount * 3f) +
                                   (firework.LegendaryCount * 5f);
            }

            return scaleMultiplier;
        }

        [ContentInitializer]
        private static IEnumerator LoadContent(ContentInitializerArgs args)
        {
            AsyncOperationHandle<GameObject> fireworkProjectileLoad = AddressableUtil.LoadTempAssetAsync<GameObject>(RoR2_Base_Firework.FireworkProjectile_prefab);
            AsyncOperationHandle<GameObject> fireworkGhostLoad = AddressableUtil.LoadTempAssetAsync<GameObject>(RoR2_Base_Firework.FireworkGhost_prefab);
            AsyncOperationHandle<GameObject> explodeEffectLoad = AddressableUtil.LoadAssetAsync<GameObject>(RoR2_Base_Common_VFX.OmniExplosionVFXQuick_prefab);

            ParallelProgressCoroutine prefabsLoadCoroutine = new ParallelProgressCoroutine(args.ProgressReceiver);
            prefabsLoadCoroutine.Add(fireworkProjectileLoad);
            prefabsLoadCoroutine.Add(fireworkGhostLoad);
            prefabsLoadCoroutine.Add(explodeEffectLoad);

            yield return prefabsLoadCoroutine;

            if (!fireworkProjectileLoad.AssertLoaded() || !fireworkGhostLoad.AssertLoaded())
            {
                yield break;
            }

            GameObject fireworkBigGhost = fireworkGhostLoad.Result.InstantiateClone("FireworkBigGhost", false);
            ProjectileGhostController fireworkBigGhostController = fireworkBigGhost.GetComponent<ProjectileGhostController>();
            fireworkBigGhostController.inheritScaleFromProjectile = true;

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

            if (explodeEffectLoad.Status == AsyncOperationStatus.Succeeded && explodeEffectLoad.Result)
            {
                fireworkBigImpactExplosion.impactEffect = explodeEffectLoad.Result;
            }

            fireworkBigPrefab.AddComponent<FireworkProjectileQualityController>();

            _fireworkBigProjectilePrefab = fireworkBigPrefab;
            args.ContentPack.projectilePrefabs.Add(fireworkBigPrefab);
        }

        [SystemInitializer]
        private static void Init()
        {
            IL.RoR2.FireworkLauncher.FireMissile += FireworkLauncher_FireMissile;
        }

        private static void FireworkLauncher_FireMissile(ILContext il)
        {
            ILCursor c = new ILCursor(il);

            VariableDefinition fireworkStacksVar = il.AddVariable<ItemQualityCounts>();

            c.Emit(OpCodes.Ldarg_0);
            c.EmitDelegate<Func<FireworkLauncher, ItemQualityCounts>>(getFireworkCounts);
            c.Emit(OpCodes.Stloc, fireworkStacksVar);

            static ItemQualityCounts getFireworkCounts(FireworkLauncher fireworkLauncher)
            {
                GameObject owner = fireworkLauncher ? fireworkLauncher.owner : null;
                CharacterBody ownerBody = owner ? owner.GetComponent<CharacterBody>() : null;
                Inventory ownerInventory = ownerBody ? ownerBody.inventory : null;

                return ownerInventory ? ownerInventory.GetItemCountsEffective(ItemQualitiesContent.ItemQualityGroups.Firework) : default;
            }

            VariableDefinition shouldFireLargeFireworkVar = il.AddVariable<bool>();

            c.Emit(OpCodes.Ldarg_0);
            c.Emit(OpCodes.Ldloca, fireworkStacksVar);
            c.EmitDelegate<GetShouldFireLargeFireworkDelegate>(getShouldFireLargeFirework);
            c.Emit(OpCodes.Stloc, shouldFireLargeFireworkVar);

            static bool getShouldFireLargeFirework(FireworkLauncher fireworkLauncher, in ItemQualityCounts firework)
            {
                if (firework.TotalQualityCount == 0)
                {
                    return false;
                }

                GameObject owner = fireworkLauncher ? fireworkLauncher.owner : null;
                CharacterBody ownerBody = owner ? owner.GetComponent<CharacterBody>() : null;

                float largeFireworkChance = firework.HighestQuality switch
                {
                    QualityTier.Uncommon => 15f,
                    QualityTier.Rare => 30f,
                    QualityTier.Epic => 60f,
                    QualityTier.Legendary => 100f,
                    _ => throw new NotImplementedException(),
                };

                return RollUtil.CheckRoll(largeFireworkChance, ownerBody ? ownerBody.master : null, false);
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
                c.Emit(OpCodes.Ldloca, fireworkStacksVar);
                c.EmitDelegate<GetDamageCoefficientDelegate>(getDamageCoefficient);

                static float getDamageCoefficient(float damageCoefficient, bool shouldFireLargeFirework, in ItemQualityCounts firework)
                {
                    if (shouldFireLargeFirework)
                    {
                        damageCoefficient += (firework.UncommonCount * 1f) +
                                             (firework.RareCount * 1.5f) +
                                             (firework.EpicCount * 2.5f) +
                                             (firework.LegendaryCount * 3f);
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

        private delegate bool GetShouldFireLargeFireworkDelegate(FireworkLauncher fireworkLauncher, in ItemQualityCounts firework);

        private delegate float GetDamageCoefficientDelegate(float damageCoefficient, bool shouldFireLargeFirework, in ItemQualityCounts firework);
    }
}
