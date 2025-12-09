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

        QualityContagiousItemHelper _qualityContagiousItemHelper;
        ProjectileExplosionEffectScaleFixHelper _projectileExplosionEffectScaleFixHelper;

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

            populateTypeFields(typeof(Items), _contentPack.itemDefs);

            populateTypeFields(typeof(EquipmentQualityGroups), _contentPack.equipmentQualityGroups, fieldName => "eg" + fieldName);
            EquipmentQualityGroups.AllGroups = new ReadOnlyCollection<EquipmentQualityGroup>(_contentPack.equipmentQualityGroups.ToArray());

            populateTypeFields(typeof(BuffQualityGroups), _contentPack.buffQualityGroups, fieldName => "bg" + fieldName);
            BuffQualityGroups.AllGroups = new ReadOnlyCollection<BuffQualityGroup>(_contentPack.buffQualityGroups.ToArray());

            populateTypeFields(typeof(Buffs), _contentPack.buffDefs, fieldName => "bd" + fieldName);

            populateTypeFields(typeof(Prefabs), _contentPack.prefabs);

            populateTypeFields(typeof(NetworkedPrefabs), _contentPack.networkedObjectPrefabs);

            populateTypeFields(typeof(ProjectilePrefabs), _contentPack.projectilePrefabs);

            populateTypeFields(typeof(Materials), _contentPack.materials, fieldName => "mat" + fieldName);

            populateTypeFields(typeof(SpawnCards), _contentPack.spawnCards);

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

            List<GameObject> networkedPrefabsList = new List<GameObject>();
            List<GameObject> prefabsList = new List<GameObject>();

            List<GameObject> projectilePrefabsList = new List<GameObject>();

            List<GameObject> bodyPrefabsList = new List<GameObject>();
            List<GameObject> masterPrefabsList = new List<GameObject>();

            List<EffectDef> effectDefsList = new List<EffectDef>();

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

            List<Material> materialsList = new List<Material>();

            List<SpawnCard> spawnCardsList = new List<SpawnCard>();

            List<Texture> texturesList = new List<Texture>();

            foreach (UnityEngine.Object obj in assetBundleAssets)
            {
                switch (obj)
                {
                    case GameObject prefab:

                        if (prefab.GetComponent<ProjectileController>())
                        {
                            projectilePrefabsList.Add(prefab);
                        }

                        if (prefab.GetComponent<CharacterBody>())
                        {
                            bodyPrefabsList.Add(prefab);
                        }

                        if (prefab.GetComponent<CharacterMaster>())
                        {
                            masterPrefabsList.Add(prefab);
                        }

                        if (prefab.GetComponent<EffectComponent>())
                        {
                            effectDefsList.Add(new EffectDef(prefab));
                        }

                        if (prefab.GetComponent<NetworkBehaviour>())
                        {
                            networkedPrefabsList.Add(prefab);
                        }
                        else
                        {
                            prefabsList.Add(prefab);
                        }

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
                    case Material material:
                        materialsList.Add(material);
                        break;
                    case SpawnCard spawnCard:
                        spawnCardsList.Add(spawnCard);
                        break;
                    case Texture texture:
                        texturesList.Add(texture);
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

            _contentPack.masterPrefabs.Add(masterPrefabsList.ToArray());
            _contentPack.bodyPrefabs.Add(bodyPrefabsList.ToArray());

            _contentPack.effectDefs.Add(effectDefsList.ToArray());

            _contentPack.networkedObjectPrefabs.Add(networkedPrefabsList.ToArray());

            _contentPack.entityStateConfigurations.Add(entityStateConfigurationsList.ToArray());
            _contentPack.entityStateTypes.Add(entityStateConfigurationsList.Select(esc => (Type)esc.targetType).Where(t => t != null).ToArray());

            _contentPack.spriteAssets.Add(spriteAssetsList.ToArray());

            _contentPack.materials.Add(materialsList.ToArray());

            _contentPack.spawnCards.Add(spawnCardsList.ToArray());

            _contentPack.textures.Add(texturesList.ToArray());

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

            _qualityContagiousItemHelper ??= new QualityContagiousItemHelper();
            yield return _qualityContagiousItemHelper.Step(_contentPack, args, args.progressReceiver);

            _projectileExplosionEffectScaleFixHelper ??= new ProjectileExplosionEffectScaleFixHelper();
            _projectileExplosionEffectScaleFixHelper.Step(_contentPack, args);
        }

        public IEnumerator FinalizeAsync(FinalizeAsyncArgs args)
        {
            ContentManager.collectContentPackProviders -= collectContentPackProviders;

            _qualityContagiousItemHelper?.Dispose();
            _qualityContagiousItemHelper = null;

            _projectileExplosionEffectScaleFixHelper = null;

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

            public static ItemQualityGroup HealOnCrit;

            public static ItemQualityGroup FreeChest;

            public static ItemQualityGroup PrimarySkillShuriken;

            public static ItemQualityGroup Squid;

            public static ItemQualityGroup Phasing;

            public static ItemQualityGroup ChainLightning;

            public static ItemQualityGroup TeleportOnLowHealth;

            public static ItemQualityGroup TeleportOnLowHealthConsumed;

            public static ItemQualityGroup EnergizedOnEquipmentUse;

            public static ItemQualityGroup JumpBoost;

            public static ItemQualityGroup BarrierOnOverHeal;

            public static ItemQualityGroup AlienHead;

            public static ItemQualityGroup FallBoots;

            public static ItemQualityGroup Behemoth;

            public static ItemQualityGroup RandomEquipmentTrigger;

            public static ItemQualityGroup ImmuneToDebuff;

            public static ItemQualityGroup KillEliteFrenzy;

            public static ItemQualityGroup ExtraLife;

            public static ItemQualityGroup ExtraLifeConsumed;

            public static ItemQualityGroup ArmorPlate;

            public static ItemQualityGroup StunAndPierce;

            public static ItemQualityGroup Icicle;

            public static ItemQualityGroup OutOfCombatArmor;
            
            public static ItemQualityGroup BoostAllStats;

            public static ItemQualityGroup GhostOnKill;

            public static ItemQualityGroup UtilitySkillMagazine;

            public static ItemQualityGroup MoreMissile;

            public static ItemQualityGroup Plant;

            public static ItemQualityGroup CritDamage;

            public static ItemQualityGroup IncreaseHealing;
            
            public static ItemQualityGroup NovaOnHeal;

            public static ItemQualityGroup LaserTurbine;

            public static ItemQualityGroup MeteorAttackOnHighDamage;

            public static ItemQualityGroup BossDamageBonus;

            public static ItemQualityGroup PermanentDebuffOnHit;
            
            public static ItemQualityGroup SpeedBoostPickup;

            public static ItemQualityGroup BounceNearby;

            public static ItemQualityGroup ItemDropChanceOnKill;

            public static ItemQualityGroup Talisman;

            public static ItemQualityGroup DroneWeapons;
            
            public static ItemQualityGroup BarrageOnBoss;

            public static ItemQualityGroup CloverVoid;

            public static ItemQualityGroup EquipmentMagazineVoid;

            public static ItemQualityGroup IgniteOnKill;
            
            public static ItemQualityGroup BleedOnHitVoid;

            public static ItemQualityGroup VoidMegaCrabItem;

            public static ItemQualityGroup MissileVoid;

            public static ItemQualityGroup DelayedDamage;

            public static ItemQualityGroup BearVoid;

            public static ItemQualityGroup ArmorReductionOnHit;
            
            public static ItemQualityGroup SlowOnHitVoid;

            public static ItemQualityGroup ElementalRingVoid;

            public static ItemQualityGroup ChainLightningVoid;

            public static ItemQualityGroup CritGlassesVoid;

            public static ItemQualityGroup ExtraLifeVoid;

            public static ItemQualityGroup ExtraLifeVoidConsumed;

            public static ItemQualityGroup TreasureCacheVoid;

            public static ItemQualityGroup ExplodeOnDeathVoid;

            public static ItemQualityGroup MushroomVoid;

            public static ItemQualityGroup AttackSpeedPerNearbyAllyOrEnemy;

            public static ItemQualityGroup HeadHunter;

            public static ItemQualityGroup ScrapWhite;

            public static ItemQualityGroup ScrapGreen;

            public static ItemQualityGroup ScrapRed;

            public static ItemQualityGroup ScrapYellow;

            public static ItemQualityGroup BarrierOnCooldown;

            public static ItemQualityGroup SpeedOnPickup;

            public static ItemQualityGroup Duplicator;

            public static ItemQualityGroup ExplodeOnDeath;

            public static ItemQualityGroup DronesDropDynamite;

            public static ItemQualityGroup CritAtLowerElevation;

            public static ItemQualityGroup ShieldBooster;

            public static ItemQualityGroup SharedSuffering;
        }

        public static class Items
        {
            public static ItemDef DronesDropDynamiteQualityDroneItem;
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

            public static BuffQualityGroup Energized;

            public static BuffQualityGroup KillEliteFrenzyBuff;

            public static BuffQualityGroup ArmorPlateBuildup;

            public static BuffQualityGroup ArmorPlateBuff;

            public static BuffQualityGroup BoostAllStatsBuff;

            public static BuffQualityGroup AttackSpeedPerNearbyAllyOrEnemyBuff;

            public static BuffQualityGroup FragileDamageBonusBuff;

            public static BuffQualityGroup GoldArmorBuff;

            public static BuffQualityGroup ToothPrimaryBuff;

            public static BuffQualityGroup ToothSecondaryBuff;

            public static BuffQualityGroup ShieldBoosterBuff;
        }

        public static class Buffs
        {
            public static BuffDef BossStun;

            public static BuffDef SprintArmorStrong;

            public static BuffDef HealCritBoost;

            public static BuffDef MiniBossMarker;

            public static BuffDef GoldenGun;

            public static BuffDef PersonalShield;

            public static BuffDef FeatherExtraJumps;
        }

        public static class Prefabs
        {
            public static GameObject QualityPickupDisplay;

            public static GameObject DeathMarkQualityEffect;

            public static GameObject VoidDeathOrbEffect;
        }

        public static class NetworkedPrefabs
        {
            public static GameObject BossArenaHealNovaSpawner;

            public static GameObject ChainLightningArcAttachment;

            public static GameObject ExtraLifeReviveAttachment;

            public static GameObject MiniBossBodyAttachment;

            public static GameObject MeatHookDelayedForce;

            public static GameObject SlowOnHitRootArea;

            public static GameObject HealPackDelayed;

            public static GameObject HealOrbPrimary;

            public static GameObject HealOrbSecondary;

            public static GameObject HealOrbUtility;

            public static GameObject HealOrbSpecial;

            public static GameObject DuplicatorQualityAttachment;

            public static GameObject DroneShootableAttachment;
        }

        public static class ProjectilePrefabs
        {
        }

        public static class Materials
        {
            public static Material HealCritBoost;
        }

        public static class SpawnCards
        {
            [TargetAssetName("iscQualityEquipmentBarrel")]
            public static InteractableSpawnCard QualityEquipmentBarrel;

            [TargetAssetName("iscQualityChest2")]
            public static InteractableSpawnCard QualityChest2;

            [TargetAssetName("iscQualityChest1")]
            public static InteractableSpawnCard QualityChest1;

            [TargetAssetName("iscQualityDuplicator")]
            public static InteractableSpawnCard QualityDuplicator;

            [TargetAssetName("iscQualityDuplicatorLarge")]
            public static InteractableSpawnCard QualityDuplicatorLarge;

            [TargetAssetName("iscQualityDuplicatorMilitary")]
            public static InteractableSpawnCard QualityDuplicatorMilitary;

            [TargetAssetName("iscQualityDuplicatorWild")]
            public static InteractableSpawnCard QualityDuplicatorWild;
        }
    }
}
