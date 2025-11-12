using ItemQualities.Utilities.Extensions;
using RoR2;
using System.Collections.Generic;

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

        static readonly Dictionary<ItemTier, CostTypeIndex> _baseItemCostTypesLookup = new Dictionary<ItemTier, CostTypeIndex>();

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
                else if (costTypeIndex > CostTypeIndex.None && costTypeIndex < CostTypeIndex.Count)
                {
                    if (costTypeDef.itemTier != ItemTier.NoTier)
                    {
                        _baseItemCostTypesLookup[costTypeDef.itemTier] = costTypeIndex;
                    }
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
            return activatorInventory && activatorInventory.HasAtLeastXTotalQualityItemsOfTier(costTypeDef.itemTier, context.cost);
        }

        static void payCostQualityItems(CostTypeDef costTypeDef, CostTypeDef.PayCostContext context)
        {
            // This is a bit strange, but in order to avoid incompatibilities and to not have to copy over all the PayCost code, just invoke the normal PayCost.
            // ItemCostQualityPatch will spot the quality CostType, and switch to only allow quality items as input instead of regular behavior

            if (!_baseItemCostTypesLookup.TryGetValue(costTypeDef.itemTier, out CostTypeIndex baseCostType))
            {
                Log.Error($"Failed to find base cost type for item tier {costTypeDef.itemTier} (in {costTypeDef.name})");
                return;
            }

            CostTypeDef baseCostTypeDef = CostTypeCatalog.GetCostTypeDef(baseCostType);
            baseCostTypeDef.payCost(costTypeDef, context);
        }

        public static bool IsQualityItemCostType(CostTypeDef costTypeDef)
        {
            return costTypeDef == _whiteItemQualityCostDef ||
                   costTypeDef == _greenItemQualityCostDef ||
                   costTypeDef == _redItemQualityCostDef ||
                   costTypeDef == _bossItemQualityCostDef;
        }
    }
}
