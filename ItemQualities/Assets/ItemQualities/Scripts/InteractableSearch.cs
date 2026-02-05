using RoR2.DirectionalSearch;

namespace ItemQualities
{
    internal sealed class InteractableSearch : BaseDirectionalSearch<InteractableInfoProvider, InteractableSearchSelector, InteractableSearchFilter>
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

        public bool forbidDuplicated
        {
            get => candidateFilter.forbidDuplicated;
            set => candidateFilter.forbidDuplicated = value;
        }

        public InteractableSearch() : base(default, default)
        {
        }

        public InteractableSearch(InteractableSearchSelector selector, InteractableSearchFilter candidateFilter) : base(selector, candidateFilter)
        {
        }
    }
}
