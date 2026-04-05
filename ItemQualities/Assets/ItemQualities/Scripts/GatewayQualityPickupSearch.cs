using RoR2;
using RoR2.DirectionalSearch;
using System.Runtime.CompilerServices;

namespace ItemQualities
{
    public sealed class GatewayQualityPickupSearch : BaseDirectionalSearch<GatewayQualityPickupController, GatewayQualityPickupSearchSelector, GatewayQualityPickupSearchFilter>
    {
        public TeamIndex teamIndex
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => candidateFilter.TeamIndex;
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set => candidateFilter.TeamIndex = value;
        }

        public GatewayQualityPickupSearch(GatewayQualityPickupSearchSelector selector, GatewayQualityPickupSearchFilter candidateFilter) : base(selector, candidateFilter)
        {
        }

        public GatewayQualityPickupSearch() : base(default, default)
        {
        }
    }
}
