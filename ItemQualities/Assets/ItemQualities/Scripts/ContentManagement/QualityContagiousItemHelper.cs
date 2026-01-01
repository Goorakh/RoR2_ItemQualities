using HG.Coroutines;
using ItemQualities.Utilities.Extensions;
using RoR2;
using RoR2.ContentManagement;
using RoR2BepInExPack.GameAssetPathsBetter;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace ItemQualities.ContentManagement
{
    internal sealed class QualityContagiousItemHelper : IDisposable
    {
        int _completedSteps;

        readonly AssetReferenceT<ItemRelationshipType> _contagiousItemRelationshipTypeRef = new AssetReferenceT<ItemRelationshipType>(RoR2_DLC1_Common.ContagiousItem_asset);
        ItemRelationshipType _contagiousItemRelationshipType;

        ItemRelationshipProvider _contagiousQualityItemProvider;
        bool _contagiousItemProviderInContentPack = false;

        readonly Dictionary<ItemDef, ItemQualityGroup> _baseItemToGroupLookup = new Dictionary<ItemDef, ItemQualityGroup>();
        readonly List<AssetReferenceT<ItemDef>> _itemDefReferences = new List<AssetReferenceT<ItemDef>>();

        IEnumerator initialize(ExtendedContentPack contentPack, IProgress<float> progessReceiver)
        {
            ParallelProgressCoroutine initializeCoroutine = new ParallelProgressCoroutine(progessReceiver);

            AsyncOperationHandle<ItemRelationshipType> contagiousItemRelationshipTypeLoad = AssetAsyncReferenceManager<ItemRelationshipType>.LoadAsset(_contagiousItemRelationshipTypeRef);
            contagiousItemRelationshipTypeLoad.OnSuccess(contagiousItemRelationshipType =>
            {
                _contagiousItemRelationshipType = contagiousItemRelationshipType;
            });

            initializeCoroutine.Add(contagiousItemRelationshipTypeLoad);

            foreach (ItemQualityGroup itemQualityGroup in contentPack.itemQualityGroups)
            {
                if (itemQualityGroup.BaseItem)
                {
                    _baseItemToGroupLookup[itemQualityGroup.BaseItem] = itemQualityGroup;
                }
                else if (itemQualityGroup.BaseItemReference != null && itemQualityGroup.BaseItemReference.RuntimeKeyIsValid())
                {
                    AsyncOperationHandle<ItemDef> baseItemLoad = AssetAsyncReferenceManager<ItemDef>.LoadAsset(itemQualityGroup.BaseItemReference);

                    ItemQualityGroup capturedItemQualityGroup = itemQualityGroup;
                    baseItemLoad.OnSuccess(baseItem =>
                    {
                        _baseItemToGroupLookup[baseItem] = capturedItemQualityGroup;
                    });

                    _itemDefReferences.Add(itemQualityGroup.BaseItemReference);

                    initializeCoroutine.Add(baseItemLoad);
                }
            }

            return initializeCoroutine;
        }

        public IEnumerator Step(ExtendedContentPack contentPack, GetContentPackAsyncArgs args, IProgress<float> progessReceiver)
        {
            if (_completedSteps == 0)
            {
                yield return initialize(contentPack, progessReceiver);
            }

            bool addedContagiousItemProvider = false;

            if (_contagiousItemRelationshipType)
            {
                List<ItemDef.Pair> contagiousItemPairs = new List<ItemDef.Pair>();

                foreach (ContentPackLoadInfo peerLoadInfo in args.peerLoadInfos)
                {
                    ReadOnlyContentPack peerContentPack = peerLoadInfo.previousContentPack;
                    if (peerContentPack.identifier != contentPack.identifier)
                    {
                        foreach (ItemRelationshipProvider itemRelationshipProvider in peerContentPack.itemRelationshipProviders)
                        {
                            if (itemRelationshipProvider.relationshipType == _contagiousItemRelationshipType)
                            {
                                contagiousItemPairs.AddRange(itemRelationshipProvider.relationships);
                            }
                        }
                    }
                }

                List<ItemDef.Pair> contagiousQualityItemPairs = new List<ItemDef.Pair>();

                foreach (ItemDef.Pair contagiousItemPair in contagiousItemPairs)
                {
                    ItemDef sourceItemDef = contagiousItemPair.itemDef1;
                    ItemDef voidItemDef = contagiousItemPair.itemDef2;

                    if (!sourceItemDef || !voidItemDef)
                        continue;

                    if (_baseItemToGroupLookup.TryGetValue(sourceItemDef, out ItemQualityGroup sourceItemGroup))
                    {
                        ItemQualityGroup voidItemGroup = _baseItemToGroupLookup.GetValueOrDefault(voidItemDef);

                        for (QualityTier qualityTier = 0; qualityTier < QualityTier.Count; qualityTier++)
                        {
                            ItemDef qualitySourceItemDef = sourceItemGroup.GetItemDef(qualityTier);
                            ItemDef qualityVoidItemDef = voidItemGroup ? voidItemGroup.GetItemDef(qualityTier) : null;

                            // Fallback to corrupting into non-quality item if this quality of the void item has not been implemented
                            if (!qualityVoidItemDef)
                                qualityVoidItemDef = voidItemDef;

                            if (qualitySourceItemDef && qualityVoidItemDef)
                            {
                                contagiousQualityItemPairs.Add(new ItemDef.Pair
                                {
                                    itemDef1 = qualitySourceItemDef,
                                    itemDef2 = qualityVoidItemDef,
                                });
                            }
                        }
                    }
                }

                if (contagiousQualityItemPairs.Count > 0)
                {
                    if (!_contagiousQualityItemProvider)
                    {
                        _contagiousQualityItemProvider = ScriptableObject.CreateInstance<ItemRelationshipProvider>();
                        _contagiousQualityItemProvider.name = "QualityContagiousItemProvider";
                        _contagiousQualityItemProvider.relationshipType = _contagiousItemRelationshipType;
                    }

                    _contagiousQualityItemProvider.relationships = contagiousQualityItemPairs.ToArray();
                    args.output.itemRelationshipProviders.Add(_contagiousQualityItemProvider);
                    addedContagiousItemProvider = true;
                }
            }

            _contagiousItemProviderInContentPack = addedContagiousItemProvider;

            _completedSteps++;

            progessReceiver.Report(1f);
        }

        public void Dispose()
        {
            AssetAsyncReferenceManager<ItemRelationshipType>.UnloadAsset(_contagiousItemRelationshipTypeRef);
            _contagiousItemRelationshipType = null;

            foreach (AssetReferenceT<ItemDef> itemReference in _itemDefReferences)
            {
                AssetAsyncReferenceManager<ItemDef>.UnloadAsset(itemReference);
            }

            _itemDefReferences.Clear();
            _baseItemToGroupLookup.Clear();

            if (_contagiousQualityItemProvider)
            {
                if (!_contagiousItemProviderInContentPack)
                {
                    Log.Debug("Contagious Quality Item Provider not in content pack, cleaning up asset");
                    GameObject.Destroy(_contagiousQualityItemProvider);
                }
                else
                {
                    Log.Debug($"Adding contagious item relationships: [{string.Join(", ", _contagiousQualityItemProvider.relationships.Select(p => $"{p.itemDef1} -> {p.itemDef2}"))}]");
                }

                _contagiousQualityItemProvider = null;
            }
        }
    }
}
