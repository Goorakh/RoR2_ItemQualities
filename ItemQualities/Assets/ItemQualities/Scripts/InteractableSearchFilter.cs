using RoR2;
using RoR2.DirectionalSearch;

namespace ItemQualities
{
    public readonly struct InteractableSearchFilter : IGenericDirectionalSearchFilter<SpecialObjectAttributes>
    {
        public bool PassesFilter(SpecialObjectAttributes candidateInfo)
        {
            return candidateInfo.grabbable && candidateInfo.isTargetable;
        }
    }
}
