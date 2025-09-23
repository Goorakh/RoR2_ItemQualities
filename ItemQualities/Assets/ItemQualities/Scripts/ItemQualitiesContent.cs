using HG;
using HG.Coroutines;
using ItemQualities.ContentManagement;
using ItemQualities.Utilities.Extensions;
using RoR2;
using RoR2.ContentManagement;
using ShaderSwapper;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;

using Path = System.IO.Path;

namespace ItemQualities
{
    public class ItemQualitiesContent : IContentPackProvider
    {
        readonly ContentPack _contentPack = new ContentPack();

        public string identifier => ItemQualitiesPlugin.PluginGUID;

        internal static NamedAssetCollection<TMP_SpriteAsset> TMP_SpriteAssets = new NamedAssetCollection<TMP_SpriteAsset>(ContentPack.getScriptableObjectName);

        internal ItemQualitiesContent()
        {
        }

        internal void Register()
        {
            ContentManager.collectContentPackProviders += collectContentPackProviders;
        }

        void collectContentPackProviders(ContentManager.AddContentPackProviderDelegate addContentPackProvider)
        {
            addContentPackProvider(this);
        }

        public IEnumerator LoadStaticContentAsync(LoadStaticContentAsyncArgs args)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();

            string assetBundleLocation = Path.Combine(Path.GetDirectoryName(ItemQualitiesPlugin.Instance.Info.Location), "itemqualitiesassets");
            if (!File.Exists(assetBundleLocation))
            {
                throw new FileNotFoundException("Could not find ItemQualities assetbundle file");
            }

            using PartitionedProgress partitionedProgress = new PartitionedProgress(args.progressReceiver);
            IProgress<float> loadAssetBundleProgress = partitionedProgress.AddPartition(1f);
            IProgress<float> loadAssetsProgress = partitionedProgress.AddPartition(1f);
            IProgress<float> generateAssetsProgress = partitionedProgress.AddPartition(1f);
            IProgress<float> finalizeContentProgress = partitionedProgress.AddPartition(1f);

            AssetBundleCreateRequest assetBundleLoad = AssetBundle.LoadFromFileAsync(assetBundleLocation);
            yield return assetBundleLoad.AsProgressCoroutine(loadAssetBundleProgress);

            Log.Debug($"Loaded asset bundle in {stopwatch.Elapsed.TotalMilliseconds:F0}ms (at {assetBundleLocation})");
            stopwatch.Restart();

            AssetBundle assetBundle = assetBundleLoad.assetBundle;

            yield return assetBundle.UpgradeStubbedShadersAsync();

            AssetBundleRequest allAssetsLoad = assetBundle.LoadAllAssetsAsync();
            yield return allAssetsLoad.AsProgressCoroutine(loadAssetsProgress);

            UnityEngine.Object[] assetBundleAssets = allAssetsLoad.allAssets;

            List<UnityEngine.Object> generatedAssets = new List<UnityEngine.Object>();

            ParallelProgressCoroutine generateAssetsCoroutine = new ParallelProgressCoroutine(generateAssetsProgress);
            foreach (UnityEngine.Object asset in assetBundleAssets)
            {
                if (asset is IAsyncAssetGenerator asyncAssetGenerator)
                {
                    ReadableProgress<float> generateProgress = new ReadableProgress<float>();
                    generateAssetsCoroutine.Add(asyncAssetGenerator.GenerateAssetsAsync(generatedAssets, generateProgress), generateProgress);
                }

                if (asset is GameObject gameObject)
                {
                    foreach (IAsyncAssetGenerator asyncAssetGeneratorComponent in gameObject.GetComponentsInChildren<IAsyncAssetGenerator>(true))
                    {
                        ReadableProgress<float> generateProgress = new ReadableProgress<float>();
                        generateAssetsCoroutine.Add(asyncAssetGeneratorComponent.GenerateAssetsAsync(generatedAssets, generateProgress), generateProgress);
                    }
                }
            }

            yield return generateAssetsCoroutine;

            List<UnityEngine.Object> finalAssets = new List<UnityEngine.Object>(assetBundleAssets.Length + generatedAssets.Count);
            finalAssets.AddRange(assetBundleAssets);
            finalAssets.AddRange(generatedAssets);

            ParallelProgressCoroutine finalizeContentCoroutine = new ParallelProgressCoroutine(finalizeContentProgress);

            ReadableProgress<float> contentLoadCallbacksProgress = new ReadableProgress<float>();
            finalizeContentCoroutine.Add(runContentLoadCallbacks(finalAssets, contentLoadCallbacksProgress), contentLoadCallbacksProgress);

            yield return finalizeContentCoroutine;

            List<GameObject> networkedPrefabsList = new List<GameObject>();
            List<GameObject> prefabsList = new List<GameObject>();

            foreach (GameObject prefab in finalAssets.OfType<GameObject>())
            {
                List<GameObject> prefabList = prefabsList;
                if (prefab.GetComponent<NetworkBehaviour>())
                {
                    prefabList = networkedPrefabsList;
                }

                prefabList.Add(prefab);
            }

            NamedAssetCollection<GameObject> prefabs = new NamedAssetCollection<GameObject>(ContentPack.getGameObjectName);
            prefabs.Add(prefabsList.ToArray());

            NamedAssetCollection<QualityTierDef> qualityTierDefs = new NamedAssetCollection<QualityTierDef>(ContentPack.getScriptableObjectName);
            qualityTierDefs.Add(finalAssets.OfType<QualityTierDef>().ToArray());

            NamedAssetCollection<ItemQualityGroup> itemQualityGroups = new NamedAssetCollection<ItemQualityGroup>(ContentPack.getScriptableObjectName);
            itemQualityGroups.Add(finalAssets.OfType<ItemQualityGroup>().ToArray());

            NamedAssetCollection<EquipmentQualityGroup> equipmentQualityGroups = new NamedAssetCollection<EquipmentQualityGroup>(ContentPack.getScriptableObjectName);
            equipmentQualityGroups.Add(finalAssets.OfType<EquipmentQualityGroup>().ToArray());

            _contentPack.itemDefs.Add(finalAssets.OfType<ItemDef>().ToArray());
            _contentPack.itemTierDefs.Add(finalAssets.OfType<ItemTierDef>().ToArray());

            _contentPack.equipmentDefs.Add(finalAssets.OfType<EquipmentDef>().ToArray());

            _contentPack.networkedObjectPrefabs.Add(networkedPrefabsList.ToArray());

            populateTypeFields(typeof(QualityTiers), qualityTierDefs, fieldName => "qd" + fieldName);
            QualityTiers.AllQualityTiers = new ReadOnlyCollection<QualityTierDef>(qualityTierDefs.ToArray());

            populateTypeFields(typeof(ItemQualityGroups), itemQualityGroups, fieldName => "ig" + fieldName);
            ItemQualityGroups.AllGroups = new ReadOnlyCollection<ItemQualityGroup>(itemQualityGroups.ToArray());

            populateTypeFields(typeof(EquipmentQualityGroups), equipmentQualityGroups, fieldName => "eg" + fieldName);
            EquipmentQualityGroups.AllGroups = new ReadOnlyCollection<EquipmentQualityGroup>(equipmentQualityGroups.ToArray());

            populateTypeFields(typeof(Prefabs), prefabs);

            TMP_SpriteAssets.Add(finalAssets.OfType<TMP_SpriteAsset>().ToArray());

            Log.Debug($"Loaded asset bundle contents in {stopwatch.Elapsed.TotalMilliseconds:F0}ms (at {assetBundleLocation})");
        }

        static IEnumerator runContentLoadCallbacks(IEnumerable<UnityEngine.Object> assets, IProgress<float> progressReceiver)
        {
            ParallelProgressCoroutine callbackParallelCoroutine = new ParallelProgressCoroutine(progressReceiver);

            foreach (UnityEngine.Object asset in assets)
            {
                if (asset is GameObject gameObject)
                {
                    foreach (IAsyncContentLoadCallback asyncContentLoadCallback in gameObject.GetComponentsInChildren<IAsyncContentLoadCallback>(true))
                    {
                        ReadableProgress<float> callbackProgress = new ReadableProgress<float>();
                        callbackParallelCoroutine.Add(asyncContentLoadCallback.OnContentLoad(callbackProgress), callbackProgress);
                    }

                    foreach (IContentLoadCallback contentLoadCallback in gameObject.GetComponentsInChildren<IContentLoadCallback>(true))
                    {
                        contentLoadCallback.OnContentLoad();
                    }
                }
                else
                {
                    if (asset is IAsyncContentLoadCallback asyncContentLoadCallback)
                    {
                        ReadableProgress<float> callbackProgress = new ReadableProgress<float>();
                        callbackParallelCoroutine.Add(asyncContentLoadCallback.OnContentLoad(callbackProgress), callbackProgress);
                    }

                    if (asset is IContentLoadCallback contentLoadCallback)
                    {
                        contentLoadCallback.OnContentLoad();
                    }
                }
            }

            return callbackParallelCoroutine;
        }

        public IEnumerator GenerateContentPackAsync(GetContentPackAsyncArgs args)
        {
            ContentPack.Copy(_contentPack, args.output);
            args.ReportProgress(1f);
            yield break;
        }

        public IEnumerator FinalizeAsync(FinalizeAsyncArgs args)
        {
            ContentManager.collectContentPackProviders -= collectContentPackProviders;
            args.ReportProgress(1f);
            yield break;
        }

        static void populateTypeFields<TAsset>(Type typeToPopulate, NamedAssetCollection<TAsset> assets, Func<string, string> fieldNameToAssetNameConverter = null)
        {
            foreach (FieldInfo fieldInfo in typeToPopulate.GetFields(BindingFlags.Static | BindingFlags.Public))
            {
                if (!typeof(TAsset).IsAssignableFrom(fieldInfo.FieldType))
                    continue;

                TargetAssetNameAttribute customAttribute = fieldInfo.GetCustomAttribute<TargetAssetNameAttribute>();
                string assetName;
                if (customAttribute != null)
                {
                    assetName = customAttribute.targetAssetName;
                }
                else if (fieldNameToAssetNameConverter != null)
                {
                    assetName = fieldNameToAssetNameConverter(fieldInfo.Name);
                }
                else
                {
                    assetName = fieldInfo.Name;
                }

                TAsset tasset = assets.Find(assetName);
                if (tasset != null)
                {
                    fieldInfo.SetValue(null, tasset);
                }
                else
                {
                    Log.Warning($"Failed to assign {fieldInfo.DeclaringType.Name}.{fieldInfo.Name}: Asset \"{assetName}\" not found");
                }
            }
        }

        public static class QualityTiers
        {
            internal static IReadOnlyCollection<QualityTierDef> AllQualityTiers = Array.Empty<QualityTierDef>();

            public static QualityTierDef Uncommon;
            public static QualityTierDef Rare;
            public static QualityTierDef Epic;
            public static QualityTierDef Legendary;
        }

        public static class ItemQualityGroups
        {
            internal static IReadOnlyCollection<ItemQualityGroup> AllGroups = Array.Empty<ItemQualityGroup>();

            public static ItemQualityGroup Hoof;

            public static ItemQualityGroup CritGlasses;

            public static ItemQualityGroup SprintBonus;

            public static ItemQualityGroup Syringe;

            public static ItemQualityGroup HealWhileSafe;

            public static ItemQualityGroup Crowbar;

            public static ItemQualityGroup PersonalShield;

            public static ItemQualityGroup BarrierOnKill;

            public static ItemQualityGroup ExtraShrineItem;

            public static ItemQualityGroup Dagger;

            public static ItemQualityGroup FragileDamageBonus;

            public static ItemQualityGroup FragileDamageBonusConsumed;
        }

        public static class EquipmentQualityGroups
        {
            internal static IReadOnlyCollection<EquipmentQualityGroup> AllGroups = Array.Empty<EquipmentQualityGroup>();

            public static EquipmentQualityGroup BossHunterConsumed;
        }

        public static class Prefabs
        {
            public static GameObject QualityPickupDisplay;
        }
    }
}
