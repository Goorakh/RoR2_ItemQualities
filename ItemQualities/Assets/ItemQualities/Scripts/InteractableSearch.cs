using RoR2.DirectionalSearch;

namespace ItemQualities
{
    internal sealed class InteractableSearch : BaseDirectionalSearch<CatalogedInteractable, InteractableSearchSelector, InteractableSearchFilter>
    {
        public bool requireCanCopy
        {
            get => candidateFilter.requireCanCopy;
            set => candidateFilter.requireCanCopy = value;
        }

        public bool requireSpawnCard
        {
            get => candidateFilter.requireSpawnCard;
            set => candidateFilter.requireSpawnCard = value;
        }

        public InteractableSearch() : base(default, default)
        {
        }

        public InteractableSearch(InteractableSearchSelector selector, InteractableSearchFilter candidateFilter) : base(selector, candidateFilter)
        {
        }
    }
}
