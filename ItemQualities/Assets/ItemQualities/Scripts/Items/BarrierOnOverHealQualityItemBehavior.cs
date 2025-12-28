namespace ItemQualities.Items
{
    public sealed class BarrierOnOverHealQualityItemBehavior : QualityItemBodyBehavior
    {
        [ItemGroupAssociation(QualityItemBehaviorUsageFlags.Server)]
        static ItemQualityGroup GetItemGroup()
        {
            return ItemQualitiesContent.ItemQualityGroups.BarrierOnOverHeal;
        }

        bool _hadBarrier = false;

        void OnEnable()
        {
            _hadBarrier = Body.healthComponent.barrier > 0f;
        }

        void FixedUpdate()
        {
            bool hasBarrier = Body.healthComponent.barrier > 0f;
            if (hasBarrier != _hadBarrier)
            {
                _hadBarrier = hasBarrier;
                Body.MarkAllStatsDirty();
            }
        }
    }
}
