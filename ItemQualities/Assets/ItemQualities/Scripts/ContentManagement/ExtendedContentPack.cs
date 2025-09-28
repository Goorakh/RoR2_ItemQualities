using RoR2;
using RoR2.ContentManagement;
using RoR2.EntitlementManagement;
using RoR2.ExpansionManagement;
using RoR2.Skills;
using System;
using UnityEngine;

namespace ItemQualities.ContentManagement
{
    public class ExtendedContentPack
    {
        readonly ContentPack _innerContentPack;

        public ExtendedContentPack(ContentPack contentPack)
        {
            _innerContentPack = contentPack;
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

        public static implicit operator ContentPack(ExtendedContentPack contentPack)
        {
            return contentPack._innerContentPack;
        }
    }
}
