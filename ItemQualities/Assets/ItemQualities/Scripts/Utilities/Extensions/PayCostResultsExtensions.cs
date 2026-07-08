using RoR2;
using RoR2BepInExPack.Utilities;
using System;

namespace ItemQualities.Utilities.Extensions
{
    internal static class PayCostResultsExtensions
    {
        private static bool _addedClearHook = false;

        private static readonly FixedConditionalWeakTable<CostTypeDef.PayCostResults, ExtraData> _extraDataTable = new FixedConditionalWeakTable<CostTypeDef.PayCostResults, ExtraData>();

        private static ExtraData getExtraData(CostTypeDef.PayCostResults payCostResults)
        {
            if (!_addedClearHook)
            {
                _addedClearHook = true;
                On.RoR2.CostTypeDef.PayCostResults.Clear += PayCostResults_Clear;
            }

            return _extraDataTable.GetOrCreateValue(payCostResults);
        }

        public static void SetUsedSaleStars(this CostTypeDef.PayCostResults payCostResults, ItemQualityCounts usedSaleStarCounts)
        {
            if (payCostResults is null)
                throw new ArgumentNullException(nameof(payCostResults));

            ExtraData extraData = getExtraData(payCostResults);
            extraData.UsedSaleStarCounts = usedSaleStarCounts;
        }

        public static ItemQualityCounts GetUsedSaleStars(this CostTypeDef.PayCostResults payCostResults)
        {
            if (payCostResults is null)
                throw new ArgumentNullException(nameof(payCostResults));

            return _extraDataTable.TryGetValue(payCostResults, out ExtraData extraData) ? extraData.UsedSaleStarCounts : default;
        }

        private static void PayCostResults_Clear(On.RoR2.CostTypeDef.PayCostResults.orig_Clear orig, CostTypeDef.PayCostResults self)
        {
            orig(self);
            _extraDataTable.Remove(self);
        }

        private sealed class ExtraData
        {
            public ItemQualityCounts UsedSaleStarCounts;
        }
    }
}
