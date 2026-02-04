using RoR2.DirectionalSearch;
using UnityEngine;

namespace ItemQualities
{
    internal readonly struct InteractableSearchSelector : IGenericWorldSearchSelector<CatalogedInteractable>
    {
        public GameObject GetRootObject(CatalogedInteractable source)
        {
            return source.gameObject;
        }

        public Transform GetTransform(CatalogedInteractable source)
        {
            return source.IndicatorTransform;
        }
    }
}
