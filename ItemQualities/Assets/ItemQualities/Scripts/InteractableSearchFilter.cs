using RoR2.DirectionalSearch;

namespace ItemQualities
{
    internal struct InteractableSearchFilter : IGenericDirectionalSearchFilter<InteractableInfoProvider>
    {
        public bool requireCanCopy;

        public bool forbidDuplicated;

        public readonly bool PassesFilter(InteractableInfoProvider interactable)
        {
            InteractableDef interactableDef = InteractableCatalog.GetInteractableDef(interactable.CatalogIndex);
            if (interactableDef == null)
                return false;

            if (requireCanCopy && !interactableDef.CanCopy)
                return false;

            if (forbidDuplicated && interactable.Duplicated)
                return false;

            if (interactable.SpecialObjectAttributes && (!interactable.SpecialObjectAttributes.grabbable || !interactable.SpecialObjectAttributes.isTargetable))
                return false;

            if (interactable.InteractableLockable != null && interactable.InteractableLockable.IsLocked())
                return false;

            return true;
        }
    }
}
