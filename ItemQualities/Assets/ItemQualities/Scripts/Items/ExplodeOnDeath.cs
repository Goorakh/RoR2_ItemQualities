using EntityStates;
using HG;
using HG.Coroutines;
using ItemQualities.ContentManagement;
using ItemQualities.Utilities;
using ItemQualities.Utilities.Extensions;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;
using MonoMod.Utils;
using RoR2;
using RoR2.ContentManagement;
using RoR2.Items;
using RoR2.Orbs;
using RoR2.Projectile;
using RoR2.VoidRaidCrab;
using RoR2BepInExPack.GameAssetPaths.Version_1_39_0;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace ItemQualities.Items
{
    static class ExplodeOnDeath
    {
        static GameObject _banditSmokeBombScalingFixPrefab;

        static GameObject _lightningStrikeScalingFixPrefab;
        static GameObject _simpleLightningStrikeScalingFixPrefab;

        static GameObject _meteorWarningEffectScalingFixPrefab;
        static GameObject _meteorTravelEffectScalingFixPrefab;
        static GameObject _meteorImpactEffectScalingFixPrefab;

        static GameObject _brotherFistSlamImpactScaleFixPrefab;

        static GameObject _brotherWeaponSlamImpactScaleFixPrefab;

        static GameObject _golemLaserImpactScaleFixPrefab;

        static GameObject _halcyoniteTriLaserImpactScaleFixPrefab;

        static GameObject _impBossBlinkScaleFixPrefab;
        
        static GameObject _impBossGroundPoundSlamScaleFixPrefab;

        static GameObject _mageFlyUpBlinkScaleFixPrefab;

        // RoR2.Orbs.LightningStrikeOrb.OnArrival
        const float LightningStrikeOrbRadius = 3f;

        // RoR2.Orbs.SimpleLightningStrikeOrb.OnArrival
        const float SimpleLightningStrikeOrbRadius = 3f;

        public static float GetExplosionRadius(float radius, CharacterBody attacker)
        {
            if (attacker && attacker.inventory)
            {
                ItemQualityCounts explodeOnDeath = attacker.inventory.GetItemCountsEffective(ItemQualitiesContent.ItemQualityGroups.ExplodeOnDeath);
                if (explodeOnDeath.TotalQualityCount > 0)
                {
                    float radiusIncrease = (0.15f * explodeOnDeath.UncommonCount) +
                                           (0.25f * explodeOnDeath.RareCount) +
                                           (0.50f * explodeOnDeath.EpicCount) +
                                           (0.75f * explodeOnDeath.LegendaryCount);

                    if (radiusIncrease > 0)
                    {
                        radius *= 1f + radiusIncrease;
                    }
                }
            }

            return radius;
        }

        static bool isScaledExplosion(float baseRadius, CharacterBody attacker)
        {
            if (baseRadius <= 0f)
                return false;

            float radius = GetExplosionRadius(baseRadius, attacker);
            return Mathf.Abs((radius / baseRadius) - 1f) > Mathf.Epsilon;
        }

        static void spawnEffectAtMuzzle(GameObject effectPrefab, EffectData effectData, GameObject entityObject, string muzzleName, bool transmit)
        {
            if (entityObject &&
                entityObject.TryGetComponent(out ModelLocator modelLocator) &&
                modelLocator.modelChildLocator)
            {
                int muzzleTransformIndex = modelLocator.modelChildLocator.FindChildIndex(muzzleName);
                Transform muzzleTransform = muzzleTransformIndex < 0 ? null : modelLocator.modelChildLocator.FindChild(muzzleTransformIndex);
                if (muzzleTransform)
                {
                    effectData.origin = muzzleTransform.position;
                    effectData.SetChildLocatorTransformReference(entityObject, muzzleTransformIndex);

                    EffectManager.SpawnEffect(effectPrefab, effectData, transmit);
                }
            }
        }

        [ContentInitializer]
        static IEnumerator LoadContent(ContentIntializerArgs args)
        {
            ParallelProgressCoroutine coroutine = new ParallelProgressCoroutine(args.ProgressReceiver);

            static IEnumerator banditSmokeBombScaleFixAsync(IProgress<float> progressReceiver)
            {
                AsyncOperationHandle<GameObject> smokeBombPrefabLoad = AddressableUtil.LoadTempAssetAsync<GameObject>(RoR2_Base_Bandit2.Bandit2SmokeBomb_prefab);
                AsyncOperationHandle<EntityStateConfiguration> stealthModeConfigurationLoad = AddressableUtil.LoadTempAssetAsync<EntityStateConfiguration>(RoR2_Base_Bandit2.EntityStates_Bandit2_StealthMode_asset);

                ParallelProgressCoroutine loadCoroutine = new ParallelProgressCoroutine(progressReceiver);
                loadCoroutine.Add(smokeBombPrefabLoad);
                loadCoroutine.Add(stealthModeConfigurationLoad);

                yield return loadCoroutine;

                if (!smokeBombPrefabLoad.AssertLoaded("Bandit2SmokeBomb") ||
                    !stealthModeConfigurationLoad.AssertLoaded("EntityStates.Bandit2.StealthMode"))
                {
                    yield break;
                }
                
                if (stealthModeConfigurationLoad.Result.TryGetFieldValue(nameof(EntityStates.Bandit2.StealthMode.blastAttackRadius), out float radiusValue))
                {
                    EffectDef smokebombFixedScaling = EffectScalingFixer.GetOrCreateFixedScalingCopy(smokeBombPrefabLoad.Result, radiusValue);
                    if (smokebombFixedScaling != null)
                    {
                        _banditSmokeBombScalingFixPrefab = smokebombFixedScaling.prefab;
                    }
                }
            }

            ReadableProgress<float> banditSmokeBombProgress = new ReadableProgress<float>();
            coroutine.Add(banditSmokeBombScaleFixAsync(banditSmokeBombProgress), banditSmokeBombProgress);

            static IEnumerator lightningStrikeImpactScaleFixAsync(IProgress<float> progressReceiver)
            {
                AsyncOperationHandle<GameObject> impactEffectLoad = AddressableUtil.LoadTempAssetAsync<GameObject>(RoR2_Base_Lightning.LightningStrikeImpact_prefab);

                yield return impactEffectLoad.AsProgressCoroutine(progressReceiver);

                if (!impactEffectLoad.AssertLoaded("LightningStrikeImpact"))
                    yield break;

                EffectDef impactEffectScaleFix = EffectScalingFixer.GetOrCreateFixedScalingCopy(impactEffectLoad.Result, LightningStrikeOrbRadius);
                if (impactEffectScaleFix != null)
                {
                    _lightningStrikeScalingFixPrefab = impactEffectScaleFix.prefab;
                }
            }

            ReadableProgress<float> lightningStrikeImpactProgress = new ReadableProgress<float>();
            coroutine.Add(lightningStrikeImpactScaleFixAsync(lightningStrikeImpactProgress), lightningStrikeImpactProgress);

            static IEnumerator simpleLightningStrikeImpactScaleFixAsync(IProgress<float> progressReceiver)
            {
                AsyncOperationHandle<GameObject> impactEffectLoad = AddressableUtil.LoadTempAssetAsync<GameObject>(RoR2_Base_LightningStrikeOnHit.SimpleLightningStrikeImpact_prefab);

                yield return impactEffectLoad.AsProgressCoroutine(progressReceiver);

                if (!impactEffectLoad.AssertLoaded("SimpleLightningStrikeImpact"))
                    yield break;

                EffectDef impactEffectScaleFix = EffectScalingFixer.GetOrCreateFixedScalingCopy(impactEffectLoad.Result,SimpleLightningStrikeOrbRadius);
                if (impactEffectScaleFix != null)
                {
                    _simpleLightningStrikeScalingFixPrefab = impactEffectScaleFix.prefab;
                }
            }

            ReadableProgress<float> simpleLightningStrikeImpactProgress = new ReadableProgress<float>();
            coroutine.Add(simpleLightningStrikeImpactScaleFixAsync(simpleLightningStrikeImpactProgress), simpleLightningStrikeImpactProgress);

            static IEnumerator meteorStormScaleFixAsync(IProgress<float> progressReceiver)
            {
                AsyncOperationHandle<GameObject> meteorStormLoad = AddressableUtil.LoadTempAssetAsync<GameObject>(RoR2_Base_Meteor.MeteorStorm_prefab);

                yield return meteorStormLoad.AsProgressCoroutine(progressReceiver);

                if (!meteorStormLoad.AssertLoaded("MeteorStorm"))
                    yield break;

                MeteorStormController meteorStormController = meteorStormLoad.Result.GetComponent<MeteorStormController>();
                if (!meteorStormController)
                {
                    Log.Error($"Missing MeteorStormController on {meteorStormLoad.Result}");
                    yield break;
                }

                float defaultRadius = meteorStormController.blastRadius;

                if (meteorStormController.warningEffectPrefab)
                {
                    EffectDef warningEffectScaleFix = EffectScalingFixer.GetOrCreateFixedScalingCopy(meteorStormController.warningEffectPrefab, defaultRadius);
                    if (warningEffectScaleFix != null)
                    {
                        _meteorWarningEffectScalingFixPrefab = warningEffectScaleFix.prefab;
                    }
                }

                if (meteorStormController.impactEffectPrefab)
                {
                    EffectDef impactEffectScaleFix = EffectScalingFixer.GetOrCreateFixedScalingCopy(meteorStormController.impactEffectPrefab, defaultRadius);
                    if (impactEffectScaleFix != null)
                    {
                        _meteorImpactEffectScalingFixPrefab = impactEffectScaleFix.prefab;
                    }
                }

                if (meteorStormController.travelEffectPrefab)
                {
                    EffectDef travelEffectScaleFix = EffectScalingFixer.GetOrCreateFixedScalingCopy(meteorStormController.travelEffectPrefab, defaultRadius);
                    if (travelEffectScaleFix != null)
                    {
                        _meteorTravelEffectScalingFixPrefab = travelEffectScaleFix.prefab;
                    }
                }
            }

            ReadableProgress<float> meteorStormProgress = new ReadableProgress<float>();
            coroutine.Add(meteorStormScaleFixAsync(meteorStormProgress), meteorStormProgress);

            static IEnumerator brotherFistSlamScaleFixAsync(IProgress<float> progressReceiver)
            {
                AsyncOperationHandle<EntityStateConfiguration> brotherFistSlamConfigurationLoad = AddressableUtil.LoadTempAssetAsync<EntityStateConfiguration>(RoR2_Base_Brother.EntityStates_BrotherMonster_FistSlam_asset);

                yield return brotherFistSlamConfigurationLoad.AsProgressCoroutine(progressReceiver);

                if (!brotherFistSlamConfigurationLoad.AssertLoaded("EntityStates.BrotherMonster.FistSlam"))
                    yield break;

                if (!brotherFistSlamConfigurationLoad.Result.TryGetFieldValue(nameof(EntityStates.BrotherMonster.FistSlam.radius), out float baseRadius))
                {
                    Log.Error("Failed to get EntityStates.BrotherMonster.FistSlam.radius field");
                    yield break;
                }

                if (brotherFistSlamConfigurationLoad.Result.TryGetFieldValue(nameof(EntityStates.BrotherMonster.FistSlam.slamImpactEffect), out GameObject brotherFistSlamImpactPrefab))
                {
                    EffectDef brotherFistSlamImpactScaleFixPrefab = EffectScalingFixer.GetOrCreateFixedScalingCopy(brotherFistSlamImpactPrefab, baseRadius);
                    if (brotherFistSlamImpactScaleFixPrefab != null)
                    {
                        _brotherFistSlamImpactScaleFixPrefab = brotherFistSlamImpactScaleFixPrefab.prefab;
                    }
                }
                else
                {
                    Log.Error("Failed to get EntityStates.BrotherMonster.FistSlam.slamImpactEffect field");
                }
            }

            ReadableProgress<float> brotherFistSlamProgress = new ReadableProgress<float>();
            coroutine.Add(brotherFistSlamScaleFixAsync(brotherFistSlamProgress), brotherFistSlamProgress);

            static IEnumerator brotherWeaponSlamScaleFixAsync(IProgress<float> progressReceiver)
            {
                AsyncOperationHandle<EntityStateConfiguration> brotherWeaponSlamConfigurationLoad = AddressableUtil.LoadTempAssetAsync<EntityStateConfiguration>(RoR2_Base_Brother.EntityStates_BrotherMonster_WeaponSlam_asset);

                yield return brotherWeaponSlamConfigurationLoad.AsProgressCoroutine(progressReceiver);

                if (!brotherWeaponSlamConfigurationLoad.AssertLoaded("EntityStates.BrotherMonster.WeaponSlam"))
                    yield break;

                if (!brotherWeaponSlamConfigurationLoad.Result.TryGetFieldValue(nameof(EntityStates.BrotherMonster.WeaponSlam.radius), out float baseRadius))
                {
                    Log.Error("Failed to get EntityStates.BrotherMonster.WeaponSlam.radius field");
                    yield break;
                }

                if (brotherWeaponSlamConfigurationLoad.Result.TryGetFieldValue(nameof(EntityStates.BrotherMonster.WeaponSlam.slamImpactEffect), out GameObject brotherWeaponSlamImpactPrefab))
                {
                    EffectDef brotherWeaponSlamImpactScaleFixPrefab = EffectScalingFixer.GetOrCreateFixedScalingCopy(brotherWeaponSlamImpactPrefab, baseRadius);
                    if (brotherWeaponSlamImpactScaleFixPrefab != null)
                    {
                        _brotherWeaponSlamImpactScaleFixPrefab = brotherWeaponSlamImpactScaleFixPrefab.prefab;
                    }
                }
                else
                {
                    Log.Error("Failed to get EntityStates.BrotherMonster.WeaponSlam.slamImpactEffect field");
                }
            }

            ReadableProgress<float> brotherWeaponSlamProgress = new ReadableProgress<float>();
            coroutine.Add(brotherWeaponSlamScaleFixAsync(brotherWeaponSlamProgress), brotherWeaponSlamProgress);

            static IEnumerator falseSonBossPrimarySlamScaleFixAsync(ExtendedContentPack contentPack, IProgress<float> progressReceiver)
            {
                AssetReferenceT<AnimationClip> falseSonBossPrimarySlamClipReference = new AssetReferenceT<AnimationClip>(RoR2_DLC2_FalseSon.AS_FalseSon_PrimarySlam_fbx_FSArmature_BossPrimarySlam_);
                AsyncOperationHandle<AnimationClip> falseSonBossPrimarySlamClipLoad = AssetAsyncReferenceManager<AnimationClip>.LoadAsset(falseSonBossPrimarySlamClipReference);
                AsyncOperationHandle<EntityStateConfiguration> falseSonFissureSlamConfigurationLoad = AddressableUtil.LoadTempAssetAsync<EntityStateConfiguration>(RoR2_DLC2_FalseSonBoss.EntityStates_FalseSonBoss_FissureSlam_asset);

                ParallelProgressCoroutine coroutine = new ParallelProgressCoroutine(progressReceiver);
                coroutine.Add(falseSonBossPrimarySlamClipLoad);
                coroutine.Add(falseSonFissureSlamConfigurationLoad);

                yield return coroutine;

                if (!falseSonBossPrimarySlamClipLoad.AssertLoaded("FSArmature|BossPrimarySlam") ||
                    !falseSonFissureSlamConfigurationLoad.AssertLoaded("EntityStates.FalseSonBoss.FissureSlam"))
                {
                    yield break;
                }

                if (!falseSonFissureSlamConfigurationLoad.Result.TryGetFieldValue(nameof(EntityStates.FalseSonBoss.FissureSlam.blastRadius), out float baseRadius))
                {
                    Log.Error("Failed to get EntityStates.FalseSonBoss.FissureSlam.blastRadius field");
                    yield break;
                }

                // FissureSlam adds 3 to the radius for the blast attack because ???
                baseRadius += 3f;

                AnimationEvent[] events = falseSonBossPrimarySlamClipLoad.Result.events;
                bool eventsChanged = false;
                bool foundCreateImpactEffectEvent = false;

                foreach (AnimationEvent evnt in events)
                {
                    if (evnt.functionName == nameof(AnimationEvents.CreatePrefab) &&
                        evnt.stringParameter == "ClubExplosionPoint" &&
                        evnt.objectReferenceParameter is GameObject explosionEffectPrefab && explosionEffectPrefab)
                    {
                        GameObject falseSonBossPrimarySlamImpactScaleFixPrefab = EffectScalingFixer.CreateFixedScalingCopy(explosionEffectPrefab, baseRadius);
                        falseSonBossPrimarySlamImpactScaleFixPrefab.name += "_FissureSlam";

                        falseSonBossPrimarySlamImpactScaleFixPrefab.EnsureComponent<LocalEffectOwnership>();

                        ExplosionRangeIndicatorScaler scaler = falseSonBossPrimarySlamImpactScaleFixPrefab.EnsureComponent<ExplosionRangeIndicatorScaler>();
                        scaler.ExplosionInfoIndex = ExplosionInfoIndex.FalseSonBossFissureSlam;
                        scaler.IndicatorTransforms = new Transform[] { falseSonBossPrimarySlamImpactScaleFixPrefab.transform };

                        evnt.objectReferenceParameter = falseSonBossPrimarySlamImpactScaleFixPrefab;

                        contentPack.effectDefs.Add(new EffectDef(falseSonBossPrimarySlamImpactScaleFixPrefab));

                        eventsChanged = true;
                        foundCreateImpactEffectEvent = true;
                    }
                }

                if (eventsChanged)
                {
                    falseSonBossPrimarySlamClipLoad.Result.events = events;
                }
                else
                {
                    if (!foundCreateImpactEffectEvent)
                    {
                        Log.Error($"Failed to find create impact effect animation event in {falseSonBossPrimarySlamClipLoad.Result.name}");
                    }

                    AssetAsyncReferenceManager<AnimationClip>.UnloadAsset(falseSonBossPrimarySlamClipReference);
                }
            }

            ReadableProgress<float> falseSonBossPrimarySlamProgress = new ReadableProgress<float>();
            coroutine.Add(falseSonBossPrimarySlamScaleFixAsync(args.ContentPack, falseSonBossPrimarySlamProgress), falseSonBossPrimarySlamProgress);

            static IEnumerator falseSonBossPrimeDevastatorScaleFixAsync(ExtendedContentPack contentPack, IProgress<float> progressReceiver)
            {
                AssetReferenceT<AnimationClip> falseSonBossPrimeDevastatorClipReference = new AssetReferenceT<AnimationClip>(RoR2_DLC2_FalseSon.AS_FalseSonBoss_PrimeDevastator_fbx_FSArmature_BossPrimaryDevastator_);
                AsyncOperationHandle<AnimationClip> falseSonBossPrimeDevastatorClipLoad = AssetAsyncReferenceManager<AnimationClip>.LoadAsset(falseSonBossPrimeDevastatorClipReference);
                AsyncOperationHandle<EntityStateConfiguration> falseSonPrimeDevastatorConfigurationLoad = AddressableUtil.LoadTempAssetAsync<EntityStateConfiguration>(RoR2_DLC2_FalseSonBoss.EntityStates_FalseSonBoss_PrimeDevastator_asset);

                ParallelProgressCoroutine coroutine = new ParallelProgressCoroutine(progressReceiver);
                coroutine.Add(falseSonBossPrimeDevastatorClipLoad);
                coroutine.Add(falseSonPrimeDevastatorConfigurationLoad);

                yield return coroutine;

                if (!falseSonBossPrimeDevastatorClipLoad.AssertLoaded("FSArmature|BossPrimaryDevastator") ||
                    !falseSonPrimeDevastatorConfigurationLoad.AssertLoaded("EntityStates.FalseSonBoss.PrimeDevastator"))
                {
                    yield break;
                }

                if (!falseSonPrimeDevastatorConfigurationLoad.Result.TryGetFieldValue(nameof(EntityStates.FalseSonBoss.PrimeDevastator.blastRadius), out float baseRadius))
                {
                    Log.Error("Failed to get EntityStates.FalseSonBoss.PrimeDevastator.blastRadius field");
                    yield break;
                }

                AnimationEvent[] events = falseSonBossPrimeDevastatorClipLoad.Result.events;
                bool eventsChanged = false;
                bool foundCreateImpactEffectEvent = false;

                foreach (AnimationEvent evnt in events)
                {
                    if (evnt.functionName == nameof(AnimationEvents.CreatePrefab) &&
                        evnt.objectReferenceParameter is GameObject explosionEffectPrefab && explosionEffectPrefab)
                    {
                        GameObject falseSonBossPrimeDevastatorImpactScaleFixPrefab = EffectScalingFixer.CreateFixedScalingCopy(explosionEffectPrefab, baseRadius);
                        falseSonBossPrimeDevastatorImpactScaleFixPrefab.name += "_PrimeDevastator";

                        falseSonBossPrimeDevastatorImpactScaleFixPrefab.EnsureComponent<LocalEffectOwnership>();

                        ExplosionRangeIndicatorScaler scaler = falseSonBossPrimeDevastatorImpactScaleFixPrefab.EnsureComponent<ExplosionRangeIndicatorScaler>();
                        scaler.ExplosionInfoIndex = ExplosionInfoIndex.FalseSonBossPrimeDevastator;
                        scaler.IndicatorTransforms = new Transform[] { falseSonBossPrimeDevastatorImpactScaleFixPrefab.transform };

                        evnt.objectReferenceParameter = falseSonBossPrimeDevastatorImpactScaleFixPrefab;

                        contentPack.effectDefs.Add(new EffectDef(falseSonBossPrimeDevastatorImpactScaleFixPrefab));

                        eventsChanged = true;
                        foundCreateImpactEffectEvent = true;
                    }
                }

                if (eventsChanged)
                {
                    falseSonBossPrimeDevastatorClipLoad.Result.events = events;
                }
                else
                {
                    if (!foundCreateImpactEffectEvent)
                    {
                        Log.Error($"Failed to find create impact effect animation event(s) in {falseSonBossPrimeDevastatorClipLoad.Result.name}");
                    }

                    AssetAsyncReferenceManager<AnimationClip>.UnloadAsset(falseSonBossPrimeDevastatorClipReference);
                }
            }

            ReadableProgress<float> falseSonBossPrimeDevastatorProgress = new ReadableProgress<float>();
            coroutine.Add(falseSonBossPrimeDevastatorScaleFixAsync(args.ContentPack, falseSonBossPrimeDevastatorProgress), falseSonBossPrimeDevastatorProgress);

            static IEnumerator golemClapScaleFixAsync(ExtendedContentPack contentPack, IProgress<float> progressReceiver)
            {
                AssetReferenceT<AnimationClip> golemClapClipReference = new AssetReferenceT<AnimationClip>(RoR2_Base_Golem.mdlGolem_fbx_GolemArmature_Smack_);
                AsyncOperationHandle<AnimationClip> golemClapClipLoad = AssetAsyncReferenceManager<AnimationClip>.LoadAsset(golemClapClipReference);
                AsyncOperationHandle<EntityStateConfiguration> golemClapConfigurationLoad = AddressableUtil.LoadTempAssetAsync<EntityStateConfiguration>(RoR2_Base_Golem.EntityStates_GolemMonster_ClapState_asset);

                ParallelProgressCoroutine coroutine = new ParallelProgressCoroutine(progressReceiver);
                coroutine.Add(golemClapClipLoad);
                coroutine.Add(golemClapConfigurationLoad);

                yield return coroutine;

                if (!golemClapClipLoad.AssertLoaded("GolemArmature|Smack") ||
                    !golemClapConfigurationLoad.AssertLoaded("EntityStates.GolemMonster.ClapState"))
                {
                    yield break;
                }

                if (!golemClapConfigurationLoad.Result.TryGetFieldValue(nameof(EntityStates.GolemMonster.ClapState.radius), out float baseRadius))
                {
                    Log.Error("Failed to get EntityStates.GolemMonster.ClapState.radius field");
                    yield break;
                }

                AnimationEvent[] events = golemClapClipLoad.Result.events;
                bool eventsChanged = false;
                bool foundCreateImpactEffectEvent = false;

                foreach (AnimationEvent evnt in events)
                {
                    if (evnt.functionName == nameof(AnimationEvents.CreatePrefab) &&
                        evnt.objectReferenceParameter is GameObject explosionEffectPrefab && explosionEffectPrefab)
                    {
                        GameObject golemClapImpactScaleFixPrefab = EffectScalingFixer.CreateFixedScalingCopy(explosionEffectPrefab, baseRadius);
                        golemClapImpactScaleFixPrefab.name += "_GolemClap";

                        golemClapImpactScaleFixPrefab.EnsureComponent<LocalEffectOwnership>();

                        ExplosionRangeIndicatorScaler scaler = golemClapImpactScaleFixPrefab.EnsureComponent<ExplosionRangeIndicatorScaler>();
                        scaler.ExplosionInfoIndex = ExplosionInfoIndex.GolemClap;
                        scaler.IndicatorTransforms = new Transform[] { golemClapImpactScaleFixPrefab.transform };

                        evnt.objectReferenceParameter = golemClapImpactScaleFixPrefab;

                        contentPack.effectDefs.Add(new EffectDef(golemClapImpactScaleFixPrefab));

                        eventsChanged = true;
                        foundCreateImpactEffectEvent = true;
                    }
                }

                if (eventsChanged)
                {
                    golemClapClipLoad.Result.events = events;
                }
                else
                {
                    if (!foundCreateImpactEffectEvent)
                    {
                        Log.Error($"Failed to find create impact effect animation event(s) in {golemClapClipLoad.Result.name}");
                    }

                    AssetAsyncReferenceManager<AnimationClip>.UnloadAsset(golemClapClipReference);
                }
            }

            ReadableProgress<float> golemClapProgress = new ReadableProgress<float>();
            coroutine.Add(golemClapScaleFixAsync(args.ContentPack, golemClapProgress), golemClapProgress);

            static IEnumerator golemLaserScaleFixAsync(IProgress<float> progressReceiver)
            {
                AsyncOperationHandle<EntityStateConfiguration> golemLaserConfigurationLoad = AddressableUtil.LoadTempAssetAsync<EntityStateConfiguration>(RoR2_Base_Golem.EntityStates_GolemMonster_FireLaser_asset);

                yield return golemLaserConfigurationLoad.AsProgressCoroutine(progressReceiver);

                if (!golemLaserConfigurationLoad.AssertLoaded("EntityStates.GolemMonster.FireLaser"))
                    yield break;

                if (!golemLaserConfigurationLoad.Result.TryGetFieldValue(nameof(EntityStates.GolemMonster.FireLaser.blastRadius), out float baseRadius))
                {
                    Log.Error("Failed to get EntityStates.GolemMonster.FireLaser.blastRadius field");
                    yield break;
                }

                if (golemLaserConfigurationLoad.Result.TryGetFieldValue(nameof(EntityStates.GolemMonster.FireLaser.hitEffectPrefab), out GameObject golemLaserImpactPrefab))
                {
                    EffectDef golemLaserImpactScaleFixPrefab = EffectScalingFixer.GetOrCreateFixedScalingCopy(golemLaserImpactPrefab, baseRadius);
                    if (golemLaserImpactScaleFixPrefab != null)
                    {
                        _golemLaserImpactScaleFixPrefab = golemLaserImpactScaleFixPrefab.prefab;
                    }
                }
                else
                {
                    Log.Error("Failed to get EntityStates.GolemMonster.FireLaser.hitEffectPrefab field");
                }
            }

            ReadableProgress<float> golemLaserProgress = new ReadableProgress<float>();
            coroutine.Add(golemLaserScaleFixAsync(golemLaserProgress), golemLaserProgress);

            static IEnumerator halcyoniteTriLaserScaleFixAsync(IProgress<float> progressReceiver)
            {
                AsyncOperationHandle<EntityStateConfiguration> halcyoniteTriLaserConfigurationLoad = AddressableUtil.LoadTempAssetAsync<EntityStateConfiguration>(RoR2_DLC2_Halcyonite.EntityStates_HalcyoniteMonster_TriLaser_asset);

                yield return halcyoniteTriLaserConfigurationLoad.AsProgressCoroutine(progressReceiver);

                if (!halcyoniteTriLaserConfigurationLoad.AssertLoaded("EntityStates.Halcyonite.TriLaser"))
                    yield break;

                if (!halcyoniteTriLaserConfigurationLoad.Result.TryGetFieldValue(nameof(EntityStates.Halcyonite.TriLaser.blastRadius), out float baseRadius))
                {
                    Log.Error("Failed to get EntityStates.Halcyonite.TriLaser.blastRadius field");
                    yield break;
                }

                if (halcyoniteTriLaserConfigurationLoad.Result.TryGetFieldValue(nameof(EntityStates.Halcyonite.TriLaser.hitEffectPrefab), out GameObject halcyoniteTriLaserImpactPrefab))
                {
                    EffectDef halcyoniteTriLaserImpactScaleFixPrefab = EffectScalingFixer.GetOrCreateFixedScalingCopy(halcyoniteTriLaserImpactPrefab, baseRadius);
                    if (halcyoniteTriLaserImpactScaleFixPrefab != null)
                    {
                        _halcyoniteTriLaserImpactScaleFixPrefab = halcyoniteTriLaserImpactScaleFixPrefab.prefab;
                    }
                }
                else
                {
                    Log.Error("Failed to get EntityStates.Halcyonite.TriLaser.hitEffectPrefab field");
                }
            }

            ReadableProgress<float> halcyoniteTriLaserProgress = new ReadableProgress<float>();
            coroutine.Add(halcyoniteTriLaserScaleFixAsync(halcyoniteTriLaserProgress), halcyoniteTriLaserProgress);

            static IEnumerator impBossBlinkScaleFixAsync(IProgress<float> progressReceiver)
            {
                AsyncOperationHandle<EntityStateConfiguration> impBossBlinkConfigurationLoad = AddressableUtil.LoadAssetAsync<EntityStateConfiguration>(RoR2_Base_ImpBoss.EntityStates_ImpBossMonster_BlinkState_asset);

                yield return impBossBlinkConfigurationLoad.AsProgressCoroutine(progressReceiver);

                if (!impBossBlinkConfigurationLoad.AssertLoaded("EntityStates.ImpBossMonster.BlinkState"))
                    yield break;

                if (!impBossBlinkConfigurationLoad.Result.TryGetFieldValue(nameof(EntityStates.ImpBossMonster.BlinkState.blastAttackRadius), out float baseRadius))
                {
                    Log.Error("Failed to get EntityStates.ImpBossMonster.BlinkState.blastAttackRadius field");
                    yield break;
                }

                if (impBossBlinkConfigurationLoad.Result.TryGetFieldValue(nameof(EntityStates.ImpBossMonster.BlinkState.blinkPrefab), out GameObject impBossBlinkPrefab))
                {
                    EffectDef impBossBlinkScaleFixPrefab = EffectScalingFixer.GetOrCreateFixedScalingCopy(impBossBlinkPrefab, baseRadius);
                    if (impBossBlinkScaleFixPrefab != null)
                    {
                        _impBossBlinkScaleFixPrefab = impBossBlinkScaleFixPrefab.prefab;
                    }
                }
                else
                {
                    Log.Error("Failed to get EntityStates.ImpBossMonster.BlinkState.blinkPrefab field");
                }

                if (impBossBlinkConfigurationLoad.Result.TryGetFieldValue(nameof(EntityStates.ImpBossMonster.BlinkState.blinkDestinationPrefab), out GameObject impBossBlinkDestinationPrefab))
                {
                    GameObject impBossBlinkDestinationScaleFixPrefab = EffectScalingFixer.CreateFixedScalingCopy(impBossBlinkDestinationPrefab, baseRadius);
                    if (impBossBlinkDestinationScaleFixPrefab)
                    {
                        impBossBlinkDestinationScaleFixPrefab.EnsureComponent<EffectManagerHelper>();
                        impBossBlinkDestinationScaleFixPrefab.EnsureComponent<LocalEffectOwnership>();

                        ExplosionRangeIndicatorScaler scaler = impBossBlinkDestinationScaleFixPrefab.EnsureComponent<ExplosionRangeIndicatorScaler>();
                        scaler.ExplosionInfoIndex = ExplosionInfoIndex.ImpBossBlink;
                        scaler.IndicatorTransforms = new Transform[] { impBossBlinkDestinationScaleFixPrefab.transform };

                        if (!impBossBlinkConfigurationLoad.Result.TrySetFieldValue(nameof(EntityStates.ImpBossMonster.BlinkState.blinkDestinationPrefab), impBossBlinkDestinationScaleFixPrefab))
                        {
                            Log.Error("Failed to set EntityStates.ImpBossMonster.BlinkState.blinkDestinationPrefab field");
                        }
                    }
                }
                else
                {
                    Log.Error("Failed to get EntityStates.ImpBossMonster.BlinkState.blinkDestinationPrefab field");
                }
            }

            ReadableProgress<float> impBossBlinkProgress = new ReadableProgress<float>();
            coroutine.Add(impBossBlinkScaleFixAsync(impBossBlinkProgress), impBossBlinkProgress);

            static IEnumerator impBossGroundPoundScaleFixAsync(IProgress<float> progressReceiver)
            {
                AsyncOperationHandle<EntityStateConfiguration> impBossGroundPoundConfigurationLoad = AddressableUtil.LoadTempAssetAsync<EntityStateConfiguration>(RoR2_Base_ImpBoss.EntityStates_ImpBossMonster_GroundPound_asset);

                yield return impBossGroundPoundConfigurationLoad.AsProgressCoroutine(progressReceiver);

                if (!impBossGroundPoundConfigurationLoad.AssertLoaded("EntityStates.ImpBossMonster.GroundPound"))
                    yield break;

                if (!impBossGroundPoundConfigurationLoad.Result.TryGetFieldValue(nameof(EntityStates.ImpBossMonster.GroundPound.blastAttackRadius), out float baseRadius))
                {
                    Log.Error("Failed to get EntityStates.ImpBossMonster.GroundPound.blastAttackRadius field");
                    yield break;
                }

                if (impBossGroundPoundConfigurationLoad.Result.TryGetFieldValue(nameof(EntityStates.ImpBossMonster.GroundPound.slamEffectPrefab), out GameObject impBossGroundPoundSlamPrefab))
                {
                    EffectDef impBossGroundPoundSlamScaleFixPrefab = EffectScalingFixer.GetOrCreateFixedScalingCopy(impBossGroundPoundSlamPrefab, baseRadius);
                    if (impBossGroundPoundSlamScaleFixPrefab != null)
                    {
                        _impBossGroundPoundSlamScaleFixPrefab = impBossGroundPoundSlamScaleFixPrefab.prefab;
                    }
                }
                else
                {
                    Log.Error("Failed to get EntityStates.ImpBossMonster.GroundPound.slamEffectPrefab field");
                }
            }

            ReadableProgress<float> impBossGroundPoundProgress = new ReadableProgress<float>();
            coroutine.Add(impBossGroundPoundScaleFixAsync(impBossGroundPoundProgress), impBossGroundPoundProgress);

            static IEnumerator parentGroundSlamScaleFixAsync(IProgress<float> progressReceiver)
            {
                AssetReferenceT<AnimationClip> parentGroundSlamClipReference = new AssetReferenceT<AnimationClip>(RoR2_Base_Parent.mdlParent_fbx_ParentArmature_Slam_);
                AsyncOperationHandle<AnimationClip> parentGroundSlamClipLoad = AssetAsyncReferenceManager<AnimationClip>.LoadAsset(parentGroundSlamClipReference);
                AsyncOperationHandle<EntityStateConfiguration> parentGroundSlamConfigurationLoad = AddressableUtil.LoadTempAssetAsync<EntityStateConfiguration>(RoR2_Base_Parent.EntityStates_ParentMonster_GroundSlam_asset);

                ParallelProgressCoroutine coroutine = new ParallelProgressCoroutine(progressReceiver);
                coroutine.Add(parentGroundSlamClipLoad);
                coroutine.Add(parentGroundSlamConfigurationLoad);

                yield return coroutine;

                if (!parentGroundSlamClipLoad.AssertLoaded("ParentArmature|Slam") ||
                    !parentGroundSlamConfigurationLoad.AssertLoaded("EntityStates.ParentMonster.GroundSlam"))
                {
                    yield break;
                }

                if (!parentGroundSlamConfigurationLoad.Result.TryGetFieldValue(nameof(EntityStates.ParentMonster.GroundSlam.radius), out float baseRadius))
                {
                    Log.Error("Failed to get EntityStates.ParentMonster.GroundSlam.radius field");
                    yield break;
                }

                AnimationEvent[] events = parentGroundSlamClipLoad.Result.events;
                bool eventsChanged = false;
                bool foundCreateImpactEffectEvent = false;

                foreach (AnimationEvent evnt in events)
                {
                    if (evnt.functionName == nameof(AnimationEvents.CreateEffect) &&
                        evnt.objectReferenceParameter is GameObject explosionEffectPrefab && explosionEffectPrefab)
                    {
                        EffectDef parentGroundSlamImpactScaleFixPrefab = EffectScalingFixer.GetOrCreateFixedScalingCopy(explosionEffectPrefab, baseRadius);
                        if (parentGroundSlamImpactScaleFixPrefab != null)
                        {
                            evnt.objectReferenceParameter = parentGroundSlamImpactScaleFixPrefab.prefab;
                            AnimationEffectSetExplosionScalePatch.SetEncodedExplosionIndex(evnt, ExplosionInfoIndex.ParentGroundSlam);

                            eventsChanged = true;
                        }

                        foundCreateImpactEffectEvent = true;
                    }
                }

                if (eventsChanged)
                {
                    parentGroundSlamClipLoad.Result.events = events;
                }
                else
                {
                    if (!foundCreateImpactEffectEvent)
                    {
                        Log.Error($"Failed to find create impact effect animation event(s) in {parentGroundSlamClipLoad.Result.name}");
                    }

                    AssetAsyncReferenceManager<AnimationClip>.UnloadAsset(parentGroundSlamClipReference);
                }
            }

            ReadableProgress<float> parentGroundSlamProgress = new ReadableProgress<float>();
            coroutine.Add(parentGroundSlamScaleFixAsync(parentGroundSlamProgress), parentGroundSlamProgress);

            static IEnumerator mageFlyUpBlinkScaleFixAsync(IProgress<float> progressReceiver)
            {
                AsyncOperationHandle<EntityStateConfiguration> mageFlyUpStateConfigurationLoad = AddressableUtil.LoadAssetAsync<EntityStateConfiguration>(RoR2_Base_Mage.EntityStates_Mage_FlyUpState_asset);

                yield return mageFlyUpStateConfigurationLoad.AsProgressCoroutine(progressReceiver);

                if (!mageFlyUpStateConfigurationLoad.AssertLoaded("EntityStates.Mage.FlyUpState"))
                    yield break;

                if (!mageFlyUpStateConfigurationLoad.Result.TryGetFieldValue(nameof(EntityStates.Mage.FlyUpState.blastAttackRadius), out float baseRadius))
                {
                    Log.Error("Failed to get EntityStates.Mage.FlyUpState.blastAttackRadius field");
                    yield break;
                }

                if (mageFlyUpStateConfigurationLoad.Result.TryGetFieldValue(nameof(EntityStates.Mage.FlyUpState.blinkPrefab), out GameObject mageFlyUpBlinkPrefab))
                {
                    EffectDef mageFlyUpBlinkScaleFixPrefab = EffectScalingFixer.GetOrCreateFixedScalingCopy(mageFlyUpBlinkPrefab, baseRadius);
                    if (mageFlyUpBlinkScaleFixPrefab != null)
                    {
                        _mageFlyUpBlinkScaleFixPrefab = mageFlyUpBlinkScaleFixPrefab.prefab;
                    }
                }
                else
                {
                    Log.Error("Failed to get EntityStates.Mage.FlyUpState.blinkPrefab field");
                }
            }

            ReadableProgress<float> mageFlyUpBlinkProgress = new ReadableProgress<float>();
            coroutine.Add(mageFlyUpBlinkScaleFixAsync(mageFlyUpBlinkProgress), mageFlyUpBlinkProgress);

            static IEnumerator junkCubeDamageImpactScaleFixAsync(IProgress<float> progressReceiver)
            {
                AssetReferenceT<EntityStateConfiguration>[] junkCubeDamageConfigurationReferences = new AssetReferenceT<EntityStateConfiguration>[]
                {
                    new AssetReferenceT<EntityStateConfiguration>(RoR2_DLC3_Drifter.JunkCube_DamageSmall_asset),
                    new AssetReferenceT<EntityStateConfiguration>(RoR2_DLC3_Drifter.JunkCube_DamageMedium_asset),
                    new AssetReferenceT<EntityStateConfiguration>(RoR2_DLC3_Drifter.JunkCube_DamageLarge_asset)
                };

                AsyncOperationHandle<EntityStateConfiguration>[] junkCubeDamageConfigurationLoadHandles = Array.ConvertAll(junkCubeDamageConfigurationReferences, r => AssetAsyncReferenceManager<EntityStateConfiguration>.LoadAsset(r));

                ParallelProgressCoroutine coroutine = new ParallelProgressCoroutine(progressReceiver);
                foreach (AsyncOperationHandle<EntityStateConfiguration> handle in junkCubeDamageConfigurationLoadHandles)
                {
                    coroutine.Add(handle);
                }

                yield return coroutine;

                for (int i = 0; i < junkCubeDamageConfigurationLoadHandles.Length; i++)
                {
                    AsyncOperationHandle<EntityStateConfiguration> handle = junkCubeDamageConfigurationLoadHandles[i];

                    bool modified = false;

                    string assetName = i switch
                    {
                        0 => "EntityStates.JunkCube.DamageSmall",
                        1 => "EntityStates.JunkCube.DamageMedium",
                        2 => "EntityStates.JunkCube.DamageLarge",
                        _ => throw new NotImplementedException()
                    };

                    if (handle.AssertLoaded(assetName))
                    {
                        EntityStateConfiguration stateConfiguration = handle.Result;
                        if (stateConfiguration.TryGetFieldValue(nameof(EntityStates.JunkCube.Damage.DamageRadius), out float baseRadius))
                        {
                            if (stateConfiguration.TryGetFieldValue(nameof(EntityStates.JunkCube.Damage.AttackVfxPrefab), out GameObject explosionEffectPrefab))
                            {
                                EffectDef explosionEffectScaleFixPrefab = EffectScalingFixer.GetOrCreateFixedScalingCopy(explosionEffectPrefab, baseRadius);
                                if (explosionEffectScaleFixPrefab != null)
                                {
                                    if (stateConfiguration.TrySetFieldValue(nameof(EntityStates.JunkCube.Damage.AttackVfxPrefab), explosionEffectScaleFixPrefab.prefab))
                                    {
                                        modified = true;
                                    }
                                    else
                                    {
                                        Log.Error($"Failed to set EntityStates.{stateConfiguration.name}.AttackVfxPrefab field");
                                    }
                                }
                            }
                            else
                            {
                                Log.Error($"Failed to get EntityStates.{stateConfiguration.name}.AttackVfxPrefab field");
                            }
                        }
                        else
                        {
                            Log.Error($"Failed to get EntityStates.{stateConfiguration.name}.DamageRadius field");
                        }
                    }

                    if (!modified)
                    {
                        AssetAsyncReferenceManager<EntityStateConfiguration>.UnloadAsset(junkCubeDamageConfigurationReferences[i]);
                    }
                }
            }

            ReadableProgress<float> junkCubeDamageImpactProgress = new ReadableProgress<float>();
            coroutine.Add(junkCubeDamageImpactScaleFixAsync(junkCubeDamageImpactProgress), junkCubeDamageImpactProgress);

            static IEnumerator junkCubeLaunchedImpactScaleFixAsync(IProgress<float> progressReceiver)
            {
                AssetReferenceT<EntityStateConfiguration> junkCubeLaunchedConfigurationReference = new AssetReferenceT<EntityStateConfiguration>(RoR2_DLC3_Drifter.EntityStates_JunkCube_Launched_asset);
                AsyncOperationHandle<EntityStateConfiguration> junkCubeLaunchedConfigurationLoad = AssetAsyncReferenceManager<EntityStateConfiguration>.LoadAsset(junkCubeLaunchedConfigurationReference);

                yield return junkCubeLaunchedConfigurationLoad.AsProgressCoroutine(progressReceiver);

                bool modified = false;

                if (junkCubeLaunchedConfigurationLoad.AssertLoaded("EntityStates.JunkCube.Launched"))
                {
                    EntityStateConfiguration stateConfiguration = junkCubeLaunchedConfigurationLoad.Result;
                    if (stateConfiguration.TryGetFieldValue(nameof(EntityStates.JunkCube.Launched.blastRadius), out float baseRadius))
                    {
                        if (stateConfiguration.TryGetFieldValue(nameof(EntityStates.JunkCube.Launched.impactEffectPrefab), out GameObject impactEffectPrefab))
                        {
                            EffectDef impactEffectScaleFixPrefab = EffectScalingFixer.GetOrCreateFixedScalingCopy(impactEffectPrefab, baseRadius);
                            if (impactEffectScaleFixPrefab != null)
                            {
                                if (stateConfiguration.TrySetFieldValue(nameof(EntityStates.JunkCube.Launched.impactEffectPrefab), impactEffectScaleFixPrefab.prefab))
                                {
                                    modified = true;
                                }
                                else
                                {
                                    Log.Error($"Failed to set EntityStates.JunkCube.Launched.impactEffectPrefab field");
                                }
                            }
                        }
                        else
                        {
                            Log.Error($"Failed to get EntityStates.JunkCube.Launched.impactEffectPrefab field");
                        }
                    }
                    else
                    {
                        Log.Error("Failed to get EntityStates.JunkCube.Launched.blastRadius field");
                    }
                }

                if (!modified)
                {
                    AssetAsyncReferenceManager<EntityStateConfiguration>.UnloadAsset(junkCubeLaunchedConfigurationReference);
                }
            }

            ReadableProgress<float> junkCubeLaunchedImpactProgress = new ReadableProgress<float>();
            coroutine.Add(junkCubeLaunchedImpactScaleFixAsync(junkCubeLaunchedImpactProgress), junkCubeLaunchedImpactProgress);

            static IEnumerator junkCubeDeathImpactScaleFixAsync(IProgress<float> progressReceiver)
            {
                AssetReferenceT<EntityStateConfiguration>[] junkCubeDeathConfigurationReferences = new AssetReferenceT<EntityStateConfiguration>[]
                {
                    new AssetReferenceT<EntityStateConfiguration>(RoR2_DLC3_Drifter.JunkCube_DeathSmall_asset),
                    new AssetReferenceT<EntityStateConfiguration>(RoR2_DLC3_Drifter.JunkCube_DeathMedium_asset),
                    new AssetReferenceT<EntityStateConfiguration>(RoR2_DLC3_Drifter.JunkCube_DeathLarge_asset)
                };

                AsyncOperationHandle<EntityStateConfiguration>[] junkCubeDeathConfigurationLoadHandles = Array.ConvertAll(junkCubeDeathConfigurationReferences, r => AssetAsyncReferenceManager<EntityStateConfiguration>.LoadAsset(r));

                ParallelProgressCoroutine coroutine = new ParallelProgressCoroutine(progressReceiver);
                foreach (AsyncOperationHandle<EntityStateConfiguration> handle in junkCubeDeathConfigurationLoadHandles)
                {
                    coroutine.Add(handle);
                }

                yield return coroutine;

                for (int i = 0; i < junkCubeDeathConfigurationLoadHandles.Length; i++)
                {
                    AsyncOperationHandle<EntityStateConfiguration> handle = junkCubeDeathConfigurationLoadHandles[i];

                    bool modified = false;

                    string assetName = i switch
                    {
                        0 => "EntityStates.JunkCube.DeathSmall",
                        1 => "EntityStates.JunkCube.DeathMedium",
                        2 => "EntityStates.JunkCube.DeathLarge",
                        _ => throw new NotImplementedException()
                    };

                    if (handle.AssertLoaded(assetName))
                    {
                        EntityStateConfiguration stateConfiguration = handle.Result;
                        if (stateConfiguration.TryGetFieldValue(nameof(EntityStates.JunkCube.DeathState.explosionRadius), out float baseRadius))
                        {
                            if (stateConfiguration.TryGetFieldValue(nameof(EntityStates.JunkCube.DeathState.explosionEffectPrefab), out GameObject explosionEffectPrefab))
                            {
                                EffectDef explosionEffectScaleFixPrefab = EffectScalingFixer.GetOrCreateFixedScalingCopy(explosionEffectPrefab, baseRadius);
                                if (explosionEffectScaleFixPrefab != null)
                                {
                                    if (stateConfiguration.TrySetFieldValue(nameof(EntityStates.JunkCube.DeathState.explosionEffectPrefab), explosionEffectScaleFixPrefab.prefab))
                                    {
                                        modified = true;
                                    }
                                    else
                                    {
                                        Log.Error($"Failed to set EntityStates.{stateConfiguration.name}.explosionEffectPrefab field");
                                    }
                                }
                            }
                            else
                            {
                                Log.Error($"Failed to get EntityStates.{stateConfiguration.name}.explosionEffectPrefab field");
                            }
                        }
                        else
                        {
                            Log.Error($"Failed to get EntityStates.{stateConfiguration.name}.explosionRadius field");
                        }
                    }

                    if (!modified)
                    {
                        AssetAsyncReferenceManager<EntityStateConfiguration>.UnloadAsset(junkCubeDeathConfigurationReferences[i]);
                    }
                }
            }

            ReadableProgress<float> junkCubeDeathImpactProgress = new ReadableProgress<float>();
            coroutine.Add(junkCubeDeathImpactScaleFixAsync(junkCubeDeathImpactProgress), junkCubeDeathImpactProgress);

            return coroutine;
        }

        [SystemInitializer(typeof(EffectCatalogUtils))]
        static void Init()
        {
            static void beaconImpactIndicatorScaler(string beaconGuid)
            {
                AddressableUtil.LoadAssetAsync<GameObject>(beaconGuid).OnSuccess(static beaconPrefab =>
                {
                    Transform indicatorRoot = beaconPrefab.transform.Find("Inactive");
                    if (!indicatorRoot)
                    {
                        Log.Error($"Failed to find prediction VFX for beacon prefab: {beaconPrefab}");
                        return;
                    }

                    foreach (ParticleSystem particleSystem in indicatorRoot.GetComponentsInChildren<ParticleSystem>(true))
                    {
                        ParticleSystem.MainModule main = particleSystem.main;
                        if (main.scalingMode == ParticleSystemScalingMode.Local)
                        {
                            main.scalingMode = ParticleSystemScalingMode.Hierarchy;

                            // HACK: Assume all child particle systems are not children of each other, and assume no rotation
                            particleSystem.transform.localScale = Vector3.Scale(particleSystem.transform.localScale, particleSystem.transform.lossyScale.Inverse());
                        }
                    }

                    ExplosionRangeIndicatorScaler indicatorScaler = beaconPrefab.EnsureComponent<ExplosionRangeIndicatorScaler>();
                    indicatorScaler.ExplosionInfoIndex = ExplosionInfoIndex.CaptainSupplyDropImpact;
                    indicatorScaler.IndicatorTransforms = new Transform[] { indicatorRoot };
                });
            }

            beaconImpactIndicatorScaler(RoR2_Base_Captain.CaptainSupplyDrop__Base_prefab);
            beaconImpactIndicatorScaler(RoR2_Base_Captain.CaptainSupplyDrop__EquipmentRestock_prefab);
            beaconImpactIndicatorScaler(RoR2_Base_Captain.CaptainSupplyDrop__Hacking_prefab);
            beaconImpactIndicatorScaler(RoR2_Base_Captain.CaptainSupplyDrop__Healing_prefab);
            beaconImpactIndicatorScaler(RoR2_Base_Captain.CaptainSupplyDrop__Plating_prefab);
            beaconImpactIndicatorScaler(RoR2_Base_Captain.CaptainSupplyDrop__Shocking_prefab);

            AddressableUtil.LoadAssetAsync<GameObject>(RoR2_DLC2_FalseSon.FalseSonMeridiansWillIndicator_prefab).OnSuccess(meridiansWillIndicator =>
            {
                meridiansWillIndicator.EnsureComponent<GenericOwnership>();

                ExplosionRangeIndicatorScaler explosionRangeIndicatorScaler = meridiansWillIndicator.EnsureComponent<ExplosionRangeIndicatorScaler>();
                explosionRangeIndicatorScaler.ExplosionInfoIndex = ExplosionInfoIndex.MeridiansWill;
                explosionRangeIndicatorScaler.IndicatorTransforms = new Transform[] { meridiansWillIndicator.transform };
            });

            AddressableUtil.LoadAssetAsync<GameObject>(RoR2_Base_Engi.EngiMine_prefab).OnSuccess(engiMinePrefab =>
            {
                List<Transform> indicatorTransforms = new List<Transform>();

                Transform weakIndicator = engiMinePrefab.transform.Find("WeakIndicator");
                if (weakIndicator)
                {
                    indicatorTransforms.Add(weakIndicator);
                }
                else
                {
                    Log.Warning($"Failed to find WeakIndicator transform on {engiMinePrefab}");
                }

                Transform strongIndicator = engiMinePrefab.transform.Find("StrongIndicator");
                if (strongIndicator)
                {
                    indicatorTransforms.Add(strongIndicator);
                }
                else
                {
                    Log.Warning($"Failed to find StrongIndicator transform on {engiMinePrefab}");
                }

                if (indicatorTransforms.Count > 0 && !engiMinePrefab.TryGetComponent(out ExplosionRangeIndicatorScaler indicatorScaler))
                {
                    indicatorScaler = engiMinePrefab.AddComponent<ExplosionRangeIndicatorScaler>();
                    indicatorScaler.ExplosionInfoIndex = ExplosionInfoIndex.EngiMine;
                    indicatorScaler.IndicatorTransforms = indicatorTransforms.ToArray();
                }
            });

            AddressableUtil.LoadAssetAsync<GameObject>(RoR2_Base_Captain.CaptainAirstrikeGhost1_prefab).OnSuccess(captainAirstrikeGhostPrefab =>
            {
                Transform expanderTransform = captainAirstrikeGhostPrefab.transform.Find("Expander");
                if (!expanderTransform)
                {
                    Log.Error($"Failed to find Expander child on {captainAirstrikeGhostPrefab}");
                    return;
                }

                if (!captainAirstrikeGhostPrefab.TryGetComponent(out ExplosionRangeIndicatorScaler indicatorScaler))
                {
                    indicatorScaler = captainAirstrikeGhostPrefab.AddComponent<ExplosionRangeIndicatorScaler>();
                    indicatorScaler.IndicatorTransforms = new Transform[] { expanderTransform };
                }
            });

            AddressableUtil.LoadAssetAsync<GameObject>(RoR2_Base_Captain.CaptainAirstrikeAltGhost_prefab).OnSuccess(captainAirstrikeAltGhostPrefab =>
            {
                List<Transform> indicatorTransforms = new List<Transform>();

                for (int i = captainAirstrikeAltGhostPrefab.transform.childCount - 1; i >= 0; i--)
                {
                    indicatorTransforms.Add(captainAirstrikeAltGhostPrefab.transform.GetChild(i));
                }

                if (indicatorTransforms.Count > 0)
                {
                    if (!captainAirstrikeAltGhostPrefab.TryGetComponent(out ExplosionRangeIndicatorScaler indicatorScaler))
                    {
                        indicatorScaler = captainAirstrikeAltGhostPrefab.AddComponent<ExplosionRangeIndicatorScaler>();
                        indicatorScaler.IndicatorTransforms = indicatorTransforms.ToArray();
                    }
                }
                else
                {
                    Log.Warning($"Failed to find indicator transforms for {captainAirstrikeAltGhostPrefab}");
                }
            });

            IL.EntityStates.Chef.RolyPoly.GearShift += getVisualBlastAttackRadiusManipulator(emitGetEntityStateAttackerBody, false);

            IL.EntityStates.Chef.YesChef.OnEnter += getSimpleEffectDataScaleManipulator(emitGetEntityStateAttackerBody);
            IL.EntityStates.Chef.YesChef.FixedUpdate += groupManipulators(getSimpleBlastAttackRadiusManipulator(emitGetEntityStateAttackerBody), getSimpleSphereSearchRadiusManipulator(emitGetEntityStateAttackerBody));

            IL.EntityStates.Drone.DroneBombardment.BombardmentDroneProjectileEffect.ExecuteRadialAttack += groupManipulators(getSimpleSphereSearchRadiusManipulator(emitGetEntityStateAttackerBody), getSimpleEffectDataScaleManipulator(emitGetEntityStateAttackerBody));
            IL.EntityStates.Drone.DroneBombardment.BombardmentDroneSkill.SpawnBombardmentRays += groupManipulators(getSimpleSphereSearchRadiusManipulator(emitGetEntityStateAttackerBody), getSimpleEffectDataScaleManipulator(emitGetEntityStateAttackerBody));

            IL.EntityStates.JellyfishMonster.JellyNova.OnEnter += JellyNova_ReplaceNovaRadius;

            IL.EntityStates.JunkCube.DeathState.Explode += JunkCube_DeathState_Explode_ReplaceRadius;

            IL.EntityStates.Mage.FlyUpState.OnEnter += getSimpleBlastAttackRadiusManipulator(emitGetEntityStateAttackerBody);
            IL.EntityStates.Mage.FlyUpState.CreateBlinkEffect += Mage_FlyUpState_CreateBlinkEffect_ReplaceEffectRadius;

            IL.EntityStates.Seeker.Meditate.Update += Seeker_Meditate_Update_ReplaceRadius;

            IL.EntityStates.SolusAmalgamator.ShockArmor.OnEnter += getSimpleEffectDataScaleManipulator(emitGetEntityStateAttackerBody);
            IL.EntityStates.SolusAmalgamator.ShockArmor.StartShock += getSimpleEffectDataScaleManipulator(emitGetEntityStateAttackerBody);
            IL.EntityStates.SolusAmalgamator.ShockArmor.ApplyShock += getSimpleBlastAttackRadiusManipulator(emitGetEntityStateAttackerBody);

            IL.EntityStates.VagrantMonster.ChargeMegaNova.OnEnter += ChargeMegaNova_ReplaceNovaRadius;
            IL.EntityStates.VagrantMonster.ChargeMegaNova.FixedUpdate += ChargeMegaNova_ReplaceNovaRadius;

            IL.EntityStates.VagrantNovaItem.ChargeState.OnEnter += VagrantNovaItem_ReplaceBlastRadius;
            IL.EntityStates.VagrantNovaItem.DetonateState.OnEnter += VagrantNovaItem_ReplaceBlastRadius;

            IL.RoR2.FireballVehicle.DetonateServer += getVisualBlastAttackRadiusManipulator(emitGetVehicleSeatPassengerBody);

            IL.RoR2.FissureSlamCracksController.DetonateMeteor += getVisualBlastAttackRadiusManipulator(emitGetFissureSlamCracksControllerOwnerBody);
            IL.RoR2.FissureSlamCracksController.DoMeteorEffect += getSimpleEffectDataScaleManipulator(emitGetFissureSlamCracksControllerOwnerBody);

            IL.RoR2.GlobalEventManager.FrozenExplosion += getVisualBlastAttackRadiusManipulator(emitGetMethodParameterBody);

            IL.RoR2.GlobalEventManager.OnHitAllProcess += getVisualBlastAttackRadiusManipulator(emitGetMethodParameterDamageInfoAttackerBody);

            IL.RoR2.GlobalEventManager.ProcIgniteOnKill += groupManipulators(getVisualBlastAttackRadiusManipulator(emitGetMethodParameterDamageReportAttackerBody), getSimpleSphereSearchRadiusManipulator(emitGetMethodParameterDamageReportAttackerBody));

            On.RoR2.Items.JumpDamageStrikeBodyBehavior.GetRadius += JumpDamageStrikeBodyBehavior_GetRadius_ReplaceRadius;

            IL.RoR2.Projectile.ProjectileExplosion.DetonateServer += getVisualBlastAttackRadiusManipulator(emitGetProjectileOwner);

            IL.RoR2.SojournVehicle.EndSojournVehicle += getVisualBlastAttackRadiusManipulator(emitGetVehicleSeatPassengerBody);

            IL.RoR2.VoidRaidCrab.LegController.DoToeConcussionBlastAuthority += getVisualBlastAttackRadiusManipulator(emitGetVoidRaidCrabLegControllerMainBody);

            IL.RoR2.WormBodyPositions2.FireImpactBlastAttack += getVisualBlastAttackRadiusManipulator(emitGetBodyComponentBody);

            On.RoR2.Projectile.DroneBallShootableController.Start += DroneBallShootableController_Start_ReplaceRadius;

            IL.EntityStates.Bandit2.StealthMode.FireSmokebomb += StealthMode_FireSmokebomb_ReplaceRadius;

            IL.RoR2.Projectile.ProjectileFunballBehavior.FixedUpdate += getVisualBlastAttackRadiusManipulator(emitGetProjectileOwner);

            IL.EntityStates.CaptainSupplyDrop.HitGroundState.OnEnter += HitGroundState_OnEnter_ReplaceRadius;

            IL.EntityStates.FalseSon.ClubGroundSlam.DetonateAuthority += ClubGroundSlam_DetonateAuthority_ReplaceRadius;

            IL.EntityStates.FalseSon.ChargedClubSwing.DoBlastAttack += getSimpleBlastAttackRadiusManipulator(emitGetEntityStateAttackerBody);
            IL.EntityStates.FalseSon.ChargedClubSwing.InitializeBlastAttackAsCharged += ChargedClubSwing_InitializeBlastAttackAsCharged_ReplaceEffectRadius;

            IL.EntityStates.FalseSon.MeridiansWillFire.GetHurtBoxs += getSimpleSphereSearchRadiusManipulator(emitGetEntityStateAttackerBody);

            On.EntityStates.FalseSon.MeridiansWillAim.OnEnter += MeridiansWillAim_OnEnter_SetIndicatorOwner;
            On.EntityStates.FalseSon.MeridiansWillAim.OnExit += MeridiansWillAim_OnExit_UnsetIndicatorOwner;

            IL.RoR2.Orbs.LightningStrikeOrb.OnArrival += LightningStrikeOrb_OnArrival_ReplaceRadius;

            IL.RoR2.Orbs.SimpleLightningStrikeOrb.OnArrival += SimpleLightningStrikeOrb_OnArrival_ReplaceRadius;

            IL.RoR2.MeteorStormController.FixedUpdate += MeteorStormController_FixedUpdate_ReplaceRadius;
            IL.RoR2.MeteorStormController.DetonateMeteor += MeteorStormController_DetonateMeteor_ReplaceRadius;

            IL.RoR2.MeteorStormController.DoMeteorEffect += MeteorStormController_DoMeteorEffect_ReplaceTravelEffectRadius;

            IL.EntityStates.BrotherMonster.FistSlam.FixedUpdate += FistSlam_FixedUpdate_ReplaceRadius;
            IL.EntityStates.BrotherMonster.WeaponSlam.FixedUpdate += WeaponSlam_FixedUpdate_ReplaceRadius;

            IL.EntityStates.FalseSonBoss.FissureSlam.DetonateAuthority += getSimpleBlastAttackRadiusManipulator(emitGetEntityStateAttackerBody);

            IL.EntityStates.FalseSonBoss.CorruptedPathsDash.FixedUpdate += CorruptedPathsDash_FixedUpdate_ReplaceRadius;

            IL.EntityStates.FalseSonBoss.PrimeDevastator.DetonateAuthority += getSimpleBlastAttackRadiusManipulator(emitGetEntityStateAttackerBody);

            IL.EntityStates.GolemMonster.ClapState.FixedUpdate += getSimpleBlastAttackRadiusManipulator(emitGetEntityStateAttackerBody);

            IL.EntityStates.GolemMonster.FireLaser.OnEnter += FireLaser_OnEnter_ReplaceRadius;

            IL.EntityStates.Halcyonite.TriLaser.FireTriLaser += TriLaser_FireTriLaser_ReplaceRadius;

            IL.EntityStates.ImpBossMonster.BlinkState.ExitCleanup += getSimpleBlastAttackRadiusManipulator(emitGetEntityStateAttackerBody);
            IL.EntityStates.ImpBossMonster.BlinkState.CreateBlinkEffect += ImpBoss_BlinkState_CreateBlinkEffect_ReplaceRadius;
            IL.EntityStates.ImpBossMonster.BlinkState.FixedUpdate += ImpBoss_BlinkState_FixedUpdate_SetBlinkDestinationEffectOwner;

            IL.EntityStates.ImpBossMonster.GroundPound.OnEnter += getSimpleBlastAttackRadiusManipulator(emitGetEntityStateAttackerBody);
            IL.EntityStates.ImpBossMonster.GroundPound.FixedUpdate += ImpBoss_GroundPound_FixedUpdate_ReplaceEffectRadius;

            IL.EntityStates.ParentMonster.GroundSlam.FixedUpdate += getSimpleBlastAttackRadiusManipulator(emitGetEntityStateAttackerBody);

            IL.EntityStates.AimThrowableBase.OnEnter += AimThrowableBase_OnEnter_ReplaceEndpointRadius;

            RoR2Application.onLoad += onLoad;
        }

        static void onLoad()
        {
            HashSet<Type> allEntityStateTypes = new HashSet<Type>(EntityStateCatalog.stateIndexToType.Length);

            for (int i = 0; i < EntityStateCatalog.stateIndexToType.Length; i++)
            {
                Type stateType = EntityStateCatalog.stateIndexToType[i];
                while (stateType != null && typeof(EntityState).IsAssignableFrom(stateType) && allEntityStateTypes.Add(stateType))
                {
                    stateType = stateType.BaseType;
                }
            }

            int numAppliedHooks = 0;

            if (allEntityStateTypes.Count > 0)
            {
                ILContext.Manipulator manipulator = getVisualBlastAttackRadiusManipulator(emitGetEntityStateAttackerBody);

                foreach (Type stateType in allEntityStateTypes)
                {
                    try
                    {
                        if (stateType.Assembly == Assembly.GetExecutingAssembly())
                            continue;

                        foreach (MethodInfo method in stateType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly))
                        {
                            ILHook hook = null;
                            try
                            {
                                // The IsGenericMethod call sometimes causes a crash if accessed on a method where an assembly reference can't be resolved,
                                // the DeclaringType getter throws an exception instead, so do that first to catch it before trying to check IsGenericMethod
                                _ = method.DeclaringType;
                                if (method.IsGenericMethod || method.GetMethodBody() == null)
                                    continue;

                                using DynamicMethodDefinition dmd = new DynamicMethodDefinition(method);
                                using ILContext il = new ILContext(dmd.Definition);

                                if (matchSetupBlastAttack(il))
                                {
                                    hook = new ILHook(method, manipulator, new ILHookConfig { ManualApply = true });
                                    hook.Apply();
                                }
                            }
                            catch (Exception e)
                            {
                                Log.Warning($"Failed to apply attack radius hook to {method.DeclaringType.FullName}.{method.Name} ({stateType.Assembly.FullName}): {e.Message}");

                                hook?.Dispose();
                                hook = null;
                            }

                            if (hook != null)
                            {
                                numAppliedHooks++;
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        Log.Warning($"Failed to scan type for attack radius hooks: {stateType.FullName} ({stateType.Assembly.FullName}): {e.Message}");
                    }
                }
            }

            Log.Debug($"Applied {numAppliedHooks} attack radius method hook(s)");
        }

        static bool matchLoadValue(Instruction x, out Instruction instruction)
        {
            if (x.MatchCallOrCallvirt(out _) ||
                x.MatchLdsfld(out _) ||
                x.MatchLdfld(out _) ||
                x.MatchLdloc(out _) ||
                x.MatchLdarg(out _) ||
                x.MatchLdcR4(out _))
            {
                instruction = x;
                return true;
            }

            instruction = null;
            return false;
        }

        static bool instructionsEqual(Instruction a, Instruction b)
        {
            if (a.MatchLdcR4(out float constFloat))
            {
                return b.MatchLdcR4(constFloat);
            }

            if (a.MatchLdarg(out int argIndex))
            {
                return b.MatchLdarg(argIndex);
            }

            if (a.MatchLdloc(out int locIndex))
            {
                return b.MatchLdloc(locIndex);
            }

            if (a.MatchLdfld(out FieldReference fieldA))
            {
                return b.MatchLdfld(out FieldReference fieldB) && fieldA.FullName == fieldB.FullName;
            }

            if (a.MatchLdsfld(out FieldReference staticFieldA))
            {
                return b.MatchLdsfld(out FieldReference staticFieldB) && staticFieldA.FullName == staticFieldB.FullName;
            }

            if (a.MatchCallOrCallvirt(out MethodReference methodA))
            {
                return b.MatchCallOrCallvirt(out MethodReference methodB) && methodA.FullName == methodB.FullName;
            }

            return false;
        }

        static bool matchSetupBlastAttack(ILContext il)
        {
            ILCursor c = new ILCursor(il);

            Instruction loadRadiusValueInstruction = null;
            if (!c.TryGotoNext(MoveType.After,
                               x => matchLoadValue(x, out loadRadiusValueInstruction),
                               x => x.MatchStfld<BlastAttack>(nameof(BlastAttack.radius))))
            {
                return false;
            }

            Func<Instruction, bool>[] setEffectScaleMatch = new Func<Instruction, bool>[]
            {
                x => instructionsEqual(x, loadRadiusValueInstruction),
                x => x.MatchStfld<EffectData>(nameof(EffectData.scale))
            };

            return c.TryGotoNext(setEffectScaleMatch) || c.TryGotoPrev(setEffectScaleMatch);
        }

        // FML
        static void JellyNova_ReplaceNovaRadius(ILContext il)
        {
            ILCursor c = new ILCursor(il);

            int patchCount = 0;

            while (c.TryGotoNext(MoveType.After,
                                 x => x.MatchLdsfld<EntityStates.JellyfishMonster.JellyNova>(nameof(EntityStates.JellyfishMonster.JellyNova.novaRadius))))
            {
                c.Emit(OpCodes.Ldarg_0);
                c.EmitDelegate<Func<float, EntityState, float>>(getRadius);

                static float getRadius(float radius, EntityState entityState)
                {
                    return GetExplosionRadius(radius, entityState?.characterBody);
                }

                patchCount++;
            }

            if (patchCount == 0)
            {
                Log.Error("Failed to find patch location");
            }
            else
            {
                Log.Debug($"Found {patchCount} patch location(s)");
            }
        }

        static void ChargeMegaNova_ReplaceNovaRadius(ILContext il)
        {
            ILCursor c = new ILCursor(il);

            int patchCount = 0;

            while (c.TryGotoNext(MoveType.After,
                                 x => x.MatchLdsfld<EntityStates.VagrantMonster.ChargeMegaNova>(nameof(EntityStates.VagrantMonster.ChargeMegaNova.novaRadius))))
            {
                c.Emit(OpCodes.Ldarg_0);
                c.EmitDelegate<Func<float, EntityState, float>>(getRadius);

                static float getRadius(float radius, EntityState entityState)
                {
                    return GetExplosionRadius(radius, entityState?.characterBody);
                }

                patchCount++;
            }

            if (patchCount == 0)
            {
                Log.Error("Failed to find patch location");
            }
            else
            {
                Log.Debug($"Found {patchCount} patch location(s)");
            }
        }

        static void VagrantNovaItem_ReplaceBlastRadius(ILContext il)
        {
            ILCursor c = new ILCursor(il);

            int patchCount = 0;

            while (c.TryGotoNext(MoveType.After,
                                 x => x.MatchLdsfld<EntityStates.VagrantNovaItem.DetonateState>(nameof(EntityStates.VagrantNovaItem.DetonateState.blastRadius))))
            {
                c.Emit(OpCodes.Ldarg_0);
                c.EmitDelegate<Func<float, EntityState, float>>(getRadius);

                static float getRadius(float radius, EntityState entityState)
                {
                    return GetExplosionRadius(radius, entityState?.characterBody);
                }

                patchCount++;
            }

            if (patchCount == 0)
            {
                Log.Error("Failed to find patch location");
            }
            else
            {
                Log.Debug($"Found {patchCount} patch location(s)");
            }
        }

        static float JumpDamageStrikeBodyBehavior_GetRadius_ReplaceRadius(On.RoR2.Items.JumpDamageStrikeBodyBehavior.orig_GetRadius orig, JumpDamageStrikeBodyBehavior self, int charge, int stacks)
        {
            return GetExplosionRadius(orig(self, charge, stacks), self.body);
        }

        static void DroneBallShootableController_Start_ReplaceRadius(On.RoR2.Projectile.DroneBallShootableController.orig_Start orig, DroneBallShootableController self)
        {
            if (self &&
                self.TryGetComponent(out ProjectileController projectileController) &&
                projectileController.owner &&
                projectileController.owner.TryGetComponent(out CharacterBody ownerBody))
            {
                self.minRadius = GetExplosionRadius(self.minRadius, ownerBody);
                self.maxRadius = GetExplosionRadius(self.maxRadius, ownerBody);
            }

            orig(self);
        }

        static void StealthMode_FireSmokebomb_ReplaceRadius(ILContext il)
        {
            getSimpleBlastAttackRadiusManipulator(emitGetEntityStateAttackerBody).Invoke(il);

            ILCursor c = new ILCursor(il);

            ILLabel afterSpawnBlastEffectLabel = null;
            if (!c.TryGotoNext(MoveType.AfterLabel,
                               x => x.MatchLdsfld<EntityStates.Bandit2.StealthMode>(nameof(EntityStates.Bandit2.StealthMode.smokeBombEffectPrefab)),
                               x => x.MatchImplicitConversion<UnityEngine.Object, bool>(),
                               x => x.MatchBrfalse(out afterSpawnBlastEffectLabel)))
            {
                Log.Error("Failed to find patch location");
                return;
            }

            c.Emit(OpCodes.Ldarg_0);
            c.EmitDelegate<Func<EntityStates.Bandit2.StealthMode, bool>>(trySpawnEffectScaled);
            c.Emit(OpCodes.Brtrue, afterSpawnBlastEffectLabel);

            static bool trySpawnEffectScaled(EntityStates.Bandit2.StealthMode stealthMode)
            {
                if (!_banditSmokeBombScalingFixPrefab)
                    return false;

                if (!isScaledExplosion(EntityStates.Bandit2.StealthMode.blastAttackRadius, stealthMode?.characterBody))
                    return false;

                spawnEffectAtMuzzle(_banditSmokeBombScalingFixPrefab, new EffectData
                {
                    scale = GetExplosionRadius(EntityStates.Bandit2.StealthMode.blastAttackRadius, stealthMode.characterBody)
                }, stealthMode.gameObject, EntityStates.Bandit2.StealthMode.smokeBombMuzzleString, false);

                return true;
            }
        }

        static void HitGroundState_OnEnter_ReplaceRadius(ILContext il)
        {
            ILCursor c = new ILCursor(il);

            int patchCount = 0;

            while (c.TryGotoNext(MoveType.After,
                                 x => x.MatchLdsfld<EntityStates.CaptainSupplyDrop.HitGroundState>(nameof(EntityStates.CaptainSupplyDrop.HitGroundState.impactBulletRadius))))
            {
                emitGetEntityStateAttackerBody(c);
                c.EmitDelegate<Func<float, CharacterBody, float>>(GetExplosionRadius);

                patchCount++;
            }

            if (patchCount == 0)
            {
                Log.Error("Failed to find patch location");
            }
            else
            {
                Log.Debug($"Found {patchCount} patch location(s)");
            }
        }

        static void ClubGroundSlam_DetonateAuthority_ReplaceRadius(ILContext il)
        {
            if (!simpleBlastAttackRadiusManipulator(il, emitGetEntityStateAttackerBody))
                return;

            ILCursor c = new ILCursor(il);

            if (!c.TryGotoNext(MoveType.After,
                               x => x.MatchLdsfld<EntityStates.FalseSon.ClubGroundSlam>(nameof(EntityStates.FalseSon.ClubGroundSlam.blastVFXScaleMultiplier)),
                               x => x.MatchMul()))
            {
                Log.Error("Failed to find effect scale patch location");
                return;
            }

            emitGetEntityStateAttackerBody(c);
            c.EmitDelegate<Func<float, CharacterBody, float>>(GetExplosionRadius);
        }

        static void ChargedClubSwing_InitializeBlastAttackAsCharged_ReplaceEffectRadius(ILContext il)
        {
            ILCursor c = new ILCursor(il);

            if (!c.TryGotoNext(MoveType.After,
                               x => x.MatchLdarg(0),
                               x => x.MatchLdfld<EntityStates.FalseSon.ChargedClubSwing>(nameof(EntityStates.FalseSon.ChargedClubSwing.charge)),
                               x => x.MatchLdsfld<EntityStates.FalseSon.ChargedClubSwing>(nameof(EntityStates.FalseSon.ChargedClubSwing.blastVFXScaleMultiplier)),
                               x => x.MatchMul()))
            {
                Log.Error("Failed to find effect scale patch location");
                return;
            }

            emitGetEntityStateAttackerBody(c);
            c.EmitDelegate<Func<float, CharacterBody, float>>(GetExplosionRadius);
        }

        static void MeridiansWillAim_OnEnter_SetIndicatorOwner(On.EntityStates.FalseSon.MeridiansWillAim.orig_OnEnter orig, EntityStates.FalseSon.MeridiansWillAim self)
        {
            orig(self);

            try
            {
                if (self?.areaIndicatorInstance && self.areaIndicatorInstance.TryGetComponent(out GenericOwnership areaIndicatorOwnership))
                {
                    areaIndicatorOwnership.ownerObject = self.gameObject;
                }
            }
            catch (Exception e)
            {
                Log.Error_NoCallerPrefix(e);
            }
        }

        static void MeridiansWillAim_OnExit_UnsetIndicatorOwner(On.EntityStates.FalseSon.MeridiansWillAim.orig_OnExit orig, EntityStates.FalseSon.MeridiansWillAim self)
        {
            try
            {
                if (self?.areaIndicatorInstance && self.areaIndicatorInstance.TryGetComponent(out GenericOwnership areaIndicatorOwnership))
                {
                    areaIndicatorOwnership.ownerObject = null;
                }
            }
            catch (Exception e)
            {
                Log.Error_NoCallerPrefix(e);
            }

            orig(self);
        }

        static void LightningStrikeOrb_OnArrival_ReplaceRadius(ILContext il)
        {
            if (!simpleBlastAttackRadiusManipulator(il, emitGetOrbOwnerBody))
                return;

            ILCursor c = new ILCursor(il);

            if (!c.TryGotoNext(MoveType.After,
                               x => x.MatchLdstr("Prefabs/Effects/ImpactEffects/LightningStrikeImpact"),
                               x => x.MatchCallOrCallvirt(typeof(OrbStorageUtility), nameof(OrbStorageUtility.Get))))
            {
                Log.Error("Failed to find impact prefab patch location");
                return;
            }

            static bool isScaledImpact(LightningStrikeOrb self)
            {
                CharacterBody attackerBody = self?.attacker ? self.attacker.GetComponent<CharacterBody>() : null;

                float scaledRadius = GetExplosionRadius(LightningStrikeOrbRadius, attackerBody);

                return Mathf.Abs((scaledRadius / LightningStrikeOrbRadius) - 1f) > Mathf.Epsilon;
            }

            c.Emit(OpCodes.Ldarg_0);
            c.EmitDelegate<Func<GameObject, LightningStrikeOrb, GameObject>>(getImpactPrefab);

            static GameObject getImpactPrefab(GameObject prefab, LightningStrikeOrb self)
            {
                return isScaledImpact(self) && _lightningStrikeScalingFixPrefab ? _lightningStrikeScalingFixPrefab : prefab;
            }

            if (!c.TryGotoNext(MoveType.After,
                               x => x.MatchNewobj<EffectData>()))
            {
                Log.Error("Failed to find impact effect patch location");
                return;
            }

            c.Emit(OpCodes.Dup);
            c.Emit(OpCodes.Ldarg_0);
            c.EmitDelegate<Action<EffectData, LightningStrikeOrb>>(populateImpactEffect);

            static void populateImpactEffect(EffectData effectData, LightningStrikeOrb self)
            {
                if (isScaledImpact(self))
                {
                    CharacterBody attackerBody = self?.attacker ? self.attacker.GetComponent<CharacterBody>() : null;
                    effectData.scale = GetExplosionRadius(LightningStrikeOrbRadius, attackerBody);
                }
            }
        }

        static void SimpleLightningStrikeOrb_OnArrival_ReplaceRadius(ILContext il)
        {
            if (!simpleBlastAttackRadiusManipulator(il, emitGetOrbOwnerBody))
                return;

            ILCursor c = new ILCursor(il);

            if (!c.TryGotoNext(MoveType.After,
                               x => x.MatchLdstr("Prefabs/Effects/ImpactEffects/SimpleLightningStrikeImpact"),
                               x => x.MatchCallOrCallvirt(typeof(OrbStorageUtility), nameof(OrbStorageUtility.Get))))
            {
                Log.Error("Failed to find impact prefab patch location");
                return;
            }

            static bool isScaledImpact(SimpleLightningStrikeOrb self)
            {
                CharacterBody attackerBody = self?.attacker ? self.attacker.GetComponent<CharacterBody>() : null;
                if (!attackerBody)
                    return false;

                float scaledRadius = GetExplosionRadius(SimpleLightningStrikeOrbRadius, attackerBody);

                return Mathf.Abs((scaledRadius / SimpleLightningStrikeOrbRadius) - 1f) > Mathf.Epsilon;
            }

            c.Emit(OpCodes.Ldarg_0);
            c.EmitDelegate<Func<GameObject, SimpleLightningStrikeOrb, GameObject>>(getImpactPrefab);

            static GameObject getImpactPrefab(GameObject prefab, SimpleLightningStrikeOrb self)
            {
                return isScaledImpact(self) && _simpleLightningStrikeScalingFixPrefab ? _simpleLightningStrikeScalingFixPrefab : prefab;
            }

            if (!c.TryGotoNext(MoveType.After,
                               x => x.MatchNewobj<EffectData>()))
            {
                Log.Error("Failed to find impact effect patch location");
                return;
            }

            c.Emit(OpCodes.Dup);
            c.Emit(OpCodes.Ldarg_0);
            c.EmitDelegate<Action<EffectData, SimpleLightningStrikeOrb>>(populateImpactEffect);

            static void populateImpactEffect(EffectData effectData, SimpleLightningStrikeOrb self)
            {
                if (isScaledImpact(self))
                {
                    CharacterBody attackerBody = self?.attacker ? self.attacker.GetComponent<CharacterBody>() : null;
                    effectData.scale = GetExplosionRadius(SimpleLightningStrikeOrbRadius, attackerBody);
                }
            }
        }

        static void MeteorStormController_ReplaceRadius(ILContext il)
        {
            ILCursor c = new ILCursor(il);

            int patchCount = 0;

            while (c.TryGotoNext(MoveType.After,
                                 x => x.MatchLdfld<MeteorStormController>(nameof(MeteorStormController.blastRadius))))
            {
                emitGetMeteorStormControllerOwner(c);
                c.EmitDelegate<Func<float, CharacterBody, float>>(GetExplosionRadius);

                patchCount++;
            }

            if (patchCount == 0)
            {
                Log.Error($"{il.Method.FullName}: Failed to find patch location");
            }
            else
            {
                Log.Debug($"{il.Method.FullName}: Found {patchCount} patch location(s)");
            }
        }

        static void MeteorStormController_FixedUpdate_ReplaceRadius(ILContext il)
        {
            MeteorStormController_ReplaceRadius(il);

            ILCursor c = new ILCursor(il);

            if (!c.TryGotoNext(MoveType.After,
                               x => x.MatchLdfld<MeteorStormController>(nameof(MeteorStormController.warningEffectPrefab))))
            {
                Log.Error("Failed to find warning prefab patch location");
                return;
            }

            static bool isScaledImpact(MeteorStormController self)
            {
                if (!self)
                    return false;

                CharacterBody attackerBody = self.owner ? self.owner.GetComponent<CharacterBody>() : null;

                float scaledRadius = GetExplosionRadius(self.blastRadius, attackerBody);

                return Mathf.Abs((scaledRadius / self.blastRadius) - 1f) > Mathf.Epsilon;
            }

            c.Emit(OpCodes.Ldarg_0);
            c.EmitDelegate<Func<GameObject, MeteorStormController, GameObject>>(getWarningPrefab);

            static GameObject getWarningPrefab(GameObject prefab, MeteorStormController self)
            {
                return isScaledImpact(self) && _meteorWarningEffectScalingFixPrefab ? _meteorWarningEffectScalingFixPrefab : prefab;
            }
        }

        static void MeteorStormController_DetonateMeteor_ReplaceRadius(ILContext il)
        {
            MeteorStormController_ReplaceRadius(il);

            ILCursor c = new ILCursor(il);

            if (!c.TryGotoNext(MoveType.After,
                               x => x.MatchLdfld<MeteorStormController>(nameof(MeteorStormController.impactEffectPrefab))))
            {
                Log.Error("Failed to find impact prefab patch location");
                return;
            }

            static bool isScaledImpact(MeteorStormController self)
            {
                if (!self)
                    return false;

                CharacterBody attackerBody = self.owner ? self.owner.GetComponent<CharacterBody>() : null;

                float scaledRadius = GetExplosionRadius(self.blastRadius, attackerBody);

                return Mathf.Abs((scaledRadius / self.blastRadius) - 1f) > Mathf.Epsilon;
            }

            c.Emit(OpCodes.Ldarg_0);
            c.EmitDelegate<Func<GameObject, MeteorStormController, GameObject>>(getImpactPrefab);

            static GameObject getImpactPrefab(GameObject prefab, MeteorStormController self)
            {
                return isScaledImpact(self) && _meteorImpactEffectScalingFixPrefab ? _meteorImpactEffectScalingFixPrefab : prefab;
            }

            if (!c.TryGotoPrev(MoveType.After,
                               x => x.MatchNewobj<EffectData>()))
            {
                Log.Error("Failed to find impact effect patch location");
                return;
            }

            c.Emit(OpCodes.Dup);
            c.Emit(OpCodes.Ldarg_0);
            c.EmitDelegate<Action<EffectData, MeteorStormController>>(populateImpactEffect);

            static void populateImpactEffect(EffectData effectData, MeteorStormController self)
            {
                if (self && isScaledImpact(self))
                {
                    CharacterBody attackerBody = self.owner ? self.owner.GetComponent<CharacterBody>() : null;
                    effectData.scale = GetExplosionRadius(self.blastRadius, attackerBody);
                }
            }
        }

        static void MeteorStormController_DoMeteorEffect_ReplaceTravelEffectRadius(ILContext il)
        {

            ILCursor c = new ILCursor(il);

            if (!c.TryGotoNext(MoveType.After,
                               x => x.MatchLdfld<MeteorStormController>(nameof(MeteorStormController.travelEffectPrefab))))
            {
                Log.Error("Failed to find travel prefab patch location");
                return;
            }

            static bool isScaledImpact(MeteorStormController self)
            {
                if (!self)
                    return false;

                CharacterBody attackerBody = self.owner ? self.owner.GetComponent<CharacterBody>() : null;

                float scaledRadius = GetExplosionRadius(self.blastRadius, attackerBody);

                return Mathf.Abs((scaledRadius / self.blastRadius) - 1f) > Mathf.Epsilon;
            }

            c.Emit(OpCodes.Ldarg_0);
            c.EmitDelegate<Func<GameObject, MeteorStormController, GameObject>>(getTravelEffectPrefab);

            static GameObject getTravelEffectPrefab(GameObject prefab, MeteorStormController self)
            {
                return isScaledImpact(self) && _meteorTravelEffectScalingFixPrefab ? _meteorTravelEffectScalingFixPrefab : prefab;
            }

            if (!c.TryGotoNext(MoveType.After,
                               x => x.MatchNewobj<EffectData>()))
            {
                Log.Error("Failed to find travel effect patch location");
                return;
            }

            c.Emit(OpCodes.Dup);
            c.Emit(OpCodes.Ldarg_0);
            c.EmitDelegate<Action<EffectData, MeteorStormController>>(populateTravelEffect);

            static void populateTravelEffect(EffectData effectData, MeteorStormController self)
            {
                if (self && isScaledImpact(self))
                {
                    CharacterBody attackerBody = self.owner ? self.owner.GetComponent<CharacterBody>() : null;
                    effectData.scale = GetExplosionRadius(self.blastRadius, attackerBody);
                }
            }
        }

        static void FistSlam_FixedUpdate_ReplaceRadius(ILContext il)
        {
            if (!simpleBlastAttackRadiusManipulator(il, emitGetEntityStateAttackerBody))
                return;

            ILCursor c = new ILCursor(il);

            if (!c.TryFindNext(out ILCursor[] foundCursors,
                               x => x.MatchLdsfld<EntityStates.BrotherMonster.FistSlam>(nameof(EntityStates.BrotherMonster.FistSlam.slamImpactEffect)),
                               x => x.MatchCallOrCallvirt(typeof(EffectManager), nameof(EffectManager.SimpleMuzzleFlash))))
            {
                Log.Error("Failed to find patch location");
                return;
            }

            c.Goto(foundCursors[1].Next, MoveType.After);
            ILLabel afterSpawnEffectLabel = c.MarkLabel();

            c.Goto(foundCursors[0].Next, MoveType.AfterLabel);

            c.Emit(OpCodes.Ldarg_0);
            c.EmitDelegate<Func<EntityStates.BrotherMonster.FistSlam, bool>>(trySpawnScaledEffect);
            c.Emit(OpCodes.Brtrue, afterSpawnEffectLabel);

            static bool trySpawnScaledEffect(EntityStates.BrotherMonster.FistSlam self)
            {
                if (!isScaledExplosion(EntityStates.BrotherMonster.FistSlam.radius, self?.characterBody) || !_brotherFistSlamImpactScaleFixPrefab)
                    return false;

                spawnEffectAtMuzzle(_banditSmokeBombScalingFixPrefab, new EffectData
                {
                    scale = GetExplosionRadius(EntityStates.BrotherMonster.FistSlam.radius, self.characterBody)
                }, self.gameObject, EntityStates.BrotherMonster.FistSlam.muzzleString, false);

                return true;
            }
        }

        static void WeaponSlam_FixedUpdate_ReplaceRadius(ILContext il)
        {
            if (!simpleBlastAttackRadiusManipulator(il, emitGetEntityStateAttackerBody))
                return;

            ILCursor c = new ILCursor(il);

            if (!c.TryFindNext(out ILCursor[] foundCursors,
                               x => x.MatchLdsfld<EntityStates.BrotherMonster.WeaponSlam>(nameof(EntityStates.BrotherMonster.WeaponSlam.slamImpactEffect)),
                               x => x.MatchCallOrCallvirt(typeof(EffectManager), nameof(EffectManager.SimpleMuzzleFlash))))
            {
                Log.Error("Failed to find patch location");
                return;
            }

            c.Goto(foundCursors[1].Next, MoveType.After);
            ILLabel afterSpawnEffectLabel = c.MarkLabel();

            c.Goto(foundCursors[0].Next, MoveType.AfterLabel);

            c.Emit(OpCodes.Ldarg_0);
            c.EmitDelegate<Func<EntityStates.BrotherMonster.WeaponSlam, bool>>(trySpawnScaledEffect);
            c.Emit(OpCodes.Brtrue, afterSpawnEffectLabel);

            static bool trySpawnScaledEffect(EntityStates.BrotherMonster.WeaponSlam self)
            {
                if (!isScaledExplosion(EntityStates.BrotherMonster.WeaponSlam.radius, self?.characterBody) || !_brotherWeaponSlamImpactScaleFixPrefab)
                    return false;

                spawnEffectAtMuzzle(_banditSmokeBombScalingFixPrefab, new EffectData
                {
                    scale = GetExplosionRadius(EntityStates.BrotherMonster.WeaponSlam.radius, self.characterBody)
                }, self.gameObject, EntityStates.BrotherMonster.WeaponSlam.muzzleString, false);

                return true;
            }
        }

        static void CorruptedPathsDash_FixedUpdate_ReplaceRadius(ILContext il)
        {
            const float DefaultBlastRadius = 20f;

            if (!simpleBlastAttackRadiusManipulator(il, emitGetEntityStateAttackerBody))
                return;

            ILCursor c = new ILCursor(il);

            if (!c.TryGotoNext(MoveType.Before,
                               x => x.MatchStfld<EffectData>(nameof(EffectData.scale))))
            {
                Log.Error("Failed to find effect scale patch location");
                return;
            }

            c.Emit(OpCodes.Ldarg_0);
            c.EmitDelegate<Func<EntityStates.FalseSonBoss.CorruptedPathsDash, float>>(getScaleMultiplier);
            c.Emit(OpCodes.Mul);

            static float getScaleMultiplier(EntityStates.FalseSonBoss.CorruptedPathsDash corruptedPathsDash)
            {
                if (!corruptedPathsDash?.characterBody)
                    return 1f;

                float blastRadius = GetExplosionRadius(DefaultBlastRadius, corruptedPathsDash.characterBody);
                return blastRadius / DefaultBlastRadius;
            }
        }

        static void FireLaser_OnEnter_ReplaceRadius(ILContext il)
        {
            if (!simpleBlastAttackRadiusManipulator(il, emitGetEntityStateAttackerBody))
                return;

            ILCursor c = new ILCursor(il);

            int effectDataLocalIndex = -1;
            if (!c.TryFindNext(out ILCursor[] foundCursors,
                               x => x.MatchLdsfld<EntityStates.GolemMonster.FireLaser>(nameof(EntityStates.GolemMonster.FireLaser.hitEffectPrefab)),
                               x => x.MatchLdloc(typeof(EffectData), il, out effectDataLocalIndex)))
            {
                Log.Error("Failed to find hit effect scale patch location");
                return;
            }

            c.Goto(foundCursors[0].Next, MoveType.After);
            c.Emit(OpCodes.Ldarg_0);
            c.EmitDelegate<Func<GameObject, EntityStates.GolemMonster.FireLaser, GameObject>>(getHitEffectPrefab);

            static GameObject getHitEffectPrefab(GameObject prefab, EntityStates.GolemMonster.FireLaser self)
            {
                return isScaledExplosion(EntityStates.GolemMonster.FireLaser.blastRadius, self?.characterBody) && _golemLaserImpactScaleFixPrefab ? _golemLaserImpactScaleFixPrefab : prefab;
            }

            c.Goto(foundCursors[1].Next, MoveType.After);
            c.Emit(OpCodes.Ldarg_0);
            c.EmitDelegate<Func<EffectData, EntityStates.GolemMonster.FireLaser, EffectData>>(getHitEffectData);

            static EffectData getHitEffectData(EffectData effectData, EntityStates.GolemMonster.FireLaser self)
            {
                if (isScaledExplosion(EntityStates.GolemMonster.FireLaser.blastRadius, self?.characterBody))
                {
                    effectData = effectData.Clone();
                    effectData.scale = GetExplosionRadius(EntityStates.GolemMonster.FireLaser.blastRadius, self.characterBody);
                }

                return effectData;
            }
        }

        static void TriLaser_FireTriLaser_ReplaceRadius(ILContext il)
        {
            if (!simpleBlastAttackRadiusManipulator(il, emitGetEntityStateAttackerBody))
                return;

            ILCursor c = new ILCursor(il);

            int effectDataLocalIndex = -1;
            if (!c.TryFindNext(out ILCursor[] foundCursors,
                               x => x.MatchLdsfld<EntityStates.Halcyonite.TriLaser>(nameof(EntityStates.Halcyonite.TriLaser.hitEffectPrefab)),
                               x => x.MatchLdloc(typeof(EffectData), il, out effectDataLocalIndex)))
            {
                Log.Error("Failed to find hit effect scale patch location");
                return;
            }

            c.Goto(foundCursors[0].Next, MoveType.After);
            c.Emit(OpCodes.Ldarg_0);
            c.EmitDelegate<Func<GameObject, EntityStates.Halcyonite.TriLaser, GameObject>>(getHitEffectPrefab);

            static GameObject getHitEffectPrefab(GameObject prefab, EntityStates.Halcyonite.TriLaser self)
            {
                return isScaledExplosion(EntityStates.Halcyonite.TriLaser.blastRadius, self?.characterBody) && _halcyoniteTriLaserImpactScaleFixPrefab ? _halcyoniteTriLaserImpactScaleFixPrefab : prefab;
            }

            c.Goto(foundCursors[1].Next, MoveType.After);
            c.Emit(OpCodes.Ldarg_0);
            c.EmitDelegate<Func<EffectData, EntityStates.Halcyonite.TriLaser, EffectData>>(getHitEffectData);

            static EffectData getHitEffectData(EffectData effectData, EntityStates.Halcyonite.TriLaser self)
            {
                if (isScaledExplosion(EntityStates.Halcyonite.TriLaser.blastRadius, self?.characterBody))
                {
                    effectData = effectData.Clone();
                    effectData.scale = GetExplosionRadius(EntityStates.Halcyonite.TriLaser.blastRadius, self.characterBody);
                }

                return effectData;
            }
        }

        static void ImpBoss_BlinkState_CreateBlinkEffect_ReplaceRadius(ILContext il)
        {
            ILCursor c = new ILCursor(il);

            if (c.TryGotoNext(MoveType.After,
                              x => x.MatchCallOrCallvirt<EffectData>("set_" + nameof(EffectData.origin))))
            {
                c.Emit(OpCodes.Ldarg_0);
                c.EmitDelegate<Action<EntityStates.ImpBossMonster.BlinkState>>(trySetEffectDataScale);

                static void trySetEffectDataScale(EntityStates.ImpBossMonster.BlinkState self)
                {
                    if (self == null || self._effectData == null)
                        return;

                    self._effectData.scale = GetExplosionRadius(self.blastAttackRadius, self.characterBody);
                }
            }
            else
            {
                Log.Error("Failed to find blink effect scale set patch location");
            }

            if (c.TryGotoNext(MoveType.After,
                              x => x.MatchLdfld<EntityStates.ImpBossMonster.BlinkState>(nameof(EntityStates.ImpBossMonster.BlinkState.blinkPrefab))))
            {
                c.Emit(OpCodes.Ldarg_0);
                c.EmitDelegate<Func<GameObject, EntityStates.ImpBossMonster.BlinkState, GameObject>>(getBlinkPrefab);

                static GameObject getBlinkPrefab(GameObject blinkPrefab, EntityStates.ImpBossMonster.BlinkState self)
                {
                    return self != null && isScaledExplosion(self.blastAttackRadius, self.characterBody) && _impBossBlinkScaleFixPrefab ? _impBossBlinkScaleFixPrefab : blinkPrefab;
                }
            }
            else
            {
                Log.Error("Failed to find blink effect prefab patch location");
            }
        }

        static void ImpBoss_BlinkState_FixedUpdate_SetBlinkDestinationEffectOwner(ILContext il)
        {
            ILCursor c = new ILCursor(il);

            ILLabel afterInstantiateBlinkDestinationInstanceLabel = null;
            if (!c.TryGotoNext(MoveType.Before,
                               x => x.MatchLdarg(0),
                               x => x.MatchLdfld<EntityStates.ImpBossMonster.BlinkState>(nameof(EntityStates.ImpBossMonster.BlinkState.blinkDestinationPrefab)),
                               x => x.MatchImplicitConversion<UnityEngine.Object, bool>(),
                               x => x.MatchBrfalse(out afterInstantiateBlinkDestinationInstanceLabel)))
            {
                Log.Error("Failed to find patch location");
                return;
            }

            c.Goto(afterInstantiateBlinkDestinationInstanceLabel.Target, MoveType.Before);

            c.Emit(OpCodes.Ldarg_0);
            c.EmitDelegate<Action<EntityStates.ImpBossMonster.BlinkState>>(setBlinkDestinationEffectOwner);

            static void setBlinkDestinationEffectOwner(EntityStates.ImpBossMonster.BlinkState self)
            {
                if (self?.blinkDestinationInstance && self.blinkDestinationInstance.TryGetComponent(out LocalEffectOwnership ownership))
                {
                    CharacterBody ownerBody = entityStateGetAttackerBody(self);
                    ownership.OwnerObject = ownerBody ? ownerBody.gameObject : self.gameObject;
                }
            }
        }

        static void ImpBoss_GroundPound_FixedUpdate_ReplaceEffectRadius(ILContext il)
        {
            ILCursor c = new ILCursor(il);

            if (!c.TryFindNext(out ILCursor[] foundCursors,
                               x => x.MatchLdsfld<EntityStates.ImpBossMonster.GroundPound>(nameof(EntityStates.ImpBossMonster.GroundPound.slamEffectPrefab)),
                               x => x.MatchCallOrCallvirt(typeof(EffectManager), nameof(EffectManager.SimpleMuzzleFlash))))
            {
                Log.Error("Failed to find patch location");
                return;
            }

            c.Goto(foundCursors[1].Next, MoveType.After);
            ILLabel afterMuzzleFlashLabel = c.MarkLabel();

            c.Goto(foundCursors[0].Next, MoveType.AfterLabel);

            c.Emit(OpCodes.Ldarg_0);
            c.EmitDelegate<Func<EntityStates.ImpBossMonster.GroundPound, bool>>(trySpawnScaledEffect);
            c.Emit(OpCodes.Brtrue, afterMuzzleFlashLabel);

            static bool trySpawnScaledEffect(EntityStates.ImpBossMonster.GroundPound self)
            {
                if (!isScaledExplosion(EntityStates.ImpBossMonster.GroundPound.blastAttackRadius, self?.characterBody) ||
                    !_impBossGroundPoundSlamScaleFixPrefab)
                {
                    return false;
                }

                spawnEffectAtMuzzle(_impBossGroundPoundSlamScaleFixPrefab, new EffectData
                {
                    scale = GetExplosionRadius(EntityStates.ImpBossMonster.GroundPound.blastAttackRadius, self.characterBody)
                }, self.gameObject, "GroundPoundCenter", true);

                return true;
            }
        }

        static void Mage_FlyUpState_CreateBlinkEffect_ReplaceEffectRadius(ILContext il)
        {
            ILCursor c = new ILCursor(il);

            int effectDataVarIndex = -1;
            if (c.TryGotoNext(MoveType.After,
                              x => x.MatchNewobj<EffectData>(),
                              x => x.MatchStloc(typeof(EffectData), il, out effectDataVarIndex)))
            {
                c.Emit(OpCodes.Ldarg_0);
                c.Emit(OpCodes.Ldloc, effectDataVarIndex);
                c.EmitDelegate<Action<EntityStates.Mage.FlyUpState, EffectData>>(trySetEffectDataScale);

                static void trySetEffectDataScale(EntityStates.Mage.FlyUpState self, EffectData effectData)
                {
                    if (self == null || effectData == null)
                        return;

                    effectData.scale = GetExplosionRadius(EntityStates.Mage.FlyUpState.blastAttackRadius, self.characterBody);
                }
            }
            else
            {
                Log.Error("Failed to find blink effect scale set patch location");
            }

            if (c.TryGotoNext(MoveType.After,
                              x => x.MatchLdsfld<EntityStates.Mage.FlyUpState>(nameof(EntityStates.Mage.FlyUpState.blinkPrefab))))
            {
                c.Emit(OpCodes.Ldarg_0);
                c.EmitDelegate<Func<GameObject, EntityStates.Mage.FlyUpState, GameObject>>(getBlinkPrefab);

                static GameObject getBlinkPrefab(GameObject blinkPrefab, EntityStates.Mage.FlyUpState self)
                {
                    return self != null && isScaledExplosion(EntityStates.Mage.FlyUpState.blastAttackRadius, self.characterBody) && _mageFlyUpBlinkScaleFixPrefab ? _mageFlyUpBlinkScaleFixPrefab : blinkPrefab;
                }
            }
            else
            {
                Log.Error("Failed to find blink effect prefab patch location");
            }
        }

        static void Seeker_Meditate_Update_ReplaceRadius(ILContext il)
        {
            ILCursor c = new ILCursor(il);

            int patchCount = 0;

            while (c.TryGotoNext(MoveType.After,
                                 x => x.MatchLdfld<EntityStates.Seeker.Meditate>(nameof(EntityStates.Seeker.Meditate.blastRadius))))
            {
                emitGetEntityStateAttackerBody(c);
                c.EmitDelegate<Func<float, CharacterBody, float>>(GetExplosionRadius);

                patchCount++;
            }

            if (patchCount == 0)
            {
                Log.Error("Failed to find patch location");
            }
            else
            {
                Log.Debug($"Found {patchCount} patch location(s)");
            }
        }

        static void JunkCube_DeathState_Explode_ReplaceRadius(ILContext il)
        {
            if (!simpleBlastAttackRadiusManipulator(il, emitGetEntityStateAttackerBody))
                return;

            ILCursor c = new ILCursor(il);

            if (!c.TryGotoNext(MoveType.After,
                               x => x.MatchNewobj<EffectData>()))
            {
                Log.Error("Failed to find patch location");
                return;
            }

            c.Emit(OpCodes.Dup);
            c.Emit(OpCodes.Ldarg_0);
            c.EmitDelegate<Action<EffectData, EntityStates.JunkCube.DeathState>>(setEffectScale);
            
            static void setEffectScale(EffectData effectData, EntityStates.JunkCube.DeathState self)
            {
                if (effectData == null || self == null)
                    return;

                effectData.scale = GetExplosionRadius(self.explosionRadius, entityStateGetAttackerBody(self));
            }
        }

        static void AimThrowableBase_OnEnter_ReplaceEndpointRadius(ILContext il)
        {
            ILCursor c = new ILCursor(il);

            int projectileExplosionComponentVarIndex = -1;
            if (!c.TryGotoNext(MoveType.After,
                               x => x.MatchLdloc(out projectileExplosionComponentVarIndex),
                               x => x.MatchLdfld<ProjectileExplosion>(nameof(ProjectileExplosion.blastRadius))))
            {
                Log.Error("Failed to find patch location");
                return;
            }

            emitGetEntityStateAttackerBody(c);
            c.Emit(OpCodes.Ldloc, projectileExplosionComponentVarIndex);
            c.EmitDelegate<Func<float, CharacterBody, ProjectileExplosion, float>>(getImpactExplosionRadius);

            static float getImpactExplosionRadius(float radius, CharacterBody attackerBody, ProjectileExplosion projectileExplosionPrefabComponent)
            {
                if (!projectileExplosionPrefabComponent || !projectileExplosionPrefabComponent.enabled)
                    return radius;

                return GetExplosionRadius(radius, attackerBody);
            }
        }

        static ILContext.Manipulator groupManipulators(params ILContext.Manipulator[] manipulators)
        {
            return il =>
            {
                foreach (ILContext.Manipulator manipulator in manipulators)
                {
                    manipulator(il);
                }
            };
        }

        static CharacterBody entityStateGetAttackerBody(EntityState entityState)
        {
            if (entityState == null)
                return null;

            if (entityState.projectileController)
            {
                GameObject owner = entityState.projectileController.owner;
                if (owner && owner.TryGetComponent(out CharacterBody ownerBody))
                {
                    return ownerBody;
                }
            }
            else if (entityState.TryGetComponent(out NetworkedBodyAttachment networkedBodyAttachment))
            {
                CharacterBody attachedBody = networkedBodyAttachment.attachedBody;
                if (attachedBody)
                {
                    return attachedBody;
                }
            }
            else if (entityState.TryGetComponent(out GenericOwnership genericOwnership))
            {
                GameObject owner = genericOwnership.ownerObject;
                if (owner && owner.TryGetComponent(out CharacterBody ownerBody))
                {
                    return ownerBody;
                }
            }
            else if (entityState.TryGetComponent(out DroneCommandReceiver droneCommandReceiver))
            {
                CharacterBody leaderBody = droneCommandReceiver.leaderBody;
                if (leaderBody)
                {
                    return leaderBody;
                }
            }
            else if (entityState.TryGetComponent(out DestructibleSpawerDynamiteController destructibleSpawerDynamiteController))
            {
                GameObject owner = destructibleSpawerDynamiteController.owner;
                if (owner && owner.TryGetComponent(out CharacterBody ownerBody))
                {
                    return ownerBody;
                }
            }
            else if (entityState.TryGetComponent(out JunkCubeController junkCubeController))
            {
                GameObject owner = junkCubeController.owner;
                if (owner && owner.TryGetComponent(out CharacterBody ownerBody))
                {
                    return ownerBody;
                }
            }

            return entityState.characterBody;
        }

        static void emitGetEntityStateAttackerBody(ILCursor c)
        {
            c.Emit(OpCodes.Ldarg_0);
            c.EmitDelegate<Func<EntityState, CharacterBody>>(entityStateGetAttackerBody);
        }

        static T tryGetAsComponent<T>(Component component) where T : Component
        {
            if (component)
            {
                if (component is T tComponent || component.TryGetComponent(out tComponent))
                    return tComponent;
            }

            return null;
        }

        static void emitGetVehicleSeatPassengerBody(ILCursor c)
        {
            c.Emit(OpCodes.Ldarg_0);
            c.EmitDelegate<Func<MonoBehaviour, CharacterBody>>(getPassenger);
            
            static CharacterBody getPassenger(MonoBehaviour component)
            {
                VehicleSeat vehicleSeat = tryGetAsComponent<VehicleSeat>(component);
                return vehicleSeat ? vehicleSeat.currentPassengerBody : null;
            }
        }

        static void emitGetBodyComponentBody(ILCursor c)
        {
            c.Emit(OpCodes.Ldarg_0);
            c.EmitDelegate<Func<MonoBehaviour, CharacterBody>>(getBody);

            static CharacterBody getBody(MonoBehaviour component)
            {
                return tryGetAsComponent<CharacterBody>(component);
            }
        }

        static void emitGetMethodParameterBody(ILCursor c)
        {
            if (c.Context.Method.TryFindParameter<CharacterBody>(out ParameterDefinition bodyParameter))
            {
                c.Emit(OpCodes.Ldarg, bodyParameter);
            }
            else
            {
                Log.Error($"Failed to find body parameter for {c.Context.Method.FullName}");
                c.Emit(OpCodes.Ldnull);
            }
        }

        static void emitGetMethodParameterDamageInfoAttackerBody(ILCursor c)
        {
            if (c.Context.Method.TryFindParameter<DamageInfo>(out ParameterDefinition damageInfoParameter))
            {
                c.Emit(OpCodes.Ldarg, damageInfoParameter);
                c.EmitDelegate<Func<DamageInfo, CharacterBody>>(getAttackerBody);

                static CharacterBody getAttackerBody(DamageInfo damageInfo)
                {
                    return damageInfo?.attacker ? damageInfo.attacker.GetComponent<CharacterBody>() : null;
                }
            }
            else
            {
                Log.Error($"Failed to find DamageInfo parameter for {c.Context.Method.FullName}");
                c.Emit(OpCodes.Ldnull);
            }
        }

        static void emitGetMethodParameterDamageReportAttackerBody(ILCursor c)
        {
            FieldInfo attackerBodyField = typeof(DamageReport).GetField(nameof(DamageReport.attackerBody), BindingFlags.Public | BindingFlags.Instance);
            if (attackerBodyField == null)
            {
                Log.Warning("Failed to find DamageReport.attackerBody field");
            }

            if (c.Context.Method.TryFindParameter<DamageReport>(out ParameterDefinition damageReportParameter))
            {
                c.Emit(OpCodes.Ldarg, damageReportParameter);
                if (attackerBodyField != null)
                {
                    c.Emit(OpCodes.Ldfld, attackerBodyField);
                }
                else
                {
                    c.EmitDelegate<Func<DamageReport, CharacterBody>>(getAttackerBody);

                    static CharacterBody getAttackerBody(DamageReport damageReport)
                    {
                        return damageReport?.attackerBody;
                    }
                }
            }
            else
            {
                Log.Error($"Failed to find DamageReport parameter for {c.Context.Method.FullName}");
                c.Emit(OpCodes.Ldnull);
            }
        }

        static void emitGetProjectileOwner(ILCursor c)
        {
            c.Emit(OpCodes.Ldarg_0);
            c.EmitDelegate<Func<MonoBehaviour, CharacterBody>>(getBody);

            static CharacterBody getBody(MonoBehaviour component)
            {
                ProjectileController projectileController = tryGetAsComponent<ProjectileController>(component);
                if (!projectileController || !projectileController.owner)
                    return null;

                return projectileController.owner.GetComponent<CharacterBody>();
            }
        }

        static void emitGetVoidRaidCrabLegControllerMainBody(ILCursor c)
        {
            c.Emit(OpCodes.Ldarg_0);
            c.EmitDelegate<Func<MonoBehaviour, CharacterBody>>(getBody);

            static CharacterBody getBody(MonoBehaviour component)
            {
                LegController legController = tryGetAsComponent<LegController>(component);
                return legController ? legController.mainBody : null;
            }
        }

        static void emitGetFissureSlamCracksControllerOwnerBody(ILCursor c)
        {
            c.Emit(OpCodes.Ldarg_0);
            c.EmitDelegate<Func<MonoBehaviour, CharacterBody>>(getBody);

            static CharacterBody getBody(MonoBehaviour component)
            {
                FissureSlamCracksController fissureSlamCracksController = tryGetAsComponent<FissureSlamCracksController>(component);
                GameObject owner = fissureSlamCracksController ? fissureSlamCracksController.owner : null;
                return owner ? owner.GetComponent<CharacterBody>() : null;
            }
        }

        static void emitGetOrbOwnerBody(ILCursor c)
        {
            c.Emit(OpCodes.Ldarg_0);
            c.EmitDelegate<Func<Orb, CharacterBody>>(getBody);

            static CharacterBody getBody(Orb orb)
            {
                switch (orb)
                {
                    case BounceOrb bounceOrb:
                        return bounceOrb.attacker ? bounceOrb.attacker.GetComponent<CharacterBody>() : null;
                    case DamageOrb damageOrb:
                        return damageOrb.attacker ? damageOrb.attacker.GetComponent<CharacterBody>() : null;
                    case DevilOrb devilOrb:
                        return devilOrb.attacker ? devilOrb.attacker.GetComponent<CharacterBody>() : null;
                    case GenericDamageOrb genericDamageOrb:
                        return genericDamageOrb.attacker ? genericDamageOrb.attacker.GetComponent<CharacterBody>() : null;
                    case LightningOrb lightningOrb:
                        return lightningOrb.attacker ? lightningOrb.attacker.GetComponent<CharacterBody>() : null;
                    case LunarDetonatorOrb lunarDetonatorOrb:
                        return lunarDetonatorOrb.attacker ? lunarDetonatorOrb.attacker.GetComponent<CharacterBody>() : null;
                    case VoidLightningOrb voidLightningOrb:
                        return voidLightningOrb.attacker ? voidLightningOrb.attacker.GetComponent<CharacterBody>() : null;
                    default:
                        Log.Error($"Unhandled orb type {orb}");
                        return null;
                }
            }
        }

        static void emitGetMeteorStormControllerOwner(ILCursor c)
        {
            c.Emit(OpCodes.Ldarg_0);
            c.EmitDelegate<Func<MonoBehaviour, CharacterBody>>(getBody);

            static CharacterBody getBody(MonoBehaviour component)
            {
                MeteorStormController meteorStormController = tryGetAsComponent<MeteorStormController>(component);
                GameObject owner = meteorStormController ? meteorStormController.owner : null;
                return owner ? owner.GetComponent<CharacterBody>() : null;
            }
        }

        static ILContext.Manipulator getVisualBlastAttackRadiusManipulator(Action<ILCursor> emitGetAttackerBody, bool strictRadiusMatch = true)
        {
            return il =>
            {
                visualBlastAttackRadiusManipulator(il, emitGetAttackerBody, strictRadiusMatch);
            };
        }

        static ILContext.Manipulator getSimpleBlastAttackRadiusManipulator(Action<ILCursor> emitGetAttackerBody)
        {
            return il =>
            {
                simpleBlastAttackRadiusManipulator(il, emitGetAttackerBody);
            };
        }

        static ILContext.Manipulator getSimpleSphereSearchRadiusManipulator(Action<ILCursor> emitGetAttackerBody)
        {
            return il =>
            {
                simpleSphereSearchRadiusManipulator(il, emitGetAttackerBody);
            };
        }

        static ILContext.Manipulator getSimpleEffectDataScaleManipulator(Action<ILCursor> emitGetAttackerBody)
        {
            return il =>
            {
                simpleEffectDataScaleManipulator(il, emitGetAttackerBody);
            };
        }

        static void visualBlastAttackRadiusManipulator(ILContext il, Action<ILCursor> emitGetAttackerBody, bool strictRadiusMatch = true)
        {
            ILCursor c = new ILCursor(il);

            HashSet<Instruction> patchedEffectDataScaleSetters = new HashSet<Instruction>();

            Instruction loadRadiusValueInstruction = null;

            Func<Instruction, bool>[] setEffectScaleMatch = new Func<Instruction, bool>[]
            {
                x => !strictRadiusMatch || instructionsEqual(x, loadRadiusValueInstruction),
                x => x.MatchStfld<EffectData>(nameof(EffectData.scale)) && !patchedEffectDataScaleSetters.Contains(x)
            };

            int patchCount = 0;
            int effectDataPatchCount = 0;
            while (c.TryGotoNext(MoveType.After,
                                 x => matchLoadValue(x, out loadRadiusValueInstruction),
                                 x => x.MatchStfld<BlastAttack>(nameof(BlastAttack.radius))))
            {
                ILCursor effectDataCursor = c.Clone();

                if (effectDataCursor.TryGotoNext(MoveType.After, setEffectScaleMatch) ||
                    effectDataCursor.TryGotoPrev(MoveType.After, setEffectScaleMatch))
                {
                    // move before set scale
                    effectDataCursor.Index--;

                    patchedEffectDataScaleSetters.Add(effectDataCursor.Next);

                    emitGetAttackerBody(effectDataCursor);
                    effectDataCursor.EmitDelegate<Func<float, CharacterBody, float>>(GetExplosionRadius);

                    effectDataPatchCount++;
                }

                // move before set radius
                c.Index--;

                emitGetAttackerBody(c);
                c.EmitDelegate<Func<float, CharacterBody, float>>(GetExplosionRadius);

                patchCount++;

                c.SearchTarget = SearchTarget.Next;
            }

            if (patchCount == 0 || effectDataPatchCount != patchCount)
            {
                Log.Error($"{il.Method.FullName}: Failed to find valid patch location(s) (found {patchCount} radius location(s), {effectDataPatchCount} effect radius location(s))");
            }
            else
            {
                Log.Debug($"{il.Method.FullName}: Found {patchCount} radius and {effectDataPatchCount} effect radius patch location(s)");
            }
        }

        static bool simpleBlastAttackRadiusManipulator(ILContext il, Action<ILCursor> emitGetAttackerBody)
        {
            ILCursor c = new ILCursor(il);

            int patchCount = 0;
            while (c.TryGotoNext(MoveType.Before,
                                 x => x.MatchStfld<BlastAttack>(nameof(BlastAttack.radius))))
            {
                emitGetAttackerBody(c);
                c.EmitDelegate<Func<float, CharacterBody, float>>(GetExplosionRadius);

                patchCount++;

                c.SearchTarget = SearchTarget.Next;
            }

            if (patchCount == 0)
            {
                Log.Error($"{il.Method.FullName}: Failed to find patch location");
            }
            else
            {
                Log.Debug($"{il.Method.FullName}: Found {patchCount} patch location(s)");
            }

            return patchCount > 0;
        }

        static void simpleSphereSearchRadiusManipulator(ILContext il, Action<ILCursor> emitGetAttackerBody)
        {
            ILCursor c = new ILCursor(il);

            int patchCount = 0;
            while (c.TryGotoNext(MoveType.Before,
                                 x => x.MatchStfld<SphereSearch>(nameof(SphereSearch.radius))))
            {
                emitGetAttackerBody(c);
                c.EmitDelegate<Func<float, CharacterBody, float>>(GetExplosionRadius);

                patchCount++;

                c.SearchTarget = SearchTarget.Next;
            }

            if (patchCount == 0)
            {
                Log.Error($"{il.Method.FullName}: Failed to find patch location");
            }
            else
            {
                Log.Debug($"{il.Method.FullName}: Found {patchCount} patch location(s)");
            }
        }

        static void simpleEffectDataScaleManipulator(ILContext il, Action<ILCursor> emitGetAttackerBody)
        {
            ILCursor c = new ILCursor(il);

            int patchCount = 0;
            while (c.TryGotoNext(MoveType.Before,
                                 x => x.MatchStfld<EffectData>(nameof(EffectData.scale))))
            {
                emitGetAttackerBody(c);
                c.EmitDelegate<Func<float, CharacterBody, float>>(GetExplosionRadius);

                patchCount++;

                c.SearchTarget = SearchTarget.Next;
            }

            if (patchCount == 0)
            {
                Log.Error($"{il.Method.FullName}: Failed to find patch location");
            }
            else
            {
                Log.Debug($"{il.Method.FullName}: Found {patchCount} patch location(s)");
            }
        }
    }
}
