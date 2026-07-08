using RoR2;
using System;
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

        public static event Action<InteractableInfoProvider> OnCatalogedInteractableStartGlobal;

        private void Awake()
        {
            SpecialObjectAttributes = GetComponent<SpecialObjectAttributes>();
            PurchaseInteraction = GetComponent<PurchaseInteraction>();
            InteractableLockable = GetComponent<IInteractableLockable>();
        }

        private void OnEnable()
        {
            InstanceTracker.Add(this);
        }

        private void OnDisable()
        {
            InstanceTracker.Remove(this);
        }

        private void Start()
        {
            if (CatalogIndex != -1)
            {
                OnCatalogedInteractableStartGlobal?.Invoke(this);
            }
            else
            {
                Log.Warning($"Failed to resolve interactable catalog index for {Util.GetGameObjectHierarchyName(gameObject)}");
            }
        }
    }
}
