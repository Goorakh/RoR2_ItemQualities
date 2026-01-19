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

        [ContentInitializer]
        static IEnumerator LoadContent(ContentIntializerArgs args)
        {
            ParallelProgressCoroutine coroutine = new ParallelProgressCoroutine(args.ProgressReceiver);

            static IEnumerator banditSmokeBombScaleFixAsync(IProgress<float> progressReceiver)
            {
                AsyncOperationHandle<GameObject> smokeBombPrefabLoad = AddressableUtil.LoadTempAssetAsync<GameObject>(RoR2BepInExPack.GameAssetPaths.Version_1_39_0.RoR2_Base_Bandit2.Bandit2SmokeBomb_prefab);
                AsyncOperationHandle<EntityStateConfiguration> stealthModeConfigurationLoad = AddressableUtil.LoadTempAssetAsync<EntityStateConfiguration>(RoR2BepInExPack.GameAssetPaths.Version_1_39_0.RoR2_Base_Bandit2.EntityStates_Bandit2_StealthMode_asset);

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
                AsyncOperationHandle<GameObject> impactEffectLoad = AddressableUtil.LoadTempAssetAsync<GameObject>(RoR2BepInExPack.GameAssetPaths.Version_1_39_0.RoR2_Base_Lightning.LightningStrikeImpact_prefab);

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
                AsyncOperationHandle<GameObject> impactEffectLoad = AddressableUtil.LoadTempAssetAsync<GameObject>(RoR2BepInExPack.GameAssetPaths.Version_1_39_0.RoR2_Base_LightningStrikeOnHit.SimpleLightningStrikeImpact_prefab);

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
                AsyncOperationHandle<GameObject> meteorStormLoad = AddressableUtil.LoadTempAssetAsync<GameObject>(RoR2BepInExPack.GameAssetPaths.Version_1_39_0.RoR2_Base_Meteor.MeteorStorm_prefab);

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
                AsyncOperationHandle<EntityStateConfiguration> brotherFistSlamConfigurationLoad = AddressableUtil.LoadTempAssetAsync<EntityStateConfiguration>(RoR2BepInExPack.GameAssetPaths.Version_1_39_0.RoR2_Base_Brother.EntityStates_BrotherMonster_FistSlam_asset);

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
                AsyncOperationHandle<EntityStateConfiguration> brotherWeaponSlamConfigurationLoad = AddressableUtil.LoadTempAssetAsync<EntityStateConfiguration>(RoR2BepInExPack.GameAssetPaths.Version_1_39_0.RoR2_Base_Brother.EntityStates_BrotherMonster_WeaponSlam_asset);

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
                AssetReferenceT<AnimationClip> falseSonBossPrimarySlamClipReference = new AssetReferenceT<AnimationClip>(RoR2BepInExPack.GameAssetPaths.Version_1_39_0.RoR2_DLC2_FalseSon.AS_FalseSon_PrimarySlam_fbx_FSArmature_BossPrimarySlam_);
                AsyncOperationHandle<AnimationClip> falseSonBossPrimarySlamClipLoad = AssetAsyncReferenceManager<AnimationClip>.LoadAsset(falseSonBossPrimarySlamClipReference);
                AsyncOperationHandle<EntityStateConfiguration> falseSonFissureSlamConfigurationLoad = AddressableUtil.LoadTempAssetAsync<EntityStateConfiguration>(RoR2BepInExPack.GameAssetPaths.Version_1_39_0.RoR2_DLC2_FalseSonBoss.EntityStates_FalseSonBoss_FissureSlam_asset);

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
                AssetReferenceT<AnimationClip> falseSonBossPrimeDevastatorClipReference = new AssetReferenceT<AnimationClip>(RoR2BepInExPack.GameAssetPaths.Version_1_39_0.RoR2_DLC2_FalseSon.AS_FalseSonBoss_PrimeDevastator_fbx_FSArmature_BossPrimaryDevastator_);
                AsyncOperationHandle<AnimationClip> falseSonBossPrimeDevastatorClipLoad = AssetAsyncReferenceManager<AnimationClip>.LoadAsset(falseSonBossPrimeDevastatorClipReference);
                AsyncOperationHandle<EntityStateConfiguration> falseSonPrimeDevastatorConfigurationLoad = AddressableUtil.LoadTempAssetAsync<EntityStateConfiguration>(RoR2BepInExPack.GameAssetPaths.Version_1_39_0.RoR2_DLC2_FalseSonBoss.EntityStates_FalseSonBoss_PrimeDevastator_asset);

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
                AssetReferenceT<AnimationClip> golemClapClipReference = new AssetReferenceT<AnimationClip>(RoR2BepInExPack.GameAssetPaths.Version_1_39_0.RoR2_Base_Golem.mdlGolem_fbx_GolemArmature_Smack_);
                AsyncOperationHandle<AnimationClip> golemClapClipLoad = AssetAsyncReferenceManager<AnimationClip>.LoadAsset(golemClapClipReference);
                AsyncOperationHandle<EntityStateConfiguration> golemClapConfigurationLoad = AddressableUtil.LoadTempAssetAsync<EntityStateConfiguration>(RoR2BepInExPack.GameAssetPaths.Version_1_39_0.RoR2_Base_Golem.EntityStates_GolemMonster_ClapState_asset);

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

            return coroutine;
        }

        [SystemInitializer(typeof(EffectCatalogUtils))]
        static void Init()
        {
            static void enableEffectScale(string effectName)
            {
                EffectIndex effectIndex = EffectCatalogUtils.FindEffectIndex(effectName);
                if (effectIndex == EffectIndex.Invalid)
                {
                    Log.Error($"Failed to find effect '{effectName}'");
                    return;
                }

                EffectComponent effectComponent = EffectCatalog.GetEffectDef(effectIndex)?.prefabEffectComponent;
                if (effectComponent)
                {
                    effectComponent.applyScale = true;
                }
            }

            enableEffectScale("DetonateChargeVFX");
            enableEffectScale("DetonateVFX");

            enableEffectScale("DrifterJunkCubeExplosionVFX");

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

            beaconImpactIndicatorScaler(RoR2BepInExPack.GameAssetPaths.Version_1_39_0.RoR2_Base_Captain.CaptainSupplyDrop__Base_prefab);
            beaconImpactIndicatorScaler(RoR2BepInExPack.GameAssetPaths.Version_1_39_0.RoR2_Base_Captain.CaptainSupplyDrop__EquipmentRestock_prefab);
            beaconImpactIndicatorScaler(RoR2BepInExPack.GameAssetPaths.Version_1_39_0.RoR2_Base_Captain.CaptainSupplyDrop__Hacking_prefab);
            beaconImpactIndicatorScaler(RoR2BepInExPack.GameAssetPaths.Version_1_39_0.RoR2_Base_Captain.CaptainSupplyDrop__Healing_prefab);
            beaconImpactIndicatorScaler(RoR2BepInExPack.GameAssetPaths.Version_1_39_0.RoR2_Base_Captain.CaptainSupplyDrop__Plating_prefab);
            beaconImpactIndicatorScaler(RoR2BepInExPack.GameAssetPaths.Version_1_39_0.RoR2_Base_Captain.CaptainSupplyDrop__Shocking_prefab);

            AddressableUtil.LoadAssetAsync<GameObject>(RoR2BepInExPack.GameAssetPaths.Version_1_39_0.RoR2_DLC2_FalseSon.FalseSonMeridiansWillIndicator_prefab).OnSuccess(meridiansWillIndicator =>
            {
                meridiansWillIndicator.EnsureComponent<GenericOwnership>();

                ExplosionRangeIndicatorScaler explosionRangeIndicatorScaler = meridiansWillIndicator.EnsureComponent<ExplosionRangeIndicatorScaler>();
                explosionRangeIndicatorScaler.ExplosionInfoIndex = ExplosionInfoIndex.MeridiansWill;
                explosionRangeIndicatorScaler.IndicatorTransforms = new Transform[] { meridiansWillIndicator.transform };
            });

            IL.EntityStates.Chef.RolyPoly.GearShift += getVisualBlastAttackRadiusManipulator(emitGetEntityStateAttackerBody, false);

            IL.EntityStates.Chef.YesChef.OnEnter += getSimpleEffectDataScaleManipulator(emitGetEntityStateAttackerBody);
            IL.EntityStates.Chef.YesChef.FixedUpdate += groupManipulators(getSimpleBlastAttackRadiusManipulator(emitGetEntityStateAttackerBody), getSimpleSphereSearchRadiusManipulator(emitGetEntityStateAttackerBody));

            IL.EntityStates.DefectiveUnit.Detonate.OnEnter += getUnscaledEffectDataScaleManipulator(emitGetEntityStateAttackerBody);
            IL.EntityStates.DefectiveUnit.Detonate.FixedUpdate += groupManipulators(getSimpleBlastAttackRadiusManipulator(emitGetEntityStateAttackerBody), getUnscaledEffectDataScaleManipulator(emitGetEntityStateAttackerBody));

            IL.EntityStates.Drone.DroneBombardment.BombardmentDroneProjectileEffect.ExecuteRadialAttack += groupManipulators(getSimpleSphereSearchRadiusManipulator(emitGetEntityStateAttackerBody), getSimpleEffectDataScaleManipulator(emitGetEntityStateAttackerBody));
            IL.EntityStates.Drone.DroneBombardment.BombardmentDroneSkill.SpawnBombardmentRays += groupManipulators(getSimpleSphereSearchRadiusManipulator(emitGetEntityStateAttackerBody), getSimpleEffectDataScaleManipulator(emitGetEntityStateAttackerBody));

            IL.EntityStates.JellyfishMonster.JellyNova.OnEnter += JellyNova_ReplaceNovaRadius;

            IL.EntityStates.JunkCube.DeathState.Explode += groupManipulators(getSimpleBlastAttackRadiusManipulator(emitGetEntityStateAttackerBody), getUnscaledEffectDataScaleManipulator(emitGetEntityStateAttackerBody));

            IL.EntityStates.Mage.FlyUpState.OnEnter += getSimpleBlastAttackRadiusManipulator(emitGetEntityStateAttackerBody);

            IL.EntityStates.Seeker.Meditate.Update += groupManipulators(getSimpleBlastAttackRadiusManipulator(emitGetEntityStateAttackerBody), getUnscaledEffectDataScaleManipulator(emitGetEntityStateAttackerBody));

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

                CharacterBody body = stealthMode?.characterBody;
                if (!body)
                    return false;

                float finalRadius = GetExplosionRadius(EntityStates.Bandit2.StealthMode.blastAttackRadius, body);
                if (Mathf.Abs(finalRadius - EntityStates.Bandit2.StealthMode.blastAttackRadius) < 0.01f)
                    return false;

                ModelLocator modelLocator = stealthMode.modelLocator;
                if (!modelLocator || !modelLocator.modelChildLocator)
                    return false;

                int smokeBombMuzzleIndex = modelLocator.modelChildLocator.FindChildIndex(EntityStates.Bandit2.StealthMode.smokeBombMuzzleString);
                Transform smokeBombMuzzle = modelLocator.modelChildLocator.FindChild(smokeBombMuzzleIndex);
                if (!smokeBombMuzzle)
                    return false;

                EffectData effectData = new EffectData
                {
                    origin = smokeBombMuzzle.position,
                    scale = finalRadius
                };

                effectData.SetChildLocatorTransformReference(stealthMode.gameObject, smokeBombMuzzleIndex);

                EffectManager.SpawnEffect(_banditSmokeBombScalingFixPrefab, effectData, false);

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

                ChildLocator childLocator = self.GetModelChildLocator();
                if (!childLocator)
                    return false;

                int explosionMuzzleIndex = childLocator.FindChildIndex(EntityStates.BrotherMonster.FistSlam.muzzleString);
                Transform explosionMuzzle = explosionMuzzleIndex != -1 ? childLocator.FindChild(explosionMuzzleIndex) : null;
                if (!explosionMuzzle)
                    return false;

                EffectData effectData = new EffectData
                {
                    origin = explosionMuzzle.position,
                    scale = GetExplosionRadius(EntityStates.BrotherMonster.FistSlam.radius, self.characterBody)
                };

                effectData.SetChildLocatorTransformReference(self.gameObject, explosionMuzzleIndex);

                EffectManager.SpawnEffect(_brotherFistSlamImpactScaleFixPrefab, effectData, false);
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

                ChildLocator childLocator = self.GetModelChildLocator();
                if (!childLocator)
                    return false;

                int explosionMuzzleIndex = childLocator.FindChildIndex(EntityStates.BrotherMonster.WeaponSlam.muzzleString);
                Transform explosionMuzzle = explosionMuzzleIndex != -1 ? childLocator.FindChild(explosionMuzzleIndex) : null;
                if (!explosionMuzzle)
                    return false;

                EffectData effectData = new EffectData
                {
                    origin = explosionMuzzle.position,
                    scale = GetExplosionRadius(EntityStates.BrotherMonster.WeaponSlam.radius, self.characterBody)
                };

                effectData.SetChildLocatorTransformReference(self.gameObject, explosionMuzzleIndex);

                EffectManager.SpawnEffect(_brotherWeaponSlamImpactScaleFixPrefab, effectData, false);
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

        static ILContext.Manipulator getUnscaledEffectDataScaleManipulator(Action<ILCursor> emitGetAttackerBody)
        {
            return il =>
            {
                unscaledEffectDataScaleManipulator(il, emitGetAttackerBody);
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

        static void unscaledEffectDataScaleManipulator(ILContext il, Action<ILCursor> emitGetAttackerBody)
        {
            FieldInfo effectDataScaleField = typeof(EffectData).GetField(nameof(EffectData.scale), BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly);
            if (effectDataScaleField == null)
            {
                Log.Error("Failed to find EffectData.scale field");
                return;
            }

            ILCursor c = new ILCursor(il);

            int patchCount = 0;
            while (c.TryGotoNext(MoveType.After,
                                 x => x.MatchNewobj<EffectData>()))
            {
                c.Emit(OpCodes.Dup);
                c.Emit(OpCodes.Dup);
                c.Emit(OpCodes.Ldfld, effectDataScaleField);
                emitGetAttackerBody(c);
                c.EmitDelegate<Func<float, CharacterBody, float>>(GetExplosionRadius);
                c.Emit(OpCodes.Stfld, effectDataScaleField);

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
