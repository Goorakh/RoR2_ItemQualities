using HG.Coroutines;
using ItemQualities.ContentManagement;
using ItemQualities.Utilities;
using ItemQualities.Utilities.Extensions;
using Mono.Cecil;
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
    static class Missile
    {
        static GameObject _missileBigProjectilePrefab;

        [ContentInitializer]
        static IEnumerator LoadContent(ContentIntializerArgs args)
        {
            AsyncOperationHandle<GameObject> missileProjectileLoad = AddressableUtil.LoadTempAssetAsync<GameObject>(RoR2_Base_Common.MissileProjectile_prefab);
            AsyncOperationHandle<GameObject> missileGhostLoad = AddressableUtil.LoadTempAssetAsync<GameObject>(RoR2_Base_Common.MissileGhost_prefab);
            AsyncOperationHandle<GameObject> explodeEffectLoad = AddressableUtil.LoadAssetAsync<GameObject>(RoR2_Base_Common_VFX.OmniExplosionVFXQuick_prefab);

            ParallelProgressCoroutine prefabsLoadCoroutine = new ParallelProgressCoroutine(args.ProgressReceiver);
            prefabsLoadCoroutine.Add(missileProjectileLoad);
            prefabsLoadCoroutine.Add(missileGhostLoad);
            prefabsLoadCoroutine.Add(explodeEffectLoad);

            yield return prefabsLoadCoroutine;

            if (missileProjectileLoad.Status != AsyncOperationStatus.Succeeded || !missileProjectileLoad.Result)
            {
                Log.Error($"Failed to load missile projectile prefab: {missileProjectileLoad.OperationException}");
                yield break;
            }

            if (missileGhostLoad.Status != AsyncOperationStatus.Succeeded || !missileGhostLoad.Result)
            {
                Log.Error($"Failed to load missile projectile ghost prefab: {missileGhostLoad.OperationException}");
                yield break;
            }

            GameObject missileBigGhost = missileGhostLoad.Result.InstantiateClone("MissileBigGhost", false);
            Transform missileBigGhostModelRoot = missileBigGhost.transform.Find("missile VFX");
            if (missileBigGhostModelRoot)
            {
                missileBigGhostModelRoot.localScale *= 3f;
            }
            else
            {
                Log.Warning("Failed to find missile model root");
            }

            GameObject missileBigPrefab = missileProjectileLoad.Result.InstantiateClone("MissileBigProjectile");

            ProjectileController missileBigProjectileController = missileBigPrefab.GetComponent<ProjectileController>();
            missileBigProjectileController.ghostPrefab = missileBigGhost;

            if (explodeEffectLoad.Status == AsyncOperationStatus.Succeeded && explodeEffectLoad.Result)
            {
                if (missileBigPrefab.TryGetComponent(out ProjectileSingleTargetImpact missileBigSingleTargetImpact))
                {
                    missileBigSingleTargetImpact.impactEffect = explodeEffectLoad.Result;
                }
            }

            _missileBigProjectilePrefab = missileBigPrefab;
            args.ContentPack.projectilePrefabs.Add(missileBigPrefab);
        }

        [SystemInitializer]
        static void Init()
        {
            IL.RoR2.GlobalEventManager.ProcessHitEnemy += GlobalEventManager_ProcessHitEnemy;
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
                               x => x.MatchLdsfld(typeof(RoR2Content.Items), nameof(RoR2Content.Items.Missile)),
                               x => ItemHooks.MatchCallLocalCheckRoll(x),
                               x => x.MatchBrfalse(out _),
                               x => x.MatchCallOrCallvirt(typeof(Util), nameof(Util.OnHitProcDamage)),
                               x => x.MatchLdsfld(typeof(GlobalEventManager.CommonAssets), nameof(GlobalEventManager.CommonAssets.missilePrefab))))
            {
                Log.Error("Failed to find patch location");
                return;
            }

            c.Goto(foundCursors[2].Next, MoveType.After); // brfalse

            c.Emit(OpCodes.Ldarg, damageInfoParameter);
            c.EmitDelegate<Func<DamageInfo, bool>>(checkShouldFireBigMissile);

            VariableDefinition shouldFireBigMissileVar = il.AddVariable<bool>();
            c.Emit(OpCodes.Stloc, shouldFireBigMissileVar);

            static bool checkShouldFireBigMissile(DamageInfo damageInfo)
            {
                GameObject attacker = damageInfo?.attacker;
                CharacterBody attackerBody = attacker ? attacker.GetComponent<CharacterBody>() : null;
                CharacterMaster attackerMaster = attackerBody ? attackerBody.master : null;
                Inventory attackerInventory = attackerBody ? attackerBody.inventory : null;

                ItemQualityCounts missile = ItemQualitiesContent.ItemQualityGroups.Missile.GetItemCountsEffective(attackerInventory);
                if (missile.TotalQualityCount <= 0)
                    return false;

                float bigMissileChance = (10f * missile.UncommonCount) +
                                         (15f * missile.RareCount) +
                                         (25f * missile.EpicCount) +
                                         (40f * missile.LegendaryCount);

                return RollUtil.CheckRoll(bigMissileChance, attackerMaster, damageInfo.procChainMask.HasProc(ProcType.SureProc));
            }

            c.Goto(foundCursors[3].Next, MoveType.Before); // call Util.OnHitProcDamage

            c.Emit(OpCodes.Ldarg, damageInfoParameter);
            c.Emit(OpCodes.Ldloc, shouldFireBigMissileVar);
            c.EmitDelegate<Func<float, DamageInfo, bool, float>>(getMissileDamageCoefficient);

            static float getMissileDamageCoefficient(float damageCoefficient, DamageInfo damageInfo, bool shouldFireBigMissile)
            {
                GameObject attacker = damageInfo?.attacker;
                CharacterBody attackerBody = attacker ? attacker.GetComponent<CharacterBody>() : null;
                Inventory attackerInventory = attackerBody ? attackerBody.inventory : null;

                ItemQualityCounts missile = ItemQualitiesContent.ItemQualityGroups.Missile.GetItemCountsEffective(attackerInventory);

                if (shouldFireBigMissile)
                {
                    damageCoefficient += (1.50f * missile.UncommonCount) +
                                         (3.00f * missile.RareCount) +
                                         (6.00f * missile.EpicCount) +
                                         (8.00f * missile.LegendaryCount);
                }

                return damageCoefficient;
            }

            c.Goto(foundCursors[4].Next, MoveType.After); // ldfld GlobalEventManager.CommonAssets.missilePrefab

            c.Emit(OpCodes.Ldloc, shouldFireBigMissileVar);
            c.EmitDelegate<Func<GameObject, bool, GameObject>>(getMissileProjectilePrefab);

            static GameObject getMissileProjectilePrefab(GameObject missilePrefab, bool shouldFireBigMissile)
            {
                if (shouldFireBigMissile && _missileBigProjectilePrefab)
                {
                    missilePrefab = _missileBigProjectilePrefab;
                }

                return missilePrefab;
            }
        }
    }
}
