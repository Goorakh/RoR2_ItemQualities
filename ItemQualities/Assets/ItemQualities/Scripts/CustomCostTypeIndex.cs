using HG;
using ItemQualities.Utilities.Extensions;
using RoR2;
using RoR2.Audio;
using RoR2.Items;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace ItemQualities
{
    static class CustomCostTypeIndex
    {
        static readonly CostTypeDef _whiteItemQualityCostDef = new CostTypeDef
        {
            name = "WhiteItemQuality",
            colorIndex = ColorCatalog.ColorIndex.Tier1Item,
            itemTier = ItemTier.Tier1,
            costStringFormatToken = "COST_QUALITY_ITEM_FORMAT",
            isAffordable = isAffordableQualityItems,
            payCost = payCostQualityItems
        };

        static readonly CostTypeDef _greenItemQualityCostDef = new CostTypeDef
        {
            name = "GreenItemQuality",
            colorIndex = ColorCatalog.ColorIndex.Tier2Item,
            itemTier = ItemTier.Tier2,
            costStringFormatToken = "COST_QUALITY_ITEM_FORMAT",
            isAffordable = isAffordableQualityItems,
            payCost = payCostQualityItems
        };

        static readonly CostTypeDef _redItemQualityCostDef = new CostTypeDef
        {
            name = "RedItemQuality",
            colorIndex = ColorCatalog.ColorIndex.Tier3Item,
            itemTier = ItemTier.Tier3,
            saturateWorldStyledCostString = false,
            costStringFormatToken = "COST_QUALITY_ITEM_FORMAT",
            isAffordable = isAffordableQualityItems,
            payCost = payCostQualityItems
        };

        static readonly CostTypeDef _bossItemQualityCostDef = new CostTypeDef
        {
            name = "BossItemQuality",
            colorIndex = ColorCatalog.ColorIndex.BossItem,
            itemTier = ItemTier.Boss,
            costStringFormatToken = "COST_QUALITY_ITEM_FORMAT",
            isAffordable = isAffordableQualityItems,
            payCost = payCostQualityItems
        };

        public static CostTypeIndex WhiteItemQuality { get; private set; } = CostTypeIndex.None;

        public static CostTypeIndex GreenItemQuality { get; private set; } = CostTypeIndex.None;

        public static CostTypeIndex RedItemQuality { get; private set; } = CostTypeIndex.None;

        public static CostTypeIndex BossItemQuality { get; private set; } = CostTypeIndex.None;

        [SystemInitializer(typeof(CostTypeCatalog))]
        static void Init()
        {
            for (CostTypeIndex costTypeIndex = 0; (int)costTypeIndex < CostTypeCatalog.costTypeCount; costTypeIndex++)
            {
                CostTypeDef costTypeDef = CostTypeCatalog.GetCostTypeDef(costTypeIndex);
                if (costTypeDef == _whiteItemQualityCostDef)
                {
                    WhiteItemQuality = costTypeIndex;
                }
                else if (costTypeDef == _greenItemQualityCostDef)
                {
                    GreenItemQuality = costTypeIndex;
                }
                else if (costTypeDef == _redItemQualityCostDef)
                {
                    RedItemQuality = costTypeIndex;
                }
                else if (costTypeDef == _bossItemQualityCostDef)
                {
                    BossItemQuality = costTypeIndex;
                }
            }
        }

        internal static void Register()
        {
            CostTypeCatalog.modHelper.getAdditionalEntries += getAdditionalEntries;
        }

        static void getAdditionalEntries(List<CostTypeDef> costTypeDefs)
        {
            costTypeDefs.Add(_whiteItemQualityCostDef);
            costTypeDefs.Add(_greenItemQualityCostDef);
            costTypeDefs.Add(_redItemQualityCostDef);
            costTypeDefs.Add(_bossItemQualityCostDef);
        }
        
        static bool isAffordableQualityItems(CostTypeDef costTypeDef, CostTypeDef.IsAffordableContext context)
        {
            CharacterBody activatorBody = context.activator ? context.activator.GetComponent<CharacterBody>() : null;
            Inventory activatorInventory = activatorBody ? activatorBody.inventory : null;
            return activatorInventory && activatorInventory.HasAtLeastXTotalRemovableQualityItemsOfTier(costTypeDef.itemTier, context.cost);
        }

        static void payCostQualityItems(CostTypeDef costTypeDef, CostTypeDef.PayCostContext context)
        {
            if (context.activatorBody)
            {
                Inventory inventory = context.activatorBody.inventory;
                if (inventory)
                {
                    List<ItemIndex> itemsToTake = ListPool<ItemIndex>.RentCollection();

                    WeightedSelection<ItemIndex> itemSelection = new WeightedSelection<ItemIndex>();
                    WeightedSelection<ItemIndex>[] scrapSelectionsByQuality = new WeightedSelection<ItemIndex>[(int)QualityTier.Count];
                    WeightedSelection<ItemIndex>[] priorityScrapSelectionsByQuality = new WeightedSelection<ItemIndex>[(int)QualityTier.Count];

                    ItemQualityGroupIndex avoidedItemGroupIndex = QualityCatalog.FindItemQualityGroupIndex(context.avoidedItemIndex);

                    for (ItemIndex itemIndex = 0; (int)itemIndex < ItemCatalog.itemCount; itemIndex++)
                    {
                        QualityTier qualityTier = QualityCatalog.GetQualityTier(itemIndex);
                        if (qualityTier == QualityTier.None)
                            continue;

                        ItemQualityGroupIndex itemGroupIndex = QualityCatalog.FindItemQualityGroupIndex(itemIndex);
                        if (itemGroupIndex == avoidedItemGroupIndex)
                            continue;

                        int itemCount = inventory.GetItemCount(itemIndex);
                        if (itemCount <= 0)
                            continue;

                        ItemDef itemDef = ItemCatalog.GetItemDef(itemIndex);
                        if (itemDef.tier != costTypeDef.itemTier)
                            continue;
                        
                        WeightedSelection<ItemIndex>[] targetSelectionsByQuality = null;
                        if (itemDef.ContainsTag(ItemTag.PriorityScrap))
                        {
                            targetSelectionsByQuality = priorityScrapSelectionsByQuality;
                        }
                        else if (itemDef.ContainsTag(ItemTag.Scrap))
                        {
                            targetSelectionsByQuality = scrapSelectionsByQuality;
                        }

                        WeightedSelection<ItemIndex> targetSelection;
                        if (targetSelectionsByQuality != null)
                        {
                            targetSelection = (targetSelectionsByQuality[(int)qualityTier] ??= new WeightedSelection<ItemIndex>());
                        }
                        else
                        {
                            targetSelection = itemSelection;
                        }

                        targetSelection.AddChoice(itemIndex, itemCount);
                    }

                    void TakeItemFromWeightedSelection(WeightedSelection<ItemIndex> weightedSelection, int choiceIndex)
                    {
                        WeightedSelection<ItemIndex>.ChoiceInfo choice = weightedSelection.GetChoice(choiceIndex);
                        ItemIndex itemIndex = choice.value;
                        int itemCount = (int)choice.weight;
                        itemCount--;
                        if (itemCount <= 0)
                        {
                            weightedSelection.RemoveChoice(choiceIndex);
                        }
                        else
                        {
                            weightedSelection.ModifyChoiceWeight(choiceIndex, itemCount);
                        }

                        itemsToTake.Add(itemIndex);
                    }

                    void TakeItemsFromWeightedSelection(WeightedSelection<ItemIndex> weightedSelection)
                    {
                        while (weightedSelection.Count > 0 && itemsToTake.Count < context.cost)
                        {
                            int choiceIndex = weightedSelection.EvaluateToChoiceIndex(context.rng.nextNormalizedFloat);
                            TakeItemFromWeightedSelection(weightedSelection, choiceIndex);
                        }
                    }

                    void TakeItemsFromWeightedSelections(WeightedSelection<ItemIndex>[] weightedSelections)
                    {
                        for (int i = weightedSelections.Length - 1; i >= 0; i--)
                        {
                            if (weightedSelections[i] != null)
                            {
                                TakeItemsFromWeightedSelection(weightedSelections[i]);
                            }
                        }
                    }

                    TakeItemsFromWeightedSelections(priorityScrapSelectionsByQuality);
                    TakeItemsFromWeightedSelections(scrapSelectionsByQuality);
                    TakeItemsFromWeightedSelection(itemSelection);

                    ItemQualityGroup avoidedItemGroup = QualityCatalog.GetItemQualityGroup(QualityCatalog.FindItemQualityGroupIndex(context.avoidedItemIndex));
                    if (avoidedItemGroup)
                    {
                        if (itemsToTake.Count < context.cost)
                        {
                            ItemQualityCounts avoidedItemCounts = avoidedItemGroup.GetItemCounts(inventory);
                            for (QualityTier qualityTier = QualityTier.Count - 1; qualityTier >= 0; qualityTier--)
                            {
                                int itemCount = avoidedItemCounts[qualityTier];
                                int itemCountToAdd = Math.Min(itemCount, context.cost - itemsToTake.Count);
                                if (itemCountToAdd > 0)
                                {
                                    ItemIndex itemIndex = avoidedItemGroup.GetItemIndex(qualityTier);

                                    for (int i = 0; i < itemCountToAdd; i++)
                                    {
                                        itemsToTake.Add(itemIndex);
                                    }

                                    if (itemsToTake.Count >= context.cost)
                                        break;
                                }
                            }
                        }
                    }
                    else
                    {
                        for (int i = itemsToTake.Count; i < context.cost; i++)
                        {
                            itemsToTake.Add(context.avoidedItemIndex);
                        }
                    }

                    ItemQualityCounts takenRegeneratingScrapCounts = new ItemQualityCounts();
                    foreach (ItemIndex itemIndex in itemsToTake)
                    {
                        context.results.itemsTaken.Add(itemIndex);
                        if (QualityCatalog.GetItemIndexOfQuality(itemIndex, QualityTier.None) == DLC1Content.Items.RegeneratingScrap.itemIndex)
                        {
                            QualityTier scrapQualityTier = QualityCatalog.GetQualityTier(itemIndex);
                            takenRegeneratingScrapCounts[scrapQualityTier]++;

                            inventory.GiveItem(QualityCatalog.GetItemIndexOfQuality(DLC1Content.Items.RegeneratingScrapConsumed.itemIndex, scrapQualityTier));
                            EntitySoundManager.EmitSoundServer(NetworkSoundEventCatalog.FindNetworkSoundEventIndex("Play_item_proc_regenScrap_consume"), context.activatorBody.gameObject);
                            ModelLocator modelLocator = context.activatorBody.modelLocator;
                            if (modelLocator && modelLocator.modelTransform && modelLocator.modelTransform.TryGetComponent(out CharacterModel characterModel))
                            {
                                List<GameObject> itemDisplayObjects = characterModel.GetItemDisplayObjects(DLC1Content.Items.RegeneratingScrap.itemIndex);
                                if (itemDisplayObjects.Count > 0)
                                {
                                    GameObject effectRoot = itemDisplayObjects[0];
                                    GameObject effectPrefab = Addressables.LoadAssetAsync<GameObject>("RoR2/DLC1/RegeneratingScrap/RegeneratingScrapExplosionDisplay.prefab").WaitForCompletion();
                                    EffectData effectData = new EffectData
                                    {
                                        origin = effectRoot.transform.position,
                                        rotation = effectRoot.transform.rotation
                                    };

                                    EffectManager.SpawnEffect(effectPrefab, effectData, transmit: true);
                                }
                            }

                            EffectManager.SimpleMuzzleFlash(Addressables.LoadAssetAsync<GameObject>("RoR2/DLC1/RegeneratingScrap/RegeneratingScrapExplosionInPrinter.prefab").WaitForCompletion(), context.purchasedObject, "DropPivot", transmit: true);
                        }

                        inventory.RemoveItem(itemIndex);
                    }

                    if (takenRegeneratingScrapCounts.TotalCount > 0)
                    {
                        for (QualityTier qualityTier = 0; qualityTier < QualityTier.Count; qualityTier++)
                        {
                            if (takenRegeneratingScrapCounts[qualityTier] > 0)
                            {
                                ItemIndex regeneratingScrapItemIndex = QualityCatalog.GetItemIndexOfQuality(DLC1Content.Items.RegeneratingScrap.itemIndex, qualityTier);
                                ItemIndex regeneratingScrapConsumedItemIndex = QualityCatalog.GetItemIndexOfQuality(DLC1Content.Items.RegeneratingScrapConsumed.itemIndex, qualityTier);

                                CharacterMasterNotificationQueue.SendTransformNotification(context.activatorBody.master, regeneratingScrapItemIndex, regeneratingScrapConsumedItemIndex, CharacterMasterNotificationQueue.TransformationType.Default);
                            }
                        }
                    }

                    ListPool<ItemIndex>.ReturnCollection(itemsToTake);
                }
            }

            MultiShopCardUtils.OnNonMoneyPurchase(context);
        }

        public static bool IsQualityItemCostType(CostTypeIndex costTypeIndex)
        {
            return costTypeIndex == WhiteItemQuality ||
                   costTypeIndex == GreenItemQuality ||
                   costTypeIndex == RedItemQuality ||
                   costTypeIndex == BossItemQuality;
        }
    }
}
