using HG;
using ItemQualities.Utilities.Extensions;
using RoR2;
using RoR2.Audio;
using RoR2.Items;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace ItemQualities
{
    internal static class CustomCostTypeIndex
    {
        private static EffectIndex _regeneratingScrapDisplayExplosionEffectIndex = EffectIndex.Invalid;
        private static EffectIndex _regeneratingScrapPrinterExplosionEffectIndex = EffectIndex.Invalid;

        private static NetworkSoundEventIndex _regeneratingScrapProcSoundEventIndex = NetworkSoundEventIndex.Invalid;

        private static readonly CostTypeDef _whiteItemQualityCostDef = new CostTypeDef
        {
            name = "WhiteItemQuality",
            colorIndex = ColorCatalog.ColorIndex.Tier1Item,
            itemTier = ItemTier.Tier1,
            costStringFormatToken = "COST_QUALITY_ITEM_FORMAT",
            isAffordable = isAffordableQualityItems,
            payCost = payCostQualityItems
        };

        private static readonly CostTypeDef _greenItemQualityCostDef = new CostTypeDef
        {
            name = "GreenItemQuality",
            colorIndex = ColorCatalog.ColorIndex.Tier2Item,
            itemTier = ItemTier.Tier2,
            costStringFormatToken = "COST_QUALITY_ITEM_FORMAT",
            isAffordable = isAffordableQualityItems,
            payCost = payCostQualityItems
        };

        private static readonly CostTypeDef _redItemQualityCostDef = new CostTypeDef
        {
            name = "RedItemQuality",
            colorIndex = ColorCatalog.ColorIndex.Tier3Item,
            itemTier = ItemTier.Tier3,
            saturateWorldStyledCostString = false,
            costStringFormatToken = "COST_QUALITY_ITEM_FORMAT",
            isAffordable = isAffordableQualityItems,
            payCost = payCostQualityItems
        };

        private static readonly CostTypeDef _bossItemQualityCostDef = new CostTypeDef
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

        [SystemInitializer(typeof(CostTypeCatalog), typeof(EffectCatalogUtils), typeof(NetworkSoundEventCatalog))]
        private static void Init()
        {
            _regeneratingScrapDisplayExplosionEffectIndex = EffectCatalogUtils.FindEffectIndex("RegeneratingScrapExplosionDisplay");
            if (_regeneratingScrapDisplayExplosionEffectIndex == EffectIndex.Invalid)
            {
                Log.Warning("Failed to find RegeneratingScrapExplosionDisplay effect index");
            }

            _regeneratingScrapPrinterExplosionEffectIndex = EffectCatalogUtils.FindEffectIndex("RegeneratingScrapExplosionInPrinter");
            if (_regeneratingScrapPrinterExplosionEffectIndex == EffectIndex.Invalid)
            {
                Log.Warning("Failed to find RegeneratingScrapExplosionInPrinter effect index");
            }

            _regeneratingScrapProcSoundEventIndex = NetworkSoundEventCatalog.FindNetworkSoundEventIndex("Play_item_proc_regenScrap_consume");
            if (_regeneratingScrapProcSoundEventIndex == NetworkSoundEventIndex.Invalid)
            {
                Log.Warning("Failed to find regenerating scrap proc sound event index");
            }

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

        private static void getAdditionalEntries(List<CostTypeDef> costTypeDefs)
        {
            costTypeDefs.Add(_whiteItemQualityCostDef);
            costTypeDefs.Add(_greenItemQualityCostDef);
            costTypeDefs.Add(_redItemQualityCostDef);
            costTypeDefs.Add(_bossItemQualityCostDef);
        }

        private static bool isAffordableQualityItems(CostTypeDef costTypeDef, CostTypeDef.IsAffordableContext context)
        {
            CharacterBody activatorBody = context.activator ? context.activator.GetComponent<CharacterBody>() : null;
            Inventory activatorInventory = activatorBody ? activatorBody.inventory : null;
            return activatorInventory && activatorInventory.HasAtLeastXTotalQualityItemsOfTierForPurchase(costTypeDef.itemTier, context.cost);
        }

        private static void payCostQualityItems(CostTypeDef.PayCostContext context, CostTypeDef.PayCostResults result)
        {
            if (context.activatorInventory)
            {
                using var _ = ListPool<ItemIndex>.RentCollection(out List<ItemIndex> itemsToTake);
                ListUtils.EnsureCapacity(itemsToTake, context.cost);

                WeightedSelection<ItemIndex> itemSelection = new WeightedSelection<ItemIndex>();
                WeightedSelection<ItemIndex>[] scrapSelectionsByQuality = new WeightedSelection<ItemIndex>[(int)QualityTier.Count];
                WeightedSelection<ItemIndex>[] priorityScrapSelectionsByQuality = new WeightedSelection<ItemIndex>[(int)QualityTier.Count];
                WeightedSelection<ItemIndex>[] qualityRegeneratingScrapSelections = new WeightedSelection<ItemIndex>[(int)QualityTier.Count];

                ItemQualityGroupIndex avoidedItemGroupIndex = QualityCatalog.FindItemQualityGroupIndex(context.avoidedItemIndex);

                for (ItemIndex itemIndex = 0; (int)itemIndex < ItemCatalog.itemCount; itemIndex++)
                {
                    QualityTier qualityTier = QualityCatalog.GetQualityTier(itemIndex);
                    if (qualityTier == QualityTier.None)
                        continue;

                    ItemQualityGroupIndex itemGroupIndex = QualityCatalog.FindItemQualityGroupIndex(itemIndex);
                    if (itemGroupIndex == avoidedItemGroupIndex)
                        continue;

                    int itemCount = context.activatorInventory.GetItemCountPermanent(itemIndex);
                    if (itemCount <= 0)
                        continue;

                    ItemDef itemDef = ItemCatalog.GetItemDef(itemIndex);
                    if (itemDef.tier != context.costTypeDef.itemTier || !itemDef.canRemove || itemDef.ContainsTag(ItemTag.ObjectiveRelated))
                        continue;

                    WeightedSelection<ItemIndex>[] targetSelectionsByQuality = null;
                    if (itemGroupIndex == ItemQualitiesContent.ItemQualityGroups.RegeneratingScrap.GroupIndex)
                    {
                        targetSelectionsByQuality = qualityRegeneratingScrapSelections;
                    }
                    else if (itemDef.ContainsTag(ItemTag.PriorityScrap))
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

                void TakeItemsFromWeightedSelectionsDescending(WeightedSelection<ItemIndex>[] weightedSelections)
                {
                    for (int i = weightedSelections.Length - 1; i >= 0; i--)
                    {
                        if (weightedSelections[i] != null)
                        {
                            TakeItemsFromWeightedSelection(weightedSelections[i]);
                        }
                    }
                }

                void TakeItemsFromWeightedSelectionsAscending(WeightedSelection<ItemIndex>[] weightedSelections)
                {
                    for (int i = 0; i < weightedSelections.Length; i++)
                    {
                        if (weightedSelections[i] != null)
                        {
                            TakeItemsFromWeightedSelection(weightedSelections[i]);
                        }
                    }
                }

                bool TryTakeSingleItemFromWeightedSelection(WeightedSelection<ItemIndex> weightedSelection)
                {
                    if (weightedSelection.Count > 0 && itemsToTake.Count < context.cost)
                    {
                        int choiceIndex = weightedSelection.EvaluateToChoiceIndex(context.rng.nextNormalizedFloat);
                        TakeItemFromWeightedSelection(weightedSelection, choiceIndex);
                        return true;
                    }

                    return false;
                }

                void TryTakeSingleItemFromWeightedSelectionsDescending(WeightedSelection<ItemIndex>[] weightedSelections)
                {
                    for (int i = weightedSelections.Length - 1; i >= 0; i--)
                    {
                        if (weightedSelections[i] != null && TryTakeSingleItemFromWeightedSelection(weightedSelections[i]))
                        {
                            break;
                        }
                    }
                }

                TryTakeSingleItemFromWeightedSelectionsDescending(qualityRegeneratingScrapSelections);
                TakeItemsFromWeightedSelectionsDescending(priorityScrapSelectionsByQuality);
                TakeItemsFromWeightedSelectionsDescending(scrapSelectionsByQuality);
                TakeItemsFromWeightedSelectionsAscending(qualityRegeneratingScrapSelections);
                TakeItemsFromWeightedSelection(itemSelection);

                if (itemsToTake.Count < context.cost)
                {
                    ItemQualityGroup avoidedItemGroup = QualityCatalog.GetItemQualityGroup(avoidedItemGroupIndex);
                    if (avoidedItemGroup)
                    {
                        Span<QualityTier> avoidedItemQualityTakeOrder = stackalloc QualityTier[(int)QualityTier.Count];
                        for (QualityTier qualityTier = 0; qualityTier < QualityTier.Count; qualityTier++)
                        {
                            avoidedItemQualityTakeOrder[(int)qualityTier] = qualityTier;
                        }

                        Util.ShuffleSpan(avoidedItemQualityTakeOrder, context.rng);

                        ItemQualityCounts avoidedItemCounts = context.activatorInventory.GetItemCountsPermanent(avoidedItemGroup);
                        foreach (QualityTier qualityTier in avoidedItemQualityTakeOrder)
                        {
                            int itemCount = avoidedItemCounts[qualityTier];
                            int itemCountToAdd = Math.Min(itemCount, context.cost - itemsToTake.Count);
                            if (itemCountToAdd > 0)
                            {
                                ListUtils.AddMultiple(itemsToTake, avoidedItemGroup.GetItemIndex(qualityTier), itemCountToAdd);

                                if (itemsToTake.Count >= context.cost)
                                    break;
                            }
                        }
                    }
                    else
                    {
                        ListUtils.AddMultiple(itemsToTake, context.avoidedItemIndex, context.cost - itemsToTake.Count);
                    }
                }

                bool hasTakenAnyRegeneratingScrap = false;
                foreach (ItemIndex itemIndex in itemsToTake)
                {
                    Inventory.ItemTransformation takeItemTransformation = new Inventory.ItemTransformation
                    {
                        originalItemIndex = itemIndex,
                        newItemIndex = ItemIndex.None,
                        maxToTransform = 1,
                        forbidTempItems = true,
                        transformationType = (ItemTransformationTypeIndex)CharacterMasterNotificationQueue.TransformationType.None,
                    };

                    if (QualityCatalog.FindItemQualityGroupIndex(takeItemTransformation.originalItemIndex) == ItemQualitiesContent.ItemQualityGroups.RegeneratingScrap.GroupIndex)
                    {
                        takeItemTransformation.newItemIndex = ItemQualitiesContent.ItemQualityGroups.RegeneratingScrapConsumed.GetItemIndex(QualityCatalog.GetQualityTier(takeItemTransformation.originalItemIndex));
                        takeItemTransformation.transformationType = (ItemTransformationTypeIndex)CharacterMasterNotificationQueue.TransformationType.Default;
                    }

                    if (takeItemTransformation.TryTransform(context.activatorInventory, out Inventory.ItemTransformation.TryTransformResult tryTransformResult))
                    {
                        result.AddTakenItemsFromTransformation(tryTransformResult);

                        if (QualityCatalog.FindItemQualityGroupIndex(tryTransformResult.takenItem.itemIndex) == ItemQualitiesContent.ItemQualityGroups.RegeneratingScrap.GroupIndex)
                        {
                            hasTakenAnyRegeneratingScrap = true;
                        }
                    }
                }

                if (hasTakenAnyRegeneratingScrap)
                {
                    if (_regeneratingScrapProcSoundEventIndex != NetworkSoundEventIndex.Invalid)
                    {
                        EntitySoundManager.EmitSoundServer(_regeneratingScrapProcSoundEventIndex, context.activatorBody.gameObject);
                    }

                    if (_regeneratingScrapDisplayExplosionEffectIndex != EffectIndex.Invalid)
                    {
                        ModelLocator activatorModelLocator = context.activatorBody.modelLocator;
                        if (activatorModelLocator && activatorModelLocator.modelTransform && activatorModelLocator.modelTransform.TryGetComponent(out CharacterModel characterModel))
                        {
                            List<GameObject> itemDisplayObjects = characterModel.GetItemDisplayObjects(DLC1Content.Items.RegeneratingScrap.itemIndex);
                            if (itemDisplayObjects.Count > 0)
                            {
                                GameObject effectRoot = itemDisplayObjects[0];
                                EffectData effectData = new EffectData
                                {
                                    origin = effectRoot.transform.position,
                                    rotation = effectRoot.transform.rotation
                                };

                                EffectManager.SpawnEffect(_regeneratingScrapDisplayExplosionEffectIndex, effectData, true);
                            }
                        }
                    }

                    if (_regeneratingScrapPrinterExplosionEffectIndex != EffectIndex.Invalid)
                    {
                        if (context.purchasedObject.TryGetComponent(out ModelLocator purchasedObjectModelLocator))
                        {
                            ChildLocator modelChildLocator = purchasedObjectModelLocator.modelChildLocator;
                            if (modelChildLocator)
                            {
                                int dropPivotChildIndex = modelChildLocator.FindChildIndex("DropPivot");
                                Transform dropPivot = modelChildLocator.FindChild(dropPivotChildIndex);
                                if (dropPivot)
                                {
                                    EffectData effectData = new EffectData
                                    {
                                        origin = dropPivot.position
                                    };
                                    effectData.SetChildLocatorTransformReference(context.purchasedObject, dropPivotChildIndex);
                                    EffectManager.SpawnEffect(_regeneratingScrapPrinterExplosionEffectIndex, effectData, true);
                                }
                            }
                        }
                    }
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
