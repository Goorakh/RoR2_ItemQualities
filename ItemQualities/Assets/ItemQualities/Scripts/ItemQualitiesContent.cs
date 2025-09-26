using HG;
using HG.Coroutines;
using ItemQualities.ContentManagement;
using ItemQualities.Utilities.Extensions;
using RoR2;
using RoR2.ContentManagement;
using RoR2.Projectile;
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

            List<GameObject> projectilePrefabsList = new List<GameObject>();
            List<GameObject> networkedPrefabsList = new List<GameObject>();
            List<GameObject> prefabsList = new List<GameObject>();

            List<QualityTierDef> qualityTierDefsList = new List<QualityTierDef>();

            List<ItemQualityGroup> itemQualityGroupsList = new List<ItemQualityGroup>();
            List<EquipmentQualityGroup> equipmentQualityGroupsList = new List<EquipmentQualityGroup>();

            List<ItemDef> itemDefsList = new List<ItemDef>();
            List<ItemTierDef> itemTierDefsList = new List<ItemTierDef>();

            List<EquipmentDef> equipmentDefsList = new List<EquipmentDef>();

            List<TMP_SpriteAsset> spriteAssetsList = new List<TMP_SpriteAsset>();

            foreach (UnityEngine.Object obj in finalAssets)
            {
                switch (obj)
                {
                    case GameObject prefab:
                        List<GameObject> prefabList = prefabsList;
                        if (prefab.GetComponent<ProjectileController>())
                        {
                            prefabList = projectilePrefabsList;
                        }
                        else if (prefab.GetComponent<NetworkBehaviour>())
                        {
                            prefabList = networkedPrefabsList;
                        }

                        prefabList.Add(prefab);

                        break;
                    case QualityTierDef qualityTierDef:
                        qualityTierDefsList.Add(qualityTierDef);
                        break;
                    case ItemQualityGroup itemQualityGroup:
                        itemQualityGroupsList.Add(itemQualityGroup);
                        break;
                    case EquipmentQualityGroup equipmentQualityGroup:
                        equipmentQualityGroupsList.Add(equipmentQualityGroup);
                        break;
                    case ItemDef itemDef:
                        itemDefsList.Add(itemDef);
                        break;
                    case ItemTierDef itemTierDef:
                        itemTierDefsList.Add(itemTierDef);
                        break;
                    case EquipmentDef equipmentDef:
                        equipmentDefsList.Add(equipmentDef);
                        break;
                    case TMP_SpriteAsset spriteAsset:
                        spriteAssetsList.Add(spriteAsset);
                        break;
                }
            }

            NamedAssetCollection<GameObject> prefabs = new NamedAssetCollection<GameObject>(ContentPack.getGameObjectName);
            prefabs.Add(prefabsList.ToArray());

            NamedAssetCollection<QualityTierDef> qualityTierDefs = new NamedAssetCollection<QualityTierDef>(ContentPack.getScriptableObjectName);
            qualityTierDefs.Add(qualityTierDefsList.ToArray());

            NamedAssetCollection<ItemQualityGroup> itemQualityGroups = new NamedAssetCollection<ItemQualityGroup>(ContentPack.getScriptableObjectName);
            itemQualityGroups.Add(itemQualityGroupsList.ToArray());

            NamedAssetCollection<EquipmentQualityGroup> equipmentQualityGroups = new NamedAssetCollection<EquipmentQualityGroup>(ContentPack.getScriptableObjectName);
            equipmentQualityGroups.Add(equipmentQualityGroupsList.ToArray());

            _contentPack.itemDefs.Add(itemDefsList.ToArray());
            _contentPack.itemTierDefs.Add(itemTierDefsList.ToArray());

            _contentPack.equipmentDefs.Add(equipmentDefsList.ToArray());

            _contentPack.projectilePrefabs.Add(projectilePrefabsList.ToArray());

            _contentPack.networkedObjectPrefabs.Add(networkedPrefabsList.ToArray());

            populateTypeFields(typeof(QualityTiers), qualityTierDefs, fieldName => "qd" + fieldName);
            QualityTiers.AllQualityTiers = new ReadOnlyCollection<QualityTierDef>(qualityTierDefs.ToArray());

            populateTypeFields(typeof(ItemQualityGroups), itemQualityGroups, fieldName => "ig" + fieldName);
            ItemQualityGroups.AllGroups = new ReadOnlyCollection<ItemQualityGroup>(itemQualityGroups.ToArray());

            populateTypeFields(typeof(EquipmentQualityGroups), equipmentQualityGroups, fieldName => "eg" + fieldName);
            EquipmentQualityGroups.AllGroups = new ReadOnlyCollection<EquipmentQualityGroup>(equipmentQualityGroups.ToArray());

            populateTypeFields(typeof(Prefabs), prefabs);

            populateTypeFields(typeof(ProjectilePrefabs), _contentPack.projectilePrefabs);

            TMP_SpriteAssets.Add(spriteAssetsList.ToArray());

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

            public static ItemQualityGroup SecondarySkillMagazine;

            public static ItemQualityGroup Firework;

            public static ItemQualityGroup Medkit;

            public static ItemQualityGroup Mushroom;

            public static ItemQualityGroup EquipmentMagazine;

            public static ItemQualityGroup HealingPotion;

            public static ItemQualityGroup HealingPotionConsumed;

            public static ItemQualityGroup TriggerEnemyDebuffs;

            public static ItemQualityGroup IncreaseDamageOnMultiKill;

            public static ItemQualityGroup NearbyDamageBonus;

            public static ItemQualityGroup AttackSpeedAndMoveSpeed;

            public static ItemQualityGroup Tooth;

            public static ItemQualityGroup GoldOnHurt;

            public static ItemQualityGroup TreasureCache;
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

        public static class ProjectilePrefabs
        {
            public static GameObject FireworkProjectileBig;
        }
    }
}
