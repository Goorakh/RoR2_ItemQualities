using RoR2;
using RoR2BepInExPack.Utilities;
using System;

namespace ItemQualities.Utilities.Extensions
{
    static class PayCostResultsExtensions
    {
        static readonly FixedConditionalWeakTable<CostTypeDef.PayCostResults, ExtraData> _extraDataTable = new FixedConditionalWeakTable<CostTypeDef.PayCostResults, ExtraData>();

        public static void SetUsedSaleStars(this CostTypeDef.PayCostResults payCostResults, ItemQualityCounts usedSaleStarCounts)
        {
            if (payCostResults is null)
                throw new ArgumentNullException(nameof(payCostResults));

            ExtraData extraData = _extraDataTable.GetOrAddNew(payCostResults);
            extraData.UsedSaleStarCounts = usedSaleStarCounts;
        }

        public static ItemQualityCounts GetUsedSaleStars(this CostTypeDef.PayCostResults payCostResults)
        {
            if (payCostResults is null)
                throw new ArgumentNullException(nameof(payCostResults));

            return _extraDataTable.TryGetValue(payCostResults, out ExtraData extraData) ? extraData.UsedSaleStarCounts : default;
        }

        class ExtraData
        {
            public ItemQualityCounts UsedSaleStarCounts;
        }
    }
}
