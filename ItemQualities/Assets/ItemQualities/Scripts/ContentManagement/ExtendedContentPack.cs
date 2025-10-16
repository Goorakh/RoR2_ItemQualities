using RoR2;
using RoR2.ContentManagement;
using RoR2.EntitlementManagement;
using RoR2.ExpansionManagement;
using RoR2.Skills;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;

namespace ItemQualities.ContentManagement
{
    public class ExtendedContentPack
    {
        readonly ContentPack _innerContentPack;

        readonly NamedAssetCollection[] _extendedAssetCollections = Array.Empty<NamedAssetCollection>();

        public ExtendedContentPack(ContentPack contentPack)
        {
            _innerContentPack = contentPack;

            _extendedAssetCollections = new NamedAssetCollection[]
            {
                prefabs,
                spawnCards,
                qualityTierDefs,
                itemQualityGroups,
                equipmentQualityGroups,
                buffQualityGroups,
                spriteAssets,
                materials,
            };
        }

        public ExtendedContentPack() : this(new ContentPack())
        {
        }

        public string identifier
        {
            get
            {
                return _innerContentPack.identifier;
            }
            internal set
            {
                _innerContentPack.identifier = value;
            }
        }

        public NamedAssetCollection<GameObject> bodyPrefabs => _innerContentPack.bodyPrefabs;

        public NamedAssetCollection<GameObject> masterPrefabs => _innerContentPack.masterPrefabs;

        public NamedAssetCollection<GameObject> projectilePrefabs => _innerContentPack.projectilePrefabs;

        public NamedAssetCollection<GameObject> gameModePrefabs => _innerContentPack.gameModePrefabs;

        public NamedAssetCollection<GameObject> networkedObjectPrefabs => _innerContentPack.networkedObjectPrefabs;

        public NamedAssetCollection<SkillDef> skillDefs => _innerContentPack.skillDefs;

        public NamedAssetCollection<SkillFamily> skillFamilies => _innerContentPack.skillFamilies;

        public NamedAssetCollection<SceneDef> sceneDefs => _innerContentPack.sceneDefs;

        public NamedAssetCollection<ItemDef> itemDefs => _innerContentPack.itemDefs;

        public NamedAssetCollection<ItemTierDef> itemTierDefs => _innerContentPack.itemTierDefs;

        public NamedAssetCollection<ItemRelationshipProvider> itemRelationshipProviders => _innerContentPack.itemRelationshipProviders;

        public NamedAssetCollection<ItemRelationshipType> itemRelationshipTypes => _innerContentPack.itemRelationshipTypes;

        public NamedAssetCollection<EquipmentDef> equipmentDefs => _innerContentPack.equipmentDefs;

        public NamedAssetCollection<BuffDef> buffDefs => _innerContentPack.buffDefs;

        public NamedAssetCollection<EliteDef> eliteDefs => _innerContentPack.eliteDefs;

        public NamedAssetCollection<UnlockableDef> unlockableDefs => _innerContentPack.unlockableDefs;

        public NamedAssetCollection<SurvivorDef> survivorDefs => _innerContentPack.survivorDefs;

        public NamedAssetCollection<ArtifactDef> artifactDefs => _innerContentPack.artifactDefs;

        public NamedAssetCollection<EffectDef> effectDefs => _innerContentPack.effectDefs;

        public NamedAssetCollection<SurfaceDef> surfaceDefs => _innerContentPack.surfaceDefs;

        public NamedAssetCollection<NetworkSoundEventDef> networkSoundEventDefs => _innerContentPack.networkSoundEventDefs;

        public NamedAssetCollection<MusicTrackDef> musicTrackDefs => _innerContentPack.musicTrackDefs;

        public NamedAssetCollection<GameEndingDef> gameEndingDefs => _innerContentPack.gameEndingDefs;

        public NamedAssetCollection<EntityStateConfiguration> entityStateConfigurations => _innerContentPack.entityStateConfigurations;

        public NamedAssetCollection<ExpansionDef> expansionDefs => _innerContentPack.expansionDefs;

        public NamedAssetCollection<EntitlementDef> entitlementDefs => _innerContentPack.entitlementDefs;

        public NamedAssetCollection<MiscPickupDef> miscPickupDefs => _innerContentPack.miscPickupDefs;

        public NamedAssetCollection<Type> entityStateTypes => _innerContentPack.entityStateTypes;

        public NamedAssetCollection<GameObject> prefabs { get; } = new NamedAssetCollection<GameObject>(ContentPack.getGameObjectName);

        public NamedAssetCollection<SpawnCard> spawnCards { get; } = new NamedAssetCollection<SpawnCard>(ContentPack.getScriptableObjectName);

        public NamedAssetCollection<QualityTierDef> qualityTierDefs { get; } = new NamedAssetCollection<QualityTierDef>(ContentPack.getScriptableObjectName);

        public NamedAssetCollection<ItemQualityGroup> itemQualityGroups { get; } = new NamedAssetCollection<ItemQualityGroup>(ContentPack.getScriptableObjectName);

        public NamedAssetCollection<EquipmentQualityGroup> equipmentQualityGroups { get; } = new NamedAssetCollection<EquipmentQualityGroup>(ContentPack.getScriptableObjectName);

        public NamedAssetCollection<BuffQualityGroup> buffQualityGroups { get; } = new NamedAssetCollection<BuffQualityGroup>(ContentPack.getScriptableObjectName);

        public NamedAssetCollection<TMP_SpriteAsset> spriteAssets { get; } = new NamedAssetCollection<TMP_SpriteAsset>(ContentPack.getScriptableObjectName);

        public NamedAssetCollection<Material> materials { get; } = new NamedAssetCollection<Material>(getMaterialName);

        public IEnumerable<UnityEngine.Object> allAssets
        {
            get
            {
                List<UnityEngine.Object> allAssets = new List<UnityEngine.Object>();
                foreach (NamedAssetCollection assetCollection in _innerContentPack.assetCollections.OfType<NamedAssetCollection>()
                                                                                                   .Concat(_extendedAssetCollections))
                {
                    if (assetCollection is IEnumerable enumerable)
                    {
                        allAssets.AddRange(enumerable.OfType<UnityEngine.Object>());
                    }
                }

                return allAssets;
            }
        }

        public static Func<Material, string> getMaterialName = material => material.name;

        public static implicit operator ContentPack(ExtendedContentPack contentPack)
        {
            return contentPack._innerContentPack;
        }
    }
}
