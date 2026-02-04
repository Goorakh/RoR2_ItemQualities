using RoR2.DirectionalSearch;

namespace ItemQualities
{
    internal struct InteractableSearchFilter : IGenericDirectionalSearchFilter<CatalogedInteractable>
    {
        public bool requireCanCopy;

        public bool requireSpawnCard;

        public readonly bool PassesFilter(CatalogedInteractable interactable)
        {
            InteractableDef interactableDef = InteractableCatalog.GetInteractableDef(interactable.CatalogIndex);
            if (interactableDef == null)
                return false;

            if (requireSpawnCard && !interactableDef.SpawnCard)
                return false;

            if (requireCanCopy && !interactableDef.CanCopy)
                return false;

            if (interactable.SpecialObjectAttributes && (!interactable.SpecialObjectAttributes.grabbable || !interactable.SpecialObjectAttributes.isTargetable))
                return false;

            if (interactable.InteractableLockable != null && interactable.InteractableLockable.IsLocked())
                return false;

            return true;
        }
    }
}
