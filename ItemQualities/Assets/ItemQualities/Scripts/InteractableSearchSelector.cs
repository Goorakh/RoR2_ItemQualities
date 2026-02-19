using RoR2.DirectionalSearch;
using UnityEngine;

namespace ItemQualities
{
    internal readonly struct InteractableSearchSelector : IGenericWorldSearchSelector<InteractableInfoProvider>
    {
        public GameObject GetRootObject(InteractableInfoProvider source)
        {
            return source.gameObject;
        }

        public Transform GetTransform(InteractableInfoProvider source)
        {
            return source.IndicatorTransform;
        }
    }
}
