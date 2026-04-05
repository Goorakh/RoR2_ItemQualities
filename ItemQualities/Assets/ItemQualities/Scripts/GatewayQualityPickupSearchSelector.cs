using RoR2.DirectionalSearch;
using UnityEngine;

namespace ItemQualities
{
    public readonly struct GatewayQualityPickupSearchSelector : IGenericWorldSearchSelector<GatewayQualityPickupController>
    {
        public readonly GameObject GetRootObject(GatewayQualityPickupController source)
        {
            return source.gameObject;
        }

        public readonly Transform GetTransform(GatewayQualityPickupController source)
        {
            return source.CoreTransform ? source.CoreTransform : source.transform;
        }
    }
}
