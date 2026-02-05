using RoR2;
using UnityEngine;

namespace ItemQualities
{
    internal sealed class InteractableInfoProvider : MonoBehaviour
    {
        public int CatalogIndex = -1;

        public bool Duplicated;

        public SpecialObjectAttributes SpecialObjectAttributes { get; private set; }

        public PurchaseInteraction PurchaseInteraction { get; private set; }

        public IInteractableLockable InteractableLockable { get; private set; }

        public Transform IndicatorTransform
        {
            get
            {
                if (SpecialObjectAttributes && SpecialObjectAttributes.indicatorOffset)
                    return SpecialObjectAttributes.indicatorOffset;

                return transform;
            }
        }

        void Awake()
        {
            SpecialObjectAttributes = GetComponent<SpecialObjectAttributes>();
            PurchaseInteraction = GetComponent<PurchaseInteraction>();
            InteractableLockable = GetComponent<IInteractableLockable>();
        }

        void OnEnable()
        {
            InstanceTracker.Add(this);
        }

        void OnDisable()
        {
            InstanceTracker.Remove(this);
        }
    }
}
