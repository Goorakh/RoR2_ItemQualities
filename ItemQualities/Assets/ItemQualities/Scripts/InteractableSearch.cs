using RoR2;
using RoR2.DirectionalSearch;

namespace ItemQualities
{
    public sealed class InteractableSearch : BaseDirectionalSearch<SpecialObjectAttributes, InteractableSearchSelector, InteractableSearchFilter>
    {
        public InteractableSearch() : base(default, default)
        {
        }

        public InteractableSearch(InteractableSearchSelector selector, InteractableSearchFilter candidateFilter) : base(selector, candidateFilter)
        {
        }
    }
}
