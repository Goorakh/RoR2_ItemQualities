using RoR2;
using RoR2.DirectionalSearch;

namespace ItemQualities
{
    public struct GatewayQualityPickupSearchFilter : IGenericDirectionalSearchFilter<GatewayQualityPickupController>
    {
        public TeamIndex TeamIndex;

        public readonly bool PassesFilter(GatewayQualityPickupController candidateInfo)
        {
            return candidateInfo.IsAvailable && candidateInfo.TeamFilter.teamIndex == TeamIndex;
        }
    }
}
