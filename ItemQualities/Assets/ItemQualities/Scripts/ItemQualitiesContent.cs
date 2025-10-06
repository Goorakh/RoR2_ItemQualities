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
        readonly ExtendedContentPack _contentPack = new ExtendedContentPack();

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
            using PartitionedProgress partitionedProgress = new PartitionedProgress(args.progressReceiver);
            IProgress<float> loadContentProgress = partitionedProgress.AddPartition(1f);
            IProgress<float> finalizeContentProgress = partitionedProgress.AddPartition(1f);

            ParallelProgressCoroutine loadContentCoroutine = new ParallelProgressCoroutine(loadContentProgress);

            ReadableProgress<float> loadAssetBundleProgress = new ReadableProgress<float>();
            loadContentCoroutine.Add(loadAssetBundleContentAsync(loadAssetBundleProgress), loadAssetBundleProgress);

            ReadableProgress<float> contentInitializersProgress = new ReadableProgress<float>();
            loadContentCoroutine.Add(ContentInitializerAttribute.RunContentInitializers(_contentPack, contentInitializersProgress), contentInitializersProgress);

            yield return loadContentCoroutine;

            Stopwatch stopwatch = Stopwatch.StartNew();

            ParallelProgressCoroutine finalizeContentCoroutine = new ParallelProgressCoroutine(finalizeContentProgress);

            ReadableProgress<float> contentLoadCallbacksProgress = new ReadableProgress<float>();
            finalizeContentCoroutine.Add(runContentLoadCallbacks(contentLoadCallbacksProgress), contentLoadCallbacksProgress);

            yield return finalizeContentCoroutine;

            populateTypeFields(typeof(QualityTiers), _contentPack.qualityTierDefs, fieldName => "qd" + fieldName);
            QualityTiers.AllQualityTiers = new ReadOnlyCollection<QualityTierDef>(_contentPack.qualityTierDefs.ToArray());

            populateTypeFields(typeof(ItemQualityGroups), _contentPack.itemQualityGroups, fieldName => "ig" + fieldName);
            ItemQualityGroups.AllGroups = new ReadOnlyCollection<ItemQualityGroup>(_contentPack.itemQualityGroups.ToArray());

            populateTypeFields(typeof(EquipmentQualityGroups), _contentPack.equipmentQualityGroups, fieldName => "eg" + fieldName);
            EquipmentQualityGroups.AllGroups = new ReadOnlyCollection<EquipmentQualityGroup>(_contentPack.equipmentQualityGroups.ToArray());

            populateTypeFields(typeof(BuffQualityGroups), _contentPack.buffQualityGroups, fieldName => "bg" + fieldName);
            BuffQualityGroups.AllGroups = new ReadOnlyCollection<BuffQualityGroup>(_contentPack.buffQualityGroups.ToArray());

            populateTypeFields(typeof(Buffs), _contentPack.buffDefs, fieldName => "bd" + fieldName);

            populateTypeFields(typeof(Prefabs), _contentPack.prefabs);

            populateTypeFields(typeof(NetworkedPrefabs), _contentPack.networkedObjectPrefabs);

            populateTypeFields(typeof(ProjectilePrefabs), _contentPack.projectilePrefabs);

            TMP_SpriteAssets = _contentPack.spriteAssets;

            Log.Debug($"Finalized content in {stopwatch.Elapsed.TotalMilliseconds:F0}ms");
        }

        IEnumerator loadAssetBundleContentAsync(IProgress<float> progressReceiver)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();

            string assetBundleLocation = Path.Combine(Path.GetDirectoryName(ItemQualitiesPlugin.Instance.Info.Location), "itemqualitiesassets");
            if (!File.Exists(assetBundleLocation))
            {
                throw new FileNotFoundException("Could not find ItemQualities assetbundle file");
            }

            using PartitionedProgress totalProgress = new PartitionedProgress(progressReceiver);
            IProgress<float> loadAssetBundleProgress = totalProgress.AddPartition(0.5f);
            IProgress<float> loadAssetsProgress = totalProgress.AddPartition();
            IProgress<float> generateAssetsProgress = totalProgress.AddPartition();

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
                    generateAssetsCoroutine.Add(asyncAssetGenerator.GenerateAssetsAsync(_contentPack, generateProgress), generateProgress);
                }

                if (asset is GameObject gameObject)
                {
                    foreach (IAsyncAssetGenerator asyncAssetGeneratorComponent in gameObject.GetComponentsInChildren<IAsyncAssetGenerator>(true))
                    {
                        ReadableProgress<float> generateProgress = new ReadableProgress<float>();
                        generateAssetsCoroutine.Add(asyncAssetGeneratorComponent.GenerateAssetsAsync(_contentPack, generateProgress), generateProgress);
                    }
                }
            }

            yield return generateAssetsCoroutine;

            List<GameObject> projectilePrefabsList = new List<GameObject>();
            List<GameObject> networkedPrefabsList = new List<GameObject>();
            List<GameObject> prefabsList = new List<GameObject>();

            List<QualityTierDef> qualityTierDefsList = new List<QualityTierDef>();

            List<ItemQualityGroup> itemQualityGroupsList = new List<ItemQualityGroup>();
            List<EquipmentQualityGroup> equipmentQualityGroupsList = new List<EquipmentQualityGroup>();
            List<BuffQualityGroup> buffQualityGroupsList = new List<BuffQualityGroup>();

            List<ItemDef> itemDefsList = new List<ItemDef>();
            List<ItemTierDef> itemTierDefsList = new List<ItemTierDef>();

            List<EquipmentDef> equipmentDefsList = new List<EquipmentDef>();

            List<BuffDef> buffDefsList = new List<BuffDef>();

            List<EntityStateConfiguration> entityStateConfigurationsList = new List<EntityStateConfiguration>();

            List<TMP_SpriteAsset> spriteAssetsList = new List<TMP_SpriteAsset>();

            foreach (UnityEngine.Object obj in assetBundleAssets)
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
                    case BuffQualityGroup buffQualityGroup:
                        buffQualityGroupsList.Add(buffQualityGroup);
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
                    case BuffDef buffDef:
                        buffDefsList.Add(buffDef);
                        break;
                    case EntityStateConfiguration entityStateConfiguration:
                        entityStateConfigurationsList.Add(entityStateConfiguration);
                        break;
                    case TMP_SpriteAsset spriteAsset:
                        spriteAssetsList.Add(spriteAsset);
                        break;
                }
            }

            _contentPack.prefabs.Add(prefabsList.ToArray());

            _contentPack.qualityTierDefs.Add(qualityTierDefsList.ToArray());

            _contentPack.itemQualityGroups.Add(itemQualityGroupsList.ToArray());
            _contentPack.equipmentQualityGroups.Add(equipmentQualityGroupsList.ToArray());
            _contentPack.buffQualityGroups.Add(buffQualityGroupsList.ToArray());

            _contentPack.itemDefs.Add(itemDefsList.ToArray());
            _contentPack.itemTierDefs.Add(itemTierDefsList.ToArray());

            _contentPack.buffDefs.Add(buffDefsList.ToArray());

            _contentPack.equipmentDefs.Add(equipmentDefsList.ToArray());

            _contentPack.projectilePrefabs.Add(projectilePrefabsList.ToArray());

            _contentPack.networkedObjectPrefabs.Add(networkedPrefabsList.ToArray());

            _contentPack.entityStateConfigurations.Add(entityStateConfigurationsList.ToArray());
            _contentPack.entityStateTypes.Add(entityStateConfigurationsList.Select(esc => (Type)esc.targetType).Where(t => t != null).ToArray());

            _contentPack.spriteAssets.Add(spriteAssetsList.ToArray());

            Log.Debug($"Loaded asset bundle contents in {stopwatch.Elapsed.TotalMilliseconds:F0}ms");
        }

        IEnumerator runContentLoadCallbacks(IProgress<float> progressReceiver)
        {
            ParallelProgressCoroutine callbackParallelCoroutine = new ParallelProgressCoroutine(progressReceiver);

            foreach (UnityEngine.Object asset in _contentPack.allAssets)
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

            public static ItemQualityGroup FlatHealth;

            public static ItemQualityGroup StickyBomb;

            public static ItemQualityGroup StunChanceOnHit;

            public static ItemQualityGroup BleedOnHit;

            public static ItemQualityGroup Clover;

            public static ItemQualityGroup WardOnLevel;

            public static ItemQualityGroup Bear;

            public static ItemQualityGroup Missile;

            public static ItemQualityGroup WarCryOnMultiKill;

            public static ItemQualityGroup KnockBackHitEnemies;

            public static ItemQualityGroup BonusGoldPackOnKill;

            public static ItemQualityGroup DeathMark;

            public static ItemQualityGroup Bandolier;

            public static ItemQualityGroup SlowOnHit;

            public static ItemQualityGroup ExecuteLowHealthElite;

            public static ItemQualityGroup MoveSpeedOnKill;

            public static ItemQualityGroup Feather;

            public static ItemQualityGroup StrengthenBurn;

            public static ItemQualityGroup Infusion;

            public static ItemQualityGroup FireRing;

            public static ItemQualityGroup Seed;

            public static ItemQualityGroup TPHealingNova;

            public static ItemQualityGroup IncreasePrimaryDamage;

            public static ItemQualityGroup ExtraStatsOnLevelUp;

            public static ItemQualityGroup AttackSpeedOnCrit;

            public static ItemQualityGroup Thorns;

            public static ItemQualityGroup SprintOutOfCombat;

            public static ItemQualityGroup RegeneratingScrap;

            public static ItemQualityGroup RegeneratingScrapConsumed;

            public static ItemQualityGroup SprintArmor;

            public static ItemQualityGroup IceRing;

            public static ItemQualityGroup LowerPricedChests;

            public static ItemQualityGroup LowerPricedChestsConsumed;
        }

        public static class EquipmentQualityGroups
        {
            internal static IReadOnlyCollection<EquipmentQualityGroup> AllGroups = Array.Empty<EquipmentQualityGroup>();

            public static EquipmentQualityGroup BossHunterConsumed;
        }

        public static class BuffQualityGroups
        {
            internal static IReadOnlyCollection<BuffQualityGroup> AllGroups = Array.Empty<BuffQualityGroup>();

            public static BuffQualityGroup DeathMark;

            public static BuffQualityGroup Slow60;

            public static BuffQualityGroup KillMoveSpeed;

            public static BuffQualityGroup AttackSpeedOnCrit;

            public static BuffQualityGroup WhipBoost;
        }

        public static class Buffs
        {
            public static BuffDef BossStun;

            public static BuffDef SprintArmorStrong;
        }

        public static class Prefabs
        {
            public static GameObject QualityPickupDisplay;

            public static GameObject DeathMarkQualityEffect;
        }

        public static class NetworkedPrefabs
        {
            public static GameObject BossArenaHealNovaSpawner;
        }

        public static class ProjectilePrefabs
        {
        }
    }
}
