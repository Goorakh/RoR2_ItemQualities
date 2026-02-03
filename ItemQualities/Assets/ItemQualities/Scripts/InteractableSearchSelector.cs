using RoR2;
using RoR2.DirectionalSearch;
using UnityEngine;

namespace ItemQualities
{
    public readonly struct InteractableSearchSelector : IGenericWorldSearchSelector<SpecialObjectAttributes>
    {
        public GameObject GetRootObject(SpecialObjectAttributes source)
        {
            return source.gameObject;
        }

        public Transform GetTransform(SpecialObjectAttributes source)
        {
            return source.indicatorOffset ? source.indicatorOffset : source.transform;
        }
    }
}
